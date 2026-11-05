// ============================================================================
// Purpose:  AGC2 chain composed from upstream's classes in gain_controller2.cc's order, one 10 ms 16 kHz frame per call
// Layer:    Planning.AgcFilter.Probe
// Owns:     FrameReading (struct), ProbeChain (class)
// Depends:  webrtc::ApmDataDumper, webrtc::VoiceActivityDetectorWrapper, webrtc::NoiseLevelEstimator, webrtc::SpeechLevelEstimator, webrtc::SaturationProtector, webrtc::AdaptiveDigitalGainController, webrtc::GainApplier, webrtc::Limiter
// ============================================================================

#ifndef AGC2_PROBE_AGC2_CHAIN_H
#define AGC2_PROBE_AGC2_CHAIN_H

#include <memory>

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

struct FrameReading {
    float speech_probability;
    float gain_db;
    float pre_rms;
    float post_rms;
};

class ProbeChain {
public:
    static constexpr int kFrameSamples = 160;

    explicit ProbeChain(float target_db);
    ~ProbeChain();
    ProbeChain(const ProbeChain&) = delete;
    ProbeChain& operator=(const ProbeChain&) = delete;

    FrameReading Process(float* frame);

private:
    std::unique_ptr<webrtc::ApmDataDumper> data_dumper_;
    std::unique_ptr<webrtc::VoiceActivityDetectorWrapper> vad_;
    std::unique_ptr<webrtc::NoiseLevelEstimator> noise_level_estimator_;
    std::unique_ptr<webrtc::SpeechLevelEstimator> speech_level_estimator_;
    std::unique_ptr<webrtc::SaturationProtector> saturation_protector_;
    std::unique_ptr<webrtc::AdaptiveDigitalGainController> adaptive_digital_controller_;
    std::unique_ptr<webrtc::GainApplier> fixed_gain_applier_;
    std::unique_ptr<webrtc::Limiter> limiter_;
    float frame_s16_[kFrameSamples] = {};
    float last_gain_db_;
};

#endif
