// ============================================================================
// Purpose:  Static object running the AGC2 chain on one silent frame at load, so the arm64 link keeps the subset
// Layer:    Planning.AgcFilter.Probe
// Owns:     KeepAlive (internal struct)
// Depends:  ProbeChain, FrameReading
// ============================================================================

#include "agc2_chain.h"

namespace {

struct KeepAlive {
    KeepAlive() {
        ProbeChain chain(-18.0f);
        float frame[ProbeChain::kFrameSamples] = {};
        sink = chain.Process(frame).speech_probability;
    }

    volatile float sink = 0.0f;
};

KeepAlive keep_alive;

}
