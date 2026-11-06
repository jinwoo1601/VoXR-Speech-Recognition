#include "vosk_bridge.h"
#include "vosk_api.h"
#include "ring_buffer.h"
#include "front_end.h"
#include <memory>
#include "result_queue.h"
#include "audio_capture.h"

#include <thread>
#include <atomic>
#include <string>
#include <cstring>
#include <chrono>
#include <cmath>
#include <algorithm>
#include "logging.h"

// --- Static global state (singleton bridge) ---

static VoskModel*       g_model       = nullptr;
static VoskRecognizer*  g_recognizer  = nullptr;
static float            g_sample_rate = 16000.0f;

static RingBuffer<float> g_ring_buffer;
static ResultQueue       g_result_queue;
static AudioCapture      g_audio_capture;
static std::unique_ptr<voxr::FrontEnd> g_frontend;

static std::atomic<uint32_t> g_fe_seq{0};
static std::atomic<uint64_t> g_fe_input_samples{0};
static std::atomic<uint64_t> g_fe_output_samples{0};
static std::atomic<uint64_t> g_fe_digest{0};
static std::atomic<float>    g_fe_gain_db{0.0f};
static std::atomic<float>    g_fe_pre_rms{0.0f};
static std::atomic<float>    g_fe_post_rms{0.0f};

static std::thread       g_recognition_thread;
static std::atomic<bool> g_running{false};
static std::atomic<bool> g_initialised{false};

// Push mode: audio arrives via vosk_bridge_push_audio instead of a capture
// backend. Owned by the start helper; cleared on stop/destroy so a stale
// flag can never let pushes into a capture session (single-producer ring).
static std::atomic<bool>  g_push_mode{false};
static std::atomic<float> g_input_level{0.0f};

static std::string       g_last_error;
static std::string       g_last_partial;
static QueuedResult      g_current_result;

// Bridge timing: input samples the loop has read this session (reset at each
// session start, held after stop for vosk_bridge_get_consumed), decoder
// samples fed since the recognizer was created (Vosk's word times run on
// across vosk_recognizer_reset), and that decoder count at session start —
// the session's word-time origin. Every queued result carries the first and
// the last.
static std::atomic<int64_t> g_consumed{0};
static int64_t              g_decoder_fed    = 0;
static int64_t              g_session_origin = 0;

// Processing buffer sizes
static constexpr uint32_t kReadChunkSize = 4096;       // 48 kHz samples per read (~85 ms)
static constexpr uint32_t kInt16Capacity = kReadChunkSize / voxr::Decimator::kFactor + voxr::FrontEnd::kFrameSamples;

static void reset_pipeline() {
    g_ring_buffer.Reset();
    g_result_queue.Clear();
    g_last_partial.clear();
}

static void publish_frontend_stats() {
    voxr::FrontEnd::Stats stats = g_frontend->GetStats();
    uint32_t seq = g_fe_seq.load(std::memory_order_relaxed);
    g_fe_seq.store(seq + 1, std::memory_order_relaxed);
    std::atomic_thread_fence(std::memory_order_release);
    g_fe_input_samples.store(stats.input_samples, std::memory_order_relaxed);
    g_fe_output_samples.store(stats.output_samples, std::memory_order_relaxed);
    g_fe_digest.store(stats.digest, std::memory_order_relaxed);
    g_fe_gain_db.store(stats.gain_db, std::memory_order_relaxed);
    g_fe_pre_rms.store(stats.pre_rms, std::memory_order_relaxed);
    g_fe_post_rms.store(stats.post_rms, std::memory_order_relaxed);
    g_fe_seq.store(seq + 2, std::memory_order_release);
}

// --- Recognition thread ---

static void recognition_loop() {
    float read_buf[kReadChunkSize];
    int16_t int16_buf[kInt16Capacity];

    LOGI("Recognition thread started");

    while (g_running.load(std::memory_order_acquire)) {
        if (!g_push_mode.load(std::memory_order_acquire) &&
            g_audio_capture.HasError()) {
            LOGE("Audio capture error detected, stopping recognition");
            g_result_queue.Push(
                std::string("{\"error\": \"audio device error\", \"code\": ")
                    + std::to_string(VOSK_BRIDGE_ERR_AUDIO_DEVICE_UNAVAIL) + "}",
                false, g_consumed.load(std::memory_order_relaxed), g_session_origin);
            break;
        }

        uint32_t read_count = g_ring_buffer.Read(read_buf, kReadChunkSize);

        if (read_count == 0) {
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
            continue;
        }

        g_consumed += read_count;

        {
            float sum_sq = 0.0f;
            for (uint32_t i = 0; i < read_count; ++i)
                sum_sq += read_buf[i] * read_buf[i];
            float chunk_rms = std::sqrt(sum_sq / static_cast<float>(read_count));
            float pre_dsp_rate = g_sample_rate * voxr::Decimator::kFactor;
            float chunk_ms = static_cast<float>(read_count) * 1000.0f / pre_dsp_rate;
            float alpha = 1.0f - std::exp(-chunk_ms / 300.0f);
            float level = g_input_level.load(std::memory_order_relaxed);
            g_input_level.store(level + alpha * (chunk_rms - level),
                                std::memory_order_relaxed);
        }

        int count = g_frontend->Process(read_buf, static_cast<int>(read_count), int16_buf, kInt16Capacity);
        publish_frontend_stats();

        if (count > 0) {
            int result = vosk_recognizer_accept_waveform_s(
                g_recognizer, int16_buf, count);
            g_decoder_fed += count;

            if (result == 1) {
                const char* json = vosk_recognizer_result(g_recognizer);
                if (json)
                    g_result_queue.Push(std::string(json), true,
                                        g_consumed.load(std::memory_order_relaxed),
                                        g_session_origin);
                else
                    LOGE("vosk_recognizer_result returned NULL");
            } else {
                const char* json = vosk_recognizer_partial_result(g_recognizer);
                if (json && json != g_last_partial) {
                    g_last_partial = json;
                    g_result_queue.Push(std::string(json), false,
                                        g_consumed.load(std::memory_order_relaxed),
                                        g_session_origin);
                }
            }
        }

        if (g_ring_buffer.CheckOverflow()) {
            g_result_queue.Push(
                std::string("{\"error\": \"ring buffer overflow\", \"code\": ")
                    + std::to_string(VOSK_BRIDGE_ERR_RING_BUFFER_OVERFLOW) + "}",
                false, g_consumed.load(std::memory_order_relaxed), g_session_origin);
            LOGI("Ring buffer overflow detected");
        }
    }

    g_running.store(false, std::memory_order_release);

    const char* final_json = vosk_recognizer_final_result(g_recognizer);
    if (final_json)
        g_result_queue.Push(std::string(final_json), true,
                            g_consumed.load(std::memory_order_relaxed), g_session_origin);
    else
        LOGE("vosk_recognizer_final_result returned NULL");

    LOGI("Recognition thread exiting");
}

static int start_internal(bool start_capture) {
    g_last_error.clear();

    if (!g_initialised.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_NOT_INITIALISED;

    if (g_running.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_ALREADY_RUNNING;

    reset_pipeline();
    g_frontend->BeginStream();
    publish_frontend_stats();
    g_consumed = 0;
    g_session_origin = g_decoder_fed;
    g_input_level.store(0.0f, std::memory_order_relaxed);
    g_push_mode.store(!start_capture, std::memory_order_release);

    if (start_capture) {
        int audio_result = g_audio_capture.Start(&g_ring_buffer);
        if (audio_result != VOSK_BRIDGE_OK) {
            g_last_error = "Failed to start audio capture";
            g_push_mode.store(false, std::memory_order_release);
            return audio_result;
        }
    }

    g_running.store(true, std::memory_order_release);
    g_recognition_thread = std::thread(recognition_loop);

    LOGI("Recognition started%s", start_capture ? "" : " (push mode)");
    return VOSK_BRIDGE_OK;
}

// --- Bridge C API implementation ---

extern "C" {

int vosk_bridge_init(const char* model_path, float sample_rate,
                     float mic_gain_target_db) {
    g_last_error.clear();

    if (!model_path) {
        g_last_error = "model_path is null";
        return VOSK_BRIDGE_ERR_MODEL_LOAD_FAILED;
    }

    if (g_initialised.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_ALREADY_INITIALISED;

    vosk_set_log_level(-1);

    g_model = vosk_model_new(model_path);
    if (!g_model) {
        g_last_error = "vosk_model_new() returned NULL for path: " + std::string(model_path);
        LOGE("%s", g_last_error.c_str());
        return VOSK_BRIDGE_ERR_MODEL_LOAD_FAILED;
    }

    g_sample_rate = sample_rate;
    g_recognizer = vosk_recognizer_new(g_model, g_sample_rate);
    g_decoder_fed = 0;
    if (!g_recognizer) {
        g_last_error = "vosk_recognizer_new() returned NULL";
        LOGE("%s", g_last_error.c_str());
        vosk_model_free(g_model);
        g_model = nullptr;
        return VOSK_BRIDGE_ERR_MODEL_LOAD_FAILED;
    }

    vosk_recognizer_set_words(g_recognizer, 1);

    g_frontend = std::make_unique<voxr::FrontEnd>(mic_gain_target_db);
    publish_frontend_stats();

    reset_pipeline();

    g_initialised.store(true, std::memory_order_release);
    LOGI("Bridge initialised: model=%s, sample_rate=%.0f, agc_target=%.1f dB",
         model_path, sample_rate, mic_gain_target_db);
    return VOSK_BRIDGE_OK;
}

void vosk_bridge_destroy() {
    if (!g_initialised.load(std::memory_order_acquire))
        return;

    g_running.store(false, std::memory_order_release);
    g_audio_capture.Stop();
    if (g_recognition_thread.joinable())
        g_recognition_thread.join();

    g_push_mode.store(false, std::memory_order_release);
    g_input_level.store(0.0f, std::memory_order_relaxed);

    if (g_recognizer) {
        vosk_recognizer_free(g_recognizer);
        g_recognizer = nullptr;
    }

    if (g_model) {
        vosk_model_free(g_model);
        g_model = nullptr;
    }

    g_frontend->Reset();
    publish_frontend_stats();
    reset_pipeline();

    g_initialised.store(false, std::memory_order_release);
    LOGI("Bridge destroyed");
}

int vosk_bridge_start() {
    return start_internal(true);
}

int vosk_bridge_start_push() {
    return start_internal(false);
}

void vosk_bridge_stop() {
    g_running.store(false, std::memory_order_release);
    g_audio_capture.Stop();

    if (g_recognition_thread.joinable())
        g_recognition_thread.join();

    g_push_mode.store(false, std::memory_order_release);
    g_input_level.store(0.0f, std::memory_order_relaxed);

    LOGI("Recognition stopped");
}

int vosk_bridge_reset() {
    g_last_error.clear();

    if (!g_initialised.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_NOT_INITIALISED;

    bool was_running = g_running.load(std::memory_order_acquire);
    bool was_push    = g_push_mode.load(std::memory_order_acquire);
    if (was_running)
        vosk_bridge_stop();

    vosk_recognizer_reset(g_recognizer);
    g_frontend->Reset();
    publish_frontend_stats();

    if (was_running) {
        int restart = start_internal(!was_push);
        if (restart != VOSK_BRIDGE_OK)
            return restart;
    }

    return VOSK_BRIDGE_OK;
}

int vosk_bridge_push_audio(const float* samples, uint32_t count) {
    if (!g_initialised.load(std::memory_order_acquire))
        return -VOSK_BRIDGE_ERR_NOT_INITIALISED;

    if (!g_running.load(std::memory_order_acquire) ||
        !g_push_mode.load(std::memory_order_acquire))
        return -VOSK_BRIDGE_ERR_NOT_RUNNING;

    if (!samples || count == 0)
        return 0;

    // Clamp to free space so pushed audio is never overwritten: a short
    // write tells the caller to drain and retry (capture backends instead
    // overwrite oldest and flag overflow). Writing up to capacity exactly
    // is safe — the overflow checks use strict '>'.
    uint32_t free_space = RingBuffer<float>::kCapacity - g_ring_buffer.Available();
    uint32_t to_write = std::min(count, free_space);
    if (to_write > 0)
        g_ring_buffer.Write(samples, to_write);
    return static_cast<int>(to_write);
}

float vosk_bridge_get_input_level() {
    return g_input_level.load(std::memory_order_relaxed);
}

int vosk_bridge_set_grammar(const char* grammar_json) {
    g_last_error.clear();

    if (!g_initialised.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_NOT_INITIALISED;

    if (g_running.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_ALREADY_RUNNING;

    // Free existing recognizer
    if (g_recognizer) {
        vosk_recognizer_free(g_recognizer);
        g_recognizer = nullptr;
    }

    // Create new recognizer with or without grammar
    if (grammar_json && grammar_json[0] != '\0')
        g_recognizer = vosk_recognizer_new_grm(g_model, g_sample_rate, grammar_json);
    else
        g_recognizer = vosk_recognizer_new(g_model, g_sample_rate);
    g_decoder_fed = 0;

    if (!g_recognizer) {
        g_last_error = "vosk_recognizer_new failed during set_grammar";
        LOGE("%s", g_last_error.c_str());
        return VOSK_BRIDGE_ERR_MODEL_LOAD_FAILED;
    }

    vosk_recognizer_set_words(g_recognizer, 1);

    LOGI("Grammar %s", (grammar_json && grammar_json[0] != '\0') ? "applied" : "cleared");
    return VOSK_BRIDGE_OK;
}

int vosk_bridge_has_result() {
    return g_result_queue.HasResult() ? 1 : 0;
}

const char* vosk_bridge_get_result(int* out_is_final, int* out_length) {
    if (!g_result_queue.Pop(g_current_result))
        return nullptr;
    if (out_is_final)
        *out_is_final = g_current_result.is_final ? 1 : 0;
    if (out_length)
        *out_length = static_cast<int>(g_current_result.json.size());
    return g_current_result.json.c_str();
}

void vosk_bridge_get_result_position(int64_t* out_consumed, int64_t* out_origin) {
    if (out_consumed)
        *out_consumed = g_current_result.consumed;
    if (out_origin)
        *out_origin = g_current_result.origin;
}

int64_t vosk_bridge_get_consumed(void) {
    return g_consumed.load(std::memory_order_acquire);
}

int vosk_bridge_get_frontend_stats(uint64_t* out_input_samples, uint64_t* out_output_samples,
                                   uint64_t* out_digest, float* out_gain_db,
                                   float* out_pre_rms, float* out_post_rms) {
    if (!g_initialised.load(std::memory_order_acquire))
        return VOSK_BRIDGE_ERR_NOT_INITIALISED;

    uint32_t before = 0;
    uint32_t after = 0;
    uint64_t input_samples = 0;
    uint64_t output_samples = 0;
    uint64_t digest = 0;
    float gain_db = 0.0f;
    float pre_rms = 0.0f;
    float post_rms = 0.0f;
    do {
        before = g_fe_seq.load(std::memory_order_acquire);
        input_samples = g_fe_input_samples.load(std::memory_order_relaxed);
        output_samples = g_fe_output_samples.load(std::memory_order_relaxed);
        digest = g_fe_digest.load(std::memory_order_relaxed);
        gain_db = g_fe_gain_db.load(std::memory_order_relaxed);
        pre_rms = g_fe_pre_rms.load(std::memory_order_relaxed);
        post_rms = g_fe_post_rms.load(std::memory_order_relaxed);
        std::atomic_thread_fence(std::memory_order_acquire);
        after = g_fe_seq.load(std::memory_order_relaxed);
    } while ((before & 1u) != 0u || before != after);

    if (out_input_samples)
        *out_input_samples = input_samples;
    if (out_output_samples)
        *out_output_samples = output_samples;
    if (out_digest)
        *out_digest = digest;
    if (out_gain_db)
        *out_gain_db = gain_db;
    if (out_pre_rms)
        *out_pre_rms = pre_rms;
    if (out_post_rms)
        *out_post_rms = post_rms;
    return VOSK_BRIDGE_OK;
}

int vosk_bridge_is_running() {
    return g_running.load(std::memory_order_acquire) ? 1 : 0;
}

int vosk_bridge_is_initialised() {
    return g_initialised.load(std::memory_order_acquire) ? 1 : 0;
}

int vosk_bridge_get_error(char* buf, int buf_size) {
    if (!buf || buf_size <= 0)
        return 0;

    int len = static_cast<int>(g_last_error.size());
    int copy_len = (len < buf_size - 1) ? len : buf_size - 1;
    std::memcpy(buf, g_last_error.c_str(), copy_len);
    buf[copy_len] = '\0';
    return copy_len;
}

} // extern "C"
