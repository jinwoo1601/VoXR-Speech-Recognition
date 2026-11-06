// ============================================================================
// Purpose:  Front-end unit: 48 kHz input decimated to 16 kHz, buffered into 10 ms frames, levelled by the AGC2 chain and emitted as int16
// Layer:    NativeBridge.Frontend
// Owns:     voxr::FrontEnd (class), voxr::FrontEnd::Configuration (struct), voxr::FrontEnd::Stats (struct), voxr::FloatS16ToInt16 (inline function)
// Depends:  voxr::Decimator, webrtc::ApmDataDumper, webrtc::VoiceActivityDetectorWrapper, webrtc::NoiseLevelEstimator, webrtc::SpeechLevelEstimator, webrtc::SaturationProtector, webrtc::AdaptiveDigitalGainController, webrtc::GainApplier, webrtc::Limiter
// ============================================================================

#ifndef VOXR_FRONTEND_FRONT_END_H
#define VOXR_FRONTEND_FRONT_END_H

#include <cmath>
#include <cstdint>
#include <memory>

#include "decimator.h"

namespace webrtc {
class ApmDataDumper;
class VoiceActivityDetectorWrapper;
class NoiseLevelEstimator;
class SpeechLevelEstimator;
class SaturationProtector;
class AdaptiveDigitalGainController;
class GainApplier;
class Limiter;
}

namespace voxr {

inline int16_t FloatS16ToInt16(float v) {
    if (std::isnan(v)) {
        return 0;
    }
    if (v >= 32767.0f) {
        return 32767;
    }
    if (v <= -32768.0f) {
        return -32768;
    }
    return static_cast<int16_t>(std::round(v));
}

class FrontEnd {
public:
    static constexpr int kFrameSamples = 160;

    struct Configuration {
        bool adaptive_digital_enabled;
        float headroom_db;
        float max_gain_db;
        float initial_gain_db;
        float max_gain_change_db_per_second;
        float max_output_noise_level_dbfs;
        float fixed_gain_db;
    };

    struct Stats {
        uint64_t input_samples;
        uint64_t output_samples;
        uint64_t digest;
        float gain_db;
        float pre_rms;
        float post_rms;
    };

    explicit FrontEnd(float target_db);
    ~FrontEnd();
    FrontEnd(const FrontEnd&) = delete;
    FrontEnd& operator=(const FrontEnd&) = delete;

    int Process(const float* in48k, int n, int16_t* out16k, int cap, float* out_float_s16 = nullptr);
    void BeginStream();
    void Reset();

    Stats GetStats() const;
    float SpeechProbability() const;
    const Configuration& Config() const;

private:
    void BuildStatefulStages();
    void ClearStream();
    void ProcessFrame(int16_t* out, float* out_float_s16);

    Configuration config_;
    Decimator decimator_;
    std::unique_ptr<webrtc::ApmDataDumper> data_dumper_;
    std::unique_ptr<webrtc::VoiceActivityDetectorWrapper> vad_;
    std::unique_ptr<webrtc::NoiseLevelEstimator> noise_level_estimator_;
    std::unique_ptr<webrtc::SpeechLevelEstimator> speech_level_estimator_;
    std::unique_ptr<webrtc::SaturationProtector> saturation_protector_;
    std::unique_ptr<webrtc::AdaptiveDigitalGainController> adaptive_digital_controller_;
    std::unique_ptr<webrtc::GainApplier> fixed_gain_applier_;
    std::unique_ptr<webrtc::Limiter> limiter_;
    float decimated_[kFrameSamples];
    float frame_[kFrameSamples];
    int frame_fill_;
    Stats stats_;
    float speech_probability_;
};

}

#endif
