// ============================================================================
// Purpose:  FrontEnd member definitions: frame buffering, the AGC2 chain per 10 ms frame in gain_controller2.cc's order, int16 output, stats and digest
// Layer:    NativeBridge.Frontend
// Owns:     voxr::FrontEnd (class, member definitions)
// Depends:  voxr::Decimator, voxr::FloatS16ToInt16, webrtc::ApmDataDumper, webrtc::VoiceActivityDetectorWrapper, webrtc::NoiseLevelEstimator, webrtc::SpeechLevelEstimator, webrtc::SaturationProtector, webrtc::AdaptiveDigitalGainController, webrtc::GainApplier, webrtc::Limiter, webrtc::AvailableCpuFeatures, webrtc::DeinterleavedView
// ============================================================================

#include "front_end.h"

#include <algorithm>
#include <cmath>

#include "api/audio/audio_frame.h"
#include "api/audio/audio_processing.h"
#include "api/audio/audio_view.h"
#include "common_audio/include/audio_util.h"
#include "modules/audio_processing/agc2/adaptive_digital_gain_controller.h"
#include "modules/audio_processing/agc2/agc2_common.h"
#include "modules/audio_processing/agc2/cpu_features.h"
#include "modules/audio_processing/agc2/gain_applier.h"
#include "modules/audio_processing/agc2/limiter.h"
#include "modules/audio_processing/agc2/noise_level_estimator.h"
#include "modules/audio_processing/agc2/saturation_protector.h"
#include "modules/audio_processing/agc2/speech_level_estimator.h"
#include "modules/audio_processing/agc2/vad_wrapper.h"
#include "modules/audio_processing/logging/apm_data_dumper.h"
#include "rtc_base/system/arch.h"

namespace voxr {
namespace {

using AdaptiveDigitalConfig = webrtc::AudioProcessing::Config::GainController2::AdaptiveDigital;

constexpr int kSampleRateHz = 16000;
constexpr float kMaxGainDb = 50.0f;
constexpr float kInitialGainDb = 15.0f;
constexpr float kMaxGainChangeDbPerSecond = 6.0f;
constexpr float kMaxOutputNoiseLevelDbfs = -50.0f;
constexpr float kFixedGainDb = 0.0f;
constexpr float kDefaultTargetDb = -18.0f;
constexpr uint64_t kFnvOffsetBasis = 14695981039346656037ull;
constexpr uint64_t kFnvPrime = 1099511628211ull;

FrontEnd::Configuration MakeConfiguration(float target_db) {
    if (!std::isfinite(target_db)) {
        target_db = kDefaultTargetDb;
    }
    FrontEnd::Configuration config;
    config.adaptive_digital_enabled = true;
    config.headroom_db = -std::min(target_db, 0.0f);
    config.max_gain_db = kMaxGainDb;
    config.initial_gain_db = kInitialGainDb;
    config.max_gain_change_db_per_second = kMaxGainChangeDbPerSecond;
    config.max_output_noise_level_dbfs = kMaxOutputNoiseLevelDbfs;
    config.fixed_gain_db = kFixedGainDb;
    return config;
}

AdaptiveDigitalConfig MakeAdaptiveDigitalConfig(const FrontEnd::Configuration& configuration) {
    AdaptiveDigitalConfig config;
    config.enabled = configuration.adaptive_digital_enabled;
    config.headroom_db = configuration.headroom_db;
    config.max_gain_db = configuration.max_gain_db;
    config.initial_gain_db = configuration.initial_gain_db;
    config.max_gain_change_db_per_second = configuration.max_gain_change_db_per_second;
    config.max_output_noise_level_dbfs = configuration.max_output_noise_level_dbfs;
    return config;
}

webrtc::AvailableCpuFeatures ChainCpuFeatures() {
#if defined(WEBRTC_ARCH_X86_FAMILY)
    return webrtc::AvailableCpuFeatures(true, false, false);
#else
    return webrtc::GetAvailableCpuFeatures();
#endif
}

float Rms(const float* samples, int count) {
    float energy = 0.0f;
    for (int i = 0; i < count; ++i) {
        energy += samples[i] * samples[i];
    }
    return std::sqrt(energy / count);
}

uint64_t FoldSample(uint64_t digest, int16_t sample) {
    const uint16_t bits = static_cast<uint16_t>(sample);
    digest = (digest ^ static_cast<uint64_t>(bits & 0xFFu)) * kFnvPrime;
    digest = (digest ^ static_cast<uint64_t>(bits >> 8)) * kFnvPrime;
    return digest;
}

}

FrontEnd::FrontEnd(float target_db)
    : config_(MakeConfiguration(target_db)),
      data_dumper_(std::make_unique<webrtc::ApmDataDumper>(1)),
      frame_fill_(0),
      stats_(),
      speech_probability_(0.0f) {
    const AdaptiveDigitalConfig config = MakeAdaptiveDigitalConfig(config_);
    BuildStatefulStages();
    speech_level_estimator_ = std::make_unique<webrtc::SpeechLevelEstimator>(
        data_dumper_.get(), config, webrtc::kAdjacentSpeechFramesThreshold);
    saturation_protector_ = webrtc::CreateSaturationProtector(
        webrtc::kSaturationProtectorInitialHeadroomDb, webrtc::kAdjacentSpeechFramesThreshold,
        data_dumper_.get());
    fixed_gain_applier_ = std::make_unique<webrtc::GainApplier>(
        false, webrtc::DbToRatio(config_.fixed_gain_db));
    limiter_ = std::make_unique<webrtc::Limiter>(
        data_dumper_.get(), webrtc::SampleRateToDefaultChannelSize(kSampleRateHz), "Agc2");
    ClearStream();
    stats_.gain_db = config_.initial_gain_db;
}

FrontEnd::~FrontEnd() = default;

int FrontEnd::Process(const float* in48k, int n, int16_t* out16k, int cap, float* out_float_s16) {
    if (in48k == nullptr || out16k == nullptr || n < 0) {
        return -1;
    }
    const int64_t pending = static_cast<int64_t>(decimator_.Phase()) + n;
    const int64_t frames = (frame_fill_ + pending / Decimator::kFactor) / kFrameSamples;
    if (cap < frames * kFrameSamples) {
        return -1;
    }

    int written = 0;
    int consumed = 0;
    while (consumed < n) {
        const int chunk = std::min(n - consumed, kFrameSamples * Decimator::kFactor);
        const int produced = decimator_.Process(in48k + consumed, chunk, decimated_);
        consumed += chunk;
        for (int i = 0; i < produced; ++i) {
            frame_[frame_fill_++] = decimated_[i];
            if (frame_fill_ == kFrameSamples) {
                ProcessFrame(out16k + written, out_float_s16 == nullptr ? nullptr : out_float_s16 + written);
                written += kFrameSamples;
                frame_fill_ = 0;
            }
        }
    }
    stats_.input_samples += static_cast<uint64_t>(n);
    return written;
}

void FrontEnd::BeginStream() {
    ClearStream();
}

void FrontEnd::Reset() {
    BuildStatefulStages();
    speech_level_estimator_->Reset();
    saturation_protector_->Reset();
    limiter_->Reset();
    ClearStream();
    stats_.gain_db = config_.initial_gain_db;
    speech_probability_ = 0.0f;
}

FrontEnd::Stats FrontEnd::GetStats() const {
    return stats_;
}

float FrontEnd::SpeechProbability() const {
    return speech_probability_;
}

const FrontEnd::Configuration& FrontEnd::Config() const {
    return config_;
}

void FrontEnd::BuildStatefulStages() {
    const AdaptiveDigitalConfig config = MakeAdaptiveDigitalConfig(config_);
    vad_ = std::make_unique<webrtc::VoiceActivityDetectorWrapper>(
        webrtc::kVadResetPeriodMs, ChainCpuFeatures(), kSampleRateHz);
    noise_level_estimator_ = webrtc::CreateNoiseFloorEstimator(data_dumper_.get());
    adaptive_digital_controller_ = std::make_unique<webrtc::AdaptiveDigitalGainController>(
        data_dumper_.get(), config, webrtc::kAdjacentSpeechFramesThreshold);
}

void FrontEnd::ClearStream() {
    decimator_.Reset();
    frame_fill_ = 0;
    stats_.input_samples = 0;
    stats_.output_samples = 0;
    stats_.digest = kFnvOffsetBasis;
    stats_.pre_rms = 0.0f;
    stats_.post_rms = 0.0f;
}

void FrontEnd::ProcessFrame(int16_t* out, float* out_float_s16) {
    stats_.pre_rms = Rms(frame_, kFrameSamples);

    for (int i = 0; i < kFrameSamples; ++i) {
        frame_[i] = webrtc::FloatToFloatS16(frame_[i]);
    }
    webrtc::DeinterleavedView<float> view(frame_, kFrameSamples, 1);

    speech_probability_ = vad_->Analyze(view);

    float peak = 0.0f;
    float energy_before = 0.0f;
    for (int i = 0; i < kFrameSamples; ++i) {
        peak = std::max(std::fabs(frame_[i]), peak);
        energy_before += frame_[i] * frame_[i];
    }
    const float peak_dbfs = webrtc::FloatS16ToDbfs(peak);
    const float rms_dbfs = webrtc::FloatS16ToDbfs(std::sqrt(energy_before / kFrameSamples));

    const float noise_rms_dbfs = noise_level_estimator_->Analyze(view);
    speech_level_estimator_->Update(rms_dbfs, peak_dbfs, speech_probability_);
    const float speech_level_dbfs = speech_level_estimator_->level_dbfs();
    saturation_protector_->Analyze(speech_probability_, peak_dbfs, speech_level_dbfs);

    webrtc::AdaptiveDigitalGainController::FrameInfo info;
    info.speech_probability = speech_probability_;
    info.speech_level_dbfs = speech_level_dbfs;
    info.speech_level_reliable = speech_level_estimator_->is_confident();
    info.noise_rms_dbfs = noise_rms_dbfs;
    info.headroom_db = saturation_protector_->HeadroomDb();
    info.limiter_envelope_dbfs = webrtc::FloatS16ToDbfs(limiter_->LastAudioLevel());
    adaptive_digital_controller_->Process(info, view);

    float energy_after = 0.0f;
    for (int i = 0; i < kFrameSamples; ++i) {
        energy_after += frame_[i] * frame_[i];
    }
    if (energy_before > 0.0f) {
        stats_.gain_db = 10.0f * std::log10(energy_after / energy_before);
    }

    fixed_gain_applier_->ApplyGain(view);
    limiter_->Process(view);

    float energy = 0.0f;
    uint64_t digest = stats_.digest;
    for (int i = 0; i < kFrameSamples; ++i) {
        const float sample = webrtc::FloatS16ToFloat(frame_[i]);
        energy += sample * sample;
        const int16_t value = FloatS16ToInt16(frame_[i]);
        out[i] = value;
        if (out_float_s16 != nullptr) {
            out_float_s16[i] = frame_[i];
        }
        digest = FoldSample(digest, value);
    }
    stats_.post_rms = std::sqrt(energy / kFrameSamples);
    stats_.digest = digest;
    stats_.output_samples += kFrameSamples;
}

}
