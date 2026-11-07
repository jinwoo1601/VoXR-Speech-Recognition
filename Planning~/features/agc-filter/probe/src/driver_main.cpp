// ============================================================================
// Purpose:  WAV driver for the probe DLL: 48 kHz fixture to 16 kHz, 160-sample frames through probe_fe_process, a JSON line per frame and a summary
// Layer:    Planning.AgcFilter.Probe
// Owns:     (script, no public types)
// Depends:  FrameReading, ProbeChain, Downsampler, WavData, LoadWav, probe_fe_create, probe_fe_process, probe_fe_destroy
// ============================================================================

#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include "agc2_chain.h"
#include "downsampler.h"
#include "wav_reader.h"

extern "C" {
__declspec(dllimport) void* __cdecl probe_fe_create(float target_db);
__declspec(dllimport) void __cdecl probe_fe_process(void* handle, float* frame, FrameReading* reading);
__declspec(dllimport) void __cdecl probe_fe_destroy(void* handle);
}

namespace {

constexpr float kTargetDb = -18.0f;
constexpr float kSpeechThreshold = 0.95f;

}

int main(int argc, char** argv) {
    std::string wav_path;
    for (int i = 1; i + 1 < argc; ++i) {
        if (std::strcmp(argv[i], "--wav") == 0) {
            wav_path = argv[i + 1];
        }
    }
    if (wav_path.empty()) {
        std::fprintf(stderr, "usage: agc2-probe-driver --wav <path>\n");
        return 2;
    }

    WavData wav;
    const std::string error = LoadWav(wav_path, wav);
    if (!error.empty()) {
        std::fprintf(stderr, "%s\n", error.c_str());
        return 2;
    }

    Downsampler downsampler;
    std::vector<float> samples_16k(wav.samples.size() / Downsampler::kDecimationFactor + 1);
    const uint32_t count_16k = downsampler.Process(
        wav.samples.data(), static_cast<uint32_t>(wav.samples.size()), samples_16k.data());
    const int frames = static_cast<int>(count_16k / ProbeChain::kFrameSamples);
    const int remainder = static_cast<int>(count_16k % ProbeChain::kFrameSamples);

    void* chain = probe_fe_create(kTargetDb);
    bool valid = true;
    float max_probability = 0.0f;
    int frames_over_threshold = 0;
    float gain_first = 0.0f;
    float gain_min = 0.0f;
    float gain_max = 0.0f;
    float gain_last = 0.0f;
    for (int f = 0; f < frames; ++f) {
        FrameReading reading;
        probe_fe_process(chain, samples_16k.data() + f * ProbeChain::kFrameSamples, &reading);
        std::printf(
            "{\"frame\": %d, \"speech_probability\": %.6g, \"gain_db\": %.6g, \"pre_rms\": %.6g, \"post_rms\": %.6g}\n",
            f, reading.speech_probability, reading.gain_db, reading.pre_rms, reading.post_rms);

        if (!(reading.speech_probability >= 0.0f && reading.speech_probability <= 1.0f) ||
            !std::isfinite(reading.gain_db)) {
            valid = false;
        }
        if (reading.speech_probability > max_probability) {
            max_probability = reading.speech_probability;
        }
        if (reading.speech_probability >= kSpeechThreshold) {
            ++frames_over_threshold;
        }
        if (f == 0) {
            gain_first = reading.gain_db;
            gain_min = reading.gain_db;
            gain_max = reading.gain_db;
        }
        gain_min = std::fmin(gain_min, reading.gain_db);
        gain_max = std::fmax(gain_max, reading.gain_db);
        gain_last = reading.gain_db;
    }
    probe_fe_destroy(chain);

    std::printf(
        "{\"summary\": {\"frames\": %d, \"remainder_samples\": %d, \"max_speech_probability\": %.6g, "
        "\"threshold\": %.6g, \"frames_over_threshold\": %d, \"gain_db_first\": %.6g, \"gain_db_min\": %.6g, "
        "\"gain_db_max\": %.6g, \"gain_db_last\": %.6g, \"valid\": %s}}\n",
        frames, remainder, max_probability, kSpeechThreshold, frames_over_threshold, gain_first, gain_min,
        gain_max, gain_last, valid ? "true" : "false");
    return valid ? 0 : 1;
}
