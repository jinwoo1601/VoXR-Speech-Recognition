// ============================================================================
// Purpose:  AGC2 chain per frame: voice detector, noise and speech level estimators, saturation protector, adaptive digital controller, fixed gain, limiter
// Layer:    Planning.AgcFilter.Probe
// Owns:     ProbeChain (class, member definitions)
// Depends:  FrameReading, webrtc::ApmDataDumper, webrtc::VoiceActivityDetectorWrapper, webrtc::NoiseLevelEstimator, webrtc::SpeechLevelEstimator, webrtc::SaturationProtector, webrtc::AdaptiveDigitalGainController, webrtc::GainApplier, webrtc::Limiter, webrtc::AvailableCpuFeatures, webrtc::DeinterleavedView
// ============================================================================

#include "agc2_chain.h"

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

namespace {

using AdaptiveDigitalConfig = webrtc::AudioProcessing::Config::GainController2::AdaptiveDigital;

constexpr int kSampleRateHz = 16000;
constexpr float kMaxGainDb = 50.0f;
constexpr float kInitialGainDb = 15.0f;
constexpr float kMaxGainChangeDbPerSecond = 6.0f;
constexpr float kMaxOutputNoiseLevelDbfs = -50.0f;
constexpr float kFixedGainDb = 0.0f;

AdaptiveDigitalConfig MakeConfig(float target_db) {
    AdaptiveDigitalConfig config;
    config.enabled = true;
    config.headroom_db = -target_db;
    config.max_gain_db = kMaxGainDb;
    config.initial_gain_db = kInitialGainDb;
    config.max_gain_change_db_per_second = kMaxGainChangeDbPerSecond;
    config.max_output_noise_level_dbfs = kMaxOutputNoiseLevelDbfs;
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

}

ProbeChain::ProbeChain(float target_db)
    : data_dumper_(std::make_unique<webrtc::ApmDataDumper>(1)),
      last_gain_db_(kInitialGainDb) {
    const AdaptiveDigitalConfig config = MakeConfig(target_db);
    vad_ = std::make_unique<webrtc::VoiceActivityDetectorWrapper>(
        webrtc::kVadResetPeriodMs, ChainCpuFeatures(), kSampleRateHz);
    noise_level_estimator_ = webrtc::CreateNoiseFloorEstimator(data_dumper_.get());
    speech_level_estimator_ = std::make_unique<webrtc::SpeechLevelEstimator>(
        data_dumper_.get(), config, webrtc::kAdjacentSpeechFramesThreshold);
    saturation_protector_ = webrtc::CreateSaturationProtector(
        webrtc::kSaturationProtectorInitialHeadroomDb, webrtc::kAdjacentSpeechFramesThreshold,
        data_dumper_.get());
    adaptive_digital_controller_ = std::make_unique<webrtc::AdaptiveDigitalGainController>(
        data_dumper_.get(), config, webrtc::kAdjacentSpeechFramesThreshold);
    fixed_gain_applier_ = std::make_unique<webrtc::GainApplier>(
        false, webrtc::DbToRatio(kFixedGainDb));
    limiter_ = std::make_unique<webrtc::Limiter>(
        data_dumper_.get(), webrtc::SampleRateToDefaultChannelSize(kSampleRateHz), "Agc2");
}

ProbeChain::~ProbeChain() = default;

FrameReading ProbeChain::Process(float* frame) {
    FrameReading reading;
    reading.pre_rms = Rms(frame, kFrameSamples);

    for (int i = 0; i < kFrameSamples; ++i) {
        frame_s16_[i] = webrtc::FloatToFloatS16(frame[i]);
    }
    webrtc::DeinterleavedView<float> view(frame_s16_, kFrameSamples, 1);

    reading.speech_probability = vad_->Analyze(view);

    float peak = 0.0f;
    float energy_before = 0.0f;
    for (int i = 0; i < kFrameSamples; ++i) {
        peak = std::max(std::fabs(frame_s16_[i]), peak);
        energy_before += frame_s16_[i] * frame_s16_[i];
    }
    const float peak_dbfs = webrtc::FloatS16ToDbfs(peak);
    const float rms_dbfs = webrtc::FloatS16ToDbfs(std::sqrt(energy_before / kFrameSamples));

    const float noise_rms_dbfs = noise_level_estimator_->Analyze(view);
    speech_level_estimator_->Update(rms_dbfs, peak_dbfs, reading.speech_probability);
    const float speech_level_dbfs = speech_level_estimator_->level_dbfs();
    saturation_protector_->Analyze(reading.speech_probability, peak_dbfs, speech_level_dbfs);

    webrtc::AdaptiveDigitalGainController::FrameInfo info;
    info.speech_probability = reading.speech_probability;
    info.speech_level_dbfs = speech_level_dbfs;
    info.speech_level_reliable = speech_level_estimator_->is_confident();
    info.noise_rms_dbfs = noise_rms_dbfs;
    info.headroom_db = saturation_protector_->HeadroomDb();
    info.limiter_envelope_dbfs = webrtc::FloatS16ToDbfs(limiter_->LastAudioLevel());
    adaptive_digital_controller_->Process(info, view);

    float energy_after = 0.0f;
    for (int i = 0; i < kFrameSamples; ++i) {
        energy_after += frame_s16_[i] * frame_s16_[i];
    }
    if (energy_before > 0.0f) {
        last_gain_db_ = 10.0f * std::log10(energy_after / energy_before);
    }
    reading.gain_db = last_gain_db_;

    fixed_gain_applier_->ApplyGain(view);
    limiter_->Process(view);

    for (int i = 0; i < kFrameSamples; ++i) {
        frame[i] = webrtc::FloatS16ToFloat(frame_s16_[i]);
    }
    reading.post_rms = Rms(frame, kFrameSamples);
    return reading;
}
