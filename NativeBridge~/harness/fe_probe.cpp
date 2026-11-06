// ============================================================================
// Purpose:  Evidence instrument for the front-end unit: decimator response and sweeps, configuration, int16 bounds, pause and burst gain, allocation, cost and latency, one JSON report with a pass section
// Layer:    NativeBridge.Harness
// Owns:     (executable entry point, no public types)
// Depends:  voxr::FrontEnd, voxr::Decimator, voxr::FloatS16ToInt16, kDecimatorDesign, kDecimatorTaps, WavData, LoadWav
// ============================================================================

#include "decimator.h"
#include "decimator_taps.h"
#include "front_end.h"
#include "wav_reader.h"

#include <nlohmann/json.hpp>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <new>
#include <random>
#include <string>
#include <vector>

using nlohmann::ordered_json;

namespace {

uint64_t g_operator_new_calls = 0;

}

void* operator new(std::size_t size) {
    ++g_operator_new_calls;
    void* block = std::malloc(size == 0 ? 1 : size);
    if (block == nullptr) {
        throw std::bad_alloc();
    }
    return block;
}

void operator delete(void* block) noexcept {
    std::free(block);
}

void operator delete(void* block, std::size_t) noexcept {
    std::free(block);
}

namespace {

constexpr double kPi = 3.14159265358979323846;
constexpr int kInputRateHz = 48000;
constexpr float kTargetDb = -18.0f;
constexpr float kAltTargetDb = -12.0f;
constexpr int kCallSamples = 480;
constexpr int kOutputCapacity = kCallSamples / voxr::Decimator::kFactor + voxr::FrontEnd::kFrameSamples;
constexpr int kDroppedOutputs = voxr::kDecimatorTapCount / voxr::Decimator::kFactor;

constexpr double kResponseStepHz = 10.0;
constexpr double kPassEdgeHz = 7600.0;
constexpr double kStopEdgeHz = 8400.0;
constexpr double kDcToleranceDb = 0.1;
constexpr double kPassFloorDb = -1.0;
constexpr double kStopCeilingDb = -50.0;
constexpr double kSweepAmplitude = 0.5;
constexpr int kSweepSamples = kInputRateHz;
constexpr int kSweepStepHz = 100;
constexpr int kSweepPassTopHz = 7600;
constexpr int kSweepStopBottomHz = 8400;
constexpr int kSweepStopTopHz = 23900;
constexpr int kSweepAliasToDcHz = 16000;

constexpr int kF5RampSteps = 4 * 4 * 32768;
constexpr float kF5RampStep = 0.25f;
constexpr double kF5SineHz = 1000.0;
constexpr int kF5SineSamples = 2 * kInputRateHz;
constexpr float kF5CheckLevel = 1.0f;

constexpr float kConfidentProbability = 0.95f;
constexpr int kConfidentRun = 12;
constexpr int kWindowFrames = 50;
constexpr int kPauseSamples = 3 * kInputRateHz;
constexpr int kBurstSamples = kInputRateHz / 5;
constexpr int kNoiseOnlySamples = 10 * kInputRateHz;
constexpr double kNoiseFloorDbfs = -60.0;
constexpr uint32_t kPauseNoiseSeed = 0x564f5852u;
constexpr uint32_t kBurstSeed = 0x42555253u;
constexpr uint32_t kNoiseOnlySeed = 0x4e4f4953u;

constexpr double kLatencyBarMs = 15.0;
constexpr int kLatencySamples = kInputRateHz;

struct FrameRecord {
    float gain_db;
    float pre_rms;
    float post_rms;
    float speech_probability;
};

struct Meter {
    uint64_t process_calls = 0;
    uint64_t allocations = 0;
    std::vector<double> frame_us;
};

double Db(double ratio) {
    return 20.0 * std::log10(ratio);
}

double Centi(double db) {
    return std::round(db * 100.0) / 100.0;
}

int TimedProcess(voxr::FrontEnd& unit, const float* in, int n, int16_t* out, int cap, Meter& meter,
                 std::vector<float>* floats = nullptr) {
    float* out_float = floats == nullptr ? nullptr : floats->data();
    const uint64_t before = g_operator_new_calls;
    const auto start = std::chrono::steady_clock::now();
    const int written = unit.Process(in, n, out, cap, out_float);
    const auto stop = std::chrono::steady_clock::now();
    meter.allocations += g_operator_new_calls - before;
    ++meter.process_calls;
    if (written > 0) {
        const double us = std::chrono::duration<double, std::micro>(stop - start).count();
        meter.frame_us.push_back(us / (written / voxr::FrontEnd::kFrameSamples));
    }
    return written;
}

std::vector<FrameRecord> RunFrames(voxr::FrontEnd& unit, const std::vector<float>& signal,
                                   std::vector<int16_t>* output, Meter& meter,
                                   std::vector<float>* floats = nullptr) {
    std::vector<FrameRecord> frames;
    std::vector<int16_t> out(kOutputCapacity);
    std::vector<float> out_float(kOutputCapacity);
    for (size_t start = 0; start < signal.size(); start += kCallSamples) {
        const int n = static_cast<int>(std::min<size_t>(kCallSamples, signal.size() - start));
        const int written = TimedProcess(unit, signal.data() + start, n, out.data(), kOutputCapacity, meter,
                                         floats == nullptr ? nullptr : &out_float);
        if (written <= 0) {
            continue;
        }
        const voxr::FrontEnd::Stats stats = unit.GetStats();
        frames.push_back({stats.gain_db, stats.pre_rms, stats.post_rms, unit.SpeechProbability()});
        if (output != nullptr) {
            output->insert(output->end(), out.begin(), out.begin() + written);
        }
        if (floats != nullptr) {
            floats->insert(floats->end(), out_float.begin(), out_float.begin() + written);
        }
    }
    return frames;
}

std::vector<float> WhiteNoise(size_t count, double rms, uint32_t seed) {
    std::mt19937 engine(seed);
    std::vector<double> raw(count);
    double energy = 0.0;
    for (size_t i = 0; i < count; ++i) {
        raw[i] = static_cast<double>(engine()) / 4294967295.0 * 2.0 - 1.0;
        energy += raw[i] * raw[i];
    }
    const double scale = rms / std::sqrt(energy / static_cast<double>(count));
    std::vector<float> noise(count);
    for (size_t i = 0; i < count; ++i) {
        noise[i] = static_cast<float>(raw[i] * scale);
    }
    return noise;
}

std::vector<float> Sine(size_t count, double hz, double amplitude) {
    std::vector<float> sine(count);
    for (size_t i = 0; i < count; ++i) {
        sine[i] = static_cast<float>(amplitude * std::sin(2.0 * kPi * hz * static_cast<double>(i) / kInputRateHz));
    }
    return sine;
}

double DecimatedRms(const std::vector<float>& signal) {
    voxr::Decimator decimator;
    std::vector<float> out(signal.size() / voxr::Decimator::kFactor + 1);
    const int produced = decimator.Process(signal.data(), static_cast<int>(signal.size()), out.data());
    double energy = 0.0;
    for (int i = kDroppedOutputs; i < produced; ++i) {
        energy += static_cast<double>(out[i]) * out[i];
    }
    return std::sqrt(energy / (produced - kDroppedOutputs));
}

double TableResponseDb(double hz) {
    double re = 0.0;
    double im = 0.0;
    for (int k = 0; k < voxr::kDecimatorTapCount; ++k) {
        const double w = 2.0 * kPi * hz * k / kInputRateHz;
        re += static_cast<double>(voxr::kDecimatorTaps[k]) * std::cos(w);
        im -= static_cast<double>(voxr::kDecimatorTaps[k]) * std::sin(w);
    }
    return Db(std::sqrt(re * re + im * im));
}

double RmsOver(const std::vector<FrameRecord>& frames, const std::vector<int>& indices, float FrameRecord::*field) {
    if (indices.empty()) {
        return 0.0;
    }
    double energy = 0.0;
    for (int i : indices) {
        const double value = frames[i].*field;
        energy += value * value;
    }
    return std::sqrt(energy / static_cast<double>(indices.size()));
}

double MaxGainOver(const std::vector<FrameRecord>& frames, const std::vector<int>& indices) {
    double max_gain = -INFINITY;
    for (int i : indices) {
        max_gain = std::max(max_gain, Centi(frames[i].gain_db));
    }
    return max_gain;
}

ordered_json MeasureF1(bool& table_pass, bool& sweep_pass) {
    const double dc_db = TableResponseDb(0.0);
    double pass_min_db = INFINITY;
    double pass_min_hz = 0.0;
    double stop_max_db = -INFINITY;
    double stop_max_hz = 0.0;
    const int grid_points = static_cast<int>(kInputRateHz / 2 / kResponseStepHz);
    for (int i = 0; i <= grid_points; ++i) {
        const double hz = i * kResponseStepHz;
        const double db = TableResponseDb(hz);
        if (hz <= kPassEdgeHz && db < pass_min_db) {
            pass_min_db = db;
            pass_min_hz = hz;
        }
        if (hz >= kStopEdgeHz && db > stop_max_db) {
            stop_max_db = db;
            stop_max_hz = hz;
        }
    }
    table_pass = std::fabs(dc_db) <= kDcToleranceDb && pass_min_db >= kPassFloorDb && stop_max_db <= kStopCeilingDb;

    double sweep_pass_min_db = INFINITY;
    int sweep_pass_min_hz = 0;
    double sweep_stop_max_db = -INFINITY;
    int sweep_stop_max_hz = 0;
    int sweep_points = 0;
    for (int hz = kSweepStepHz; hz <= kSweepStopTopHz; hz += kSweepStepHz) {
        const bool in_pass = hz <= kSweepPassTopHz;
        const bool in_stop = hz >= kSweepStopBottomHz && hz != kSweepAliasToDcHz;
        if (!in_pass && !in_stop) {
            continue;
        }
        const double amplitude = std::sqrt(2.0) * DecimatedRms(Sine(kSweepSamples, hz, kSweepAmplitude));
        const double db = Db(amplitude / kSweepAmplitude);
        ++sweep_points;
        if (in_pass && db < sweep_pass_min_db) {
            sweep_pass_min_db = db;
            sweep_pass_min_hz = hz;
        }
        if (in_stop && db > sweep_stop_max_db) {
            sweep_stop_max_db = db;
            sweep_stop_max_hz = hz;
        }
    }
    sweep_pass = sweep_pass_min_db >= kPassFloorDb && sweep_stop_max_db <= kStopCeilingDb;

    ordered_json f1;
    f1["table"] = {
        {"grid_hz", kResponseStepHz},
        {"dc_db", dc_db},
        {"pass_min_db", pass_min_db},
        {"pass_min_hz", pass_min_hz},
        {"stop_max_db", stop_max_db},
        {"stop_max_hz", stop_max_hz},
        {"pass", table_pass},
    };
    f1["sweep"] = {
        {"amplitude", kSweepAmplitude},
        {"seconds_per_tone", static_cast<double>(kSweepSamples) / kInputRateHz},
        {"dropped_outputs", kDroppedOutputs},
        {"measure", "sqrt(2) x RMS of the kept outputs, at whatever frequency the tone aliases to"},
        {"points", sweep_points},
        {"pass_min_db", sweep_pass_min_db},
        {"pass_min_hz", sweep_pass_min_hz},
        {"stop_max_db", sweep_stop_max_db},
        {"stop_max_hz", sweep_stop_max_hz},
        {"pass", sweep_pass},
    };
    return f1;
}

ordered_json ConfigJson(const voxr::FrontEnd::Configuration& config) {
    return {
        {"adaptive_digital_enabled", config.adaptive_digital_enabled},
        {"headroom_db", config.headroom_db},
        {"max_gain_db", config.max_gain_db},
        {"initial_gain_db", config.initial_gain_db},
        {"max_gain_change_db_per_second", config.max_gain_change_db_per_second},
        {"max_output_noise_level_dbfs", config.max_output_noise_level_dbfs},
        {"fixed_gain_db", config.fixed_gain_db},
    };
}

ordered_json MeasureF4(bool& pass) {
    const voxr::FrontEnd unit(kTargetDb);
    const voxr::FrontEnd::Configuration& config = unit.Config();
    pass = config.adaptive_digital_enabled && config.headroom_db == 18.0f && config.max_gain_db == 50.0f &&
           config.initial_gain_db == 15.0f && config.max_gain_change_db_per_second == 6.0f &&
           config.max_output_noise_level_dbfs == -50.0f && config.fixed_gain_db == 0.0f;
    ordered_json f4;
    f4["target_db"] = kTargetDb;
    f4["config"] = ConfigJson(config);
    f4["pass"] = pass;
    return f4;
}

ordered_json MeasureF5(bool& ramp_pass, bool& sines_pass, Meter& meter) {
    bool non_decreasing = true;
    bool saturated = true;
    int16_t previous = voxr::FloatS16ToInt16(-kF5RampSteps * kF5RampStep);
    for (int i = -kF5RampSteps; i <= kF5RampSteps; ++i) {
        const float v = i * kF5RampStep;
        const int16_t out = voxr::FloatS16ToInt16(v);
        if (out < previous) {
            non_decreasing = false;
        }
        if ((v >= 32767.0f && out != 32767) || (v <= -32768.0f && out != -32768)) {
            saturated = false;
        }
        previous = out;
    }
    ramp_pass = non_decreasing && saturated;

    ordered_json f5;
    f5["ramp"] = {
        {"from", -kF5RampSteps * kF5RampStep},
        {"to", kF5RampSteps * kF5RampStep},
        {"step", kF5RampStep},
        {"non_decreasing", non_decreasing},
        {"saturated", saturated},
        {"pass", ramp_pass},
    };

    sines_pass = true;
    ordered_json sines = ordered_json::array();
    for (double amplitude : {1.0, 4.0}) {
        voxr::FrontEnd unit(kTargetDb);
        std::vector<int16_t> output;
        std::vector<float> floats;
        RunFrames(unit, Sine(kF5SineSamples, kF5SineHz, amplitude), &output, meter, &floats);
        int16_t out_min = 0;
        int16_t out_max = 0;
        for (int16_t value : output) {
            out_min = std::min(out_min, value);
            out_max = std::max(out_max, value);
        }
        float float_min = 0.0f;
        float float_max = 0.0f;
        int checked = 0;
        int mismatches = 0;
        const size_t compared = std::min(floats.size(), output.size());
        for (size_t k = 0; k < floats.size(); ++k) {
            float_min = std::min(float_min, floats[k]);
            float_max = std::max(float_max, floats[k]);
            if (k >= compared || std::fabs(floats[k]) < kF5CheckLevel) {
                continue;
            }
            ++checked;
            if ((floats[k] > 0) != (output[k] > 0) || output[k] == 0) {
                ++mismatches;
            }
        }
        const bool pass = mismatches == 0 && checked > 0 && floats.size() == output.size();
        sines_pass = sines_pass && pass;
        sines.push_back({
            {"amplitude", amplitude},
            {"hz", kF5SineHz},
            {"output_samples", output.size()},
            {"output_min", out_min},
            {"output_max", out_max},
            {"float_min", float_min},
            {"float_max", float_max},
            {"samples_checked", checked},
            {"sign_mismatches", mismatches},
            {"pass", pass},
        });
    }
    f5["sines"] = sines;
    return f5;
}

ordered_json MeasureLead(const std::string& name, const std::vector<std::string>& files,
                         const std::vector<float>& lead, Meter& meter, bool& pass) {
    std::vector<FrameRecord> lead_frames;
    {
        voxr::FrontEnd unit(kTargetDb);
        lead_frames = RunFrames(unit, lead, nullptr, meter);
    }
    const int lead_frame_count = static_cast<int>(lead_frames.size());

    std::vector<int> confident;
    int run = 0;
    int longest_run = 0;
    for (int f = 0; f < lead_frame_count; ++f) {
        if (lead_frames[f].speech_probability >= kConfidentProbability) {
            confident.push_back(f);
            longest_run = std::max(longest_run, ++run);
        } else {
            run = 0;
        }
    }
    const bool valid = longest_run >= kConfidentRun;
    const size_t window_size = std::min<size_t>(kWindowFrames, confident.size());
    const std::vector<int> window(confident.end() - static_cast<std::ptrdiff_t>(window_size), confident.end());

    const double window_input_rms = RmsOver(lead_frames, window, &FrameRecord::pre_rms);
    std::vector<float> burst = WhiteNoise(kBurstSamples, 1.0, kBurstSeed);
    const double burst_scale = window_input_rms / DecimatedRms(burst);
    for (float& sample : burst) {
        sample = static_cast<float>(sample * burst_scale);
    }

    ordered_json result;
    result["name"] = name;
    result["files"] = files;
    result["lead_samples"] = lead.size();
    result["lead_frames"] = lead_frame_count;
    result["confident_frames"] = confident.size();
    result["longest_confident_run"] = longest_run;
    result["valid"] = valid;
    result["window_frames"] = window.size();
    result["window_first_frame"] = window.empty() ? -1 : window.front();
    result["window_last_frame"] = window.empty() ? -1 : window.back();
    result["window_input_rms_dbfs"] = Centi(Db(window_input_rms));
    result["burst_measure"] = "white noise scaled so its RMS after the decimator equals the window's pre_rms";

    pass = valid;
    ordered_json pauses = ordered_json::array();
    for (const bool noise_pause : {false, true}) {
        std::vector<float> composite = lead;
        const std::vector<float> pause = noise_pause
            ? WhiteNoise(kPauseSamples, std::pow(10.0, kNoiseFloorDbfs / 20.0), kPauseNoiseSeed)
            : std::vector<float>(kPauseSamples, 0.0f);
        composite.insert(composite.end(), pause.begin(), pause.end());
        const size_t burst_start = composite.size();
        composite.insert(composite.end(), burst.begin(), burst.end());
        const size_t burst_end = composite.size();

        voxr::FrontEnd unit(kTargetDb);
        const std::vector<FrameRecord> frames = RunFrames(unit, composite, nullptr, meter);

        std::vector<int> after_lead;
        std::vector<int> burst_frames;
        for (int f = lead_frame_count; f < static_cast<int>(frames.size()); ++f) {
            after_lead.push_back(f);
            const size_t first_input = static_cast<size_t>(f) * kCallSamples;
            if (first_input >= burst_start && first_input + kCallSamples <= burst_end) {
                burst_frames.push_back(f);
            }
        }

        const double window_max_gain = MaxGainOver(frames, window);
        const double after_max_gain = MaxGainOver(frames, after_lead);
        const double window_input_db = Centi(Db(RmsOver(frames, window, &FrameRecord::pre_rms)));
        const double window_output_db = Centi(Db(RmsOver(frames, window, &FrameRecord::post_rms)));
        const double burst_output_db = Centi(Db(RmsOver(frames, burst_frames, &FrameRecord::post_rms)));
        const double burst_input_db = Centi(Db(RmsOver(frames, burst_frames, &FrameRecord::pre_rms)));
        const double burst_lift_db = Centi(burst_output_db - burst_input_db);
        const double window_lift_db = Centi(window_output_db - window_input_db);
        const bool gain_bar = valid && after_max_gain <= window_max_gain;
        const bool rms_bar = valid && burst_lift_db <= window_lift_db;
        pass = pass && gain_bar && rms_bar;

        pauses.push_back({
            {"pause", noise_pause ? "noise_-60dbfs" : "digital_silence"},
            {"pause_seconds", static_cast<double>(kPauseSamples) / kInputRateHz},
            {"burst_seconds", static_cast<double>(kBurstSamples) / kInputRateHz},
            {"frames", frames.size()},
            {"burst_frames", burst_frames.size()},
            {"window_max_gain_db", window_max_gain},
            {"pause_and_burst_max_gain_db", after_max_gain},
            {"gain_bar", gain_bar},
            {"window_input_rms_dbfs", window_input_db},
            {"window_output_rms_dbfs", window_output_db},
            {"burst_input_rms_dbfs", burst_input_db},
            {"burst_output_rms_dbfs", burst_output_db},
            {"burst_lift_db", burst_lift_db},
            {"window_lift_db", window_lift_db},
            {"rms_bar", rms_bar},
        });
    }
    result["runs"] = pauses;
    result["pass"] = pass;
    return result;
}

ordered_json MeasureNoiseOnly(Meter& meter, bool& pass) {
    voxr::FrontEnd unit(kTargetDb);
    const std::vector<FrameRecord> frames = RunFrames(
        unit, WhiteNoise(kNoiseOnlySamples, std::pow(10.0, kNoiseFloorDbfs / 20.0), kNoiseOnlySeed), nullptr, meter);
    const double first = frames.empty() ? 0.0 : Centi(frames.front().gain_db);
    double max_gain = -INFINITY;
    double min_gain = INFINITY;
    int above_first = 0;
    for (const FrameRecord& frame : frames) {
        const double gain = Centi(frame.gain_db);
        max_gain = std::max(max_gain, gain);
        min_gain = std::min(min_gain, gain);
        if (gain > first) {
            ++above_first;
        }
    }
    pass = !frames.empty() && above_first == 0;
    return {
        {"seconds", static_cast<double>(kNoiseOnlySamples) / kInputRateHz},
        {"level_dbfs", kNoiseFloorDbfs},
        {"frames", frames.size()},
        {"first_gain_db", first},
        {"min_gain_db", min_gain},
        {"max_gain_db", max_gain},
        {"last_gain_db", frames.empty() ? 0.0 : Centi(frames.back().gain_db)},
        {"readings_above_first", above_first},
        {"pass", pass},
    };
}

double LeadOutputDb(const std::vector<float>& lead, float target_db, Meter& meter) {
    voxr::FrontEnd unit(target_db);
    const std::vector<FrameRecord> frames = RunFrames(unit, lead, nullptr, meter);
    std::vector<int> all(frames.size());
    for (size_t i = 0; i < frames.size(); ++i) {
        all[i] = static_cast<int>(i);
    }
    return Centi(Db(RmsOver(frames, all, &FrameRecord::post_rms)));
}

ordered_json MeasureF11(const std::vector<float>& lead_a, const std::vector<float>& lead_b, Meter& meter,
                        bool& pass) {
    const voxr::FrontEnd at_default(kTargetDb);
    const voxr::FrontEnd at_alt(kAltTargetDb);
    const float headroom_default = at_default.Config().headroom_db;
    const float headroom_alt = at_alt.Config().headroom_db;
    pass = headroom_default == 18.0f && headroom_alt == 12.0f;
    const double output_default = LeadOutputDb(lead_a, kTargetDb, meter);
    const double output_alt = LeadOutputDb(lead_a, kAltTargetDb, meter);
    const double lead_b_output_default = LeadOutputDb(lead_b, kTargetDb, meter);
    const double lead_b_output_alt = LeadOutputDb(lead_b, kAltTargetDb, meter);
    return {
        {"headroom_db_at_-18", headroom_default},
        {"headroom_db_at_-12", headroom_alt},
        {"lead_a_output_rms_dbfs_at_-18", output_default},
        {"lead_a_output_rms_dbfs_at_-12", output_alt},
        {"lead_a_output_difference_db", Centi(output_alt - output_default)},
        {"lead_b_output_rms_dbfs_at_-18", lead_b_output_default},
        {"lead_b_output_rms_dbfs_at_-12", lead_b_output_alt},
        {"lead_b_output_difference_db", Centi(lead_b_output_alt - lead_b_output_default)},
        {"pass", pass},
    };
}

ordered_json MeasureLatency(const std::vector<float>& signal, Meter& meter, bool& pass) {
    voxr::FrontEnd unit(kTargetDb);
    std::vector<int16_t> out(kOutputCapacity);
    const size_t count = std::min<size_t>(kLatencySamples, signal.size());
    int run = 0;
    int longest_run = 0;
    for (size_t i = 0; i < count; ++i) {
        const int written = TimedProcess(unit, signal.data() + i, 1, out.data(), kOutputCapacity, meter);
        if (written == 0) {
            longest_run = std::max(longest_run, ++run);
        } else {
            run = 0;
        }
    }
    const int filter_delay = (voxr::kDecimatorTapCount - 1) / 2;
    const int latency_samples = filter_delay + longest_run + 1;
    const double latency_ms = 1000.0 * latency_samples / kInputRateHz;
    pass = latency_ms <= kLatencyBarMs;
    return {
        {"one_sample_calls", count},
        {"filter_delay_samples", filter_delay},
        {"longest_emit_free_run", longest_run},
        {"latency_samples", latency_samples},
        {"latency_ms", latency_ms},
        {"bar_ms", kLatencyBarMs},
        {"pass", pass},
    };
}

bool LoadFixtures(const std::string& dir, const std::vector<std::string>& files, std::vector<float>& out,
                  std::string& error) {
    for (const std::string& file : files) {
        WavData wav;
        error = LoadWav(dir + "/" + file, wav);
        if (!error.empty()) {
            return false;
        }
        out.insert(out.end(), wav.samples.begin(), wav.samples.end());
    }
    return true;
}

int Fail(const std::string& message) {
    std::fprintf(stderr, "fe-probe: %s\n", message.c_str());
    return 2;
}

}

int main(int argc, char** argv) {
    std::string fixtures_dir;
    std::string out_path;
    for (int i = 1; i < argc; ++i) {
        const std::string arg = argv[i];
        if (arg == "--fixtures" && i + 1 < argc) {
            fixtures_dir = argv[++i];
        } else if (arg == "--out" && i + 1 < argc) {
            out_path = argv[++i];
        } else {
            return Fail("unknown or incomplete argument: " + arg + "\nusage: fe-probe --fixtures <dir> --out <file>");
        }
    }
    if (fixtures_dir.empty() || out_path.empty()) {
        return Fail("--fixtures and --out are both required\nusage: fe-probe --fixtures <dir> --out <file>");
    }

    const std::vector<std::string> lead_a_files = {
        "approach_target_alpha_one.wav", "switch_to_navigation.wav", "enable_all.wav", "resume_fire.wav"};
    const std::vector<std::string> lead_b_files(4, "cease_fire.wav");
    std::vector<float> lead_a;
    std::vector<float> lead_b;
    std::string error;
    if (!LoadFixtures(fixtures_dir, lead_a_files, lead_a, error) ||
        !LoadFixtures(fixtures_dir, lead_b_files, lead_b, error)) {
        return Fail(error);
    }

    Meter meter;
    bool f1_table_pass = false;
    bool f1_sweep_pass = false;
    bool f4_pass = false;
    bool f5_ramp_pass = false;
    bool f5_sines_pass = false;
    bool lead_a_pass = false;
    bool lead_b_pass = false;
    bool noise_only_pass = false;
    bool f11_pass = false;
    bool latency_pass = false;

    const ordered_json f1 = MeasureF1(f1_table_pass, f1_sweep_pass);
    const ordered_json f4 = MeasureF4(f4_pass);
    const ordered_json f5 = MeasureF5(f5_ramp_pass, f5_sines_pass, meter);
    ordered_json f6;
    f6["rounding_db"] = 0.01;
    f6["leads"] = {
        MeasureLead("A", lead_a_files, lead_a, meter, lead_a_pass),
        MeasureLead("B", lead_b_files, lead_b, meter, lead_b_pass),
    };
    f6["noise_only"] = MeasureNoiseOnly(meter, noise_only_pass);
    const ordered_json f11 = MeasureF11(lead_a, lead_b, meter, f11_pass);
    const ordered_json latency = MeasureLatency(lead_a, meter, latency_pass);

    const bool alloc_pass = meter.allocations == 0;
    double cost_sum = 0.0;
    double cost_max = 0.0;
    for (double us : meter.frame_us) {
        cost_sum += us;
        cost_max = std::max(cost_max, us);
    }
    const double cost_mean = meter.frame_us.empty() ? 0.0 : cost_sum / meter.frame_us.size();

    ordered_json report;
    report["config"] = {
        {"target_db", kTargetDb},
        {"design", voxr::kDecimatorDesign},
        {"tap_count", voxr::kDecimatorTapCount},
        {"cutoff_hz", voxr::kDecimatorCutoffHz},
        {"kaiser_beta", voxr::kDecimatorKaiserBeta},
        {"sample_rate_hz", voxr::kDecimatorSampleRateHz},
        {"frame_samples", voxr::FrontEnd::kFrameSamples},
        {"call_samples", kCallSamples},
        {"fixtures", fixtures_dir},
    };
    report["f1"] = f1;
    report["f4"] = f4;
    report["f5"] = f5;
    report["f6"] = f6;
    report["f11"] = f11;
    report["alloc"] = {
        {"process_calls", meter.process_calls},
        {"operator_new_calls_in_process", meter.allocations},
        {"pass", alloc_pass},
    };
    report["cost"] = {
        {"frames", meter.frame_us.size()},
        {"mean_us_per_frame", cost_mean},
        {"max_us_per_frame", cost_max},
    };
    report["latency"] = latency;

    const bool all = f1_table_pass && f1_sweep_pass && f4_pass && f5_ramp_pass && f5_sines_pass && lead_a_pass &&
                     lead_b_pass && noise_only_pass && f11_pass && alloc_pass && latency_pass;
    report["pass"] = {
        {"f1_table", f1_table_pass},
        {"f1_sweep", f1_sweep_pass},
        {"f4", f4_pass},
        {"f5_ramp", f5_ramp_pass},
        {"f5_sines", f5_sines_pass},
        {"f6_lead_a", lead_a_pass},
        {"f6_lead_b", lead_b_pass},
        {"f6_noise_only", noise_only_pass},
        {"f11", f11_pass},
        {"alloc", alloc_pass},
        {"latency", latency_pass},
        {"all", all},
    };

    const std::string text = report.dump(2);
    std::printf("%s\n", text.c_str());
    std::ofstream out(out_path, std::ios::binary);
    if (!out) {
        return Fail("cannot write report: " + out_path);
    }
    out << text << '\n';
    return all ? 0 : 1;
}
