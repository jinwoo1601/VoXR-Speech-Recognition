// ============================================================================
// Purpose:  Decimator member definitions: mirrored history writes and the tap-order dot product at kept samples
// Layer:    NativeBridge.Frontend
// Owns:     voxr::Decimator (class, member definitions)
// Depends:  kDecimatorTapCount, kDecimatorTaps
// ============================================================================

#include "decimator.h"

namespace voxr {

Decimator::Decimator() {
    Reset();
}

int Decimator::Process(const float* in, int n, float* out) {
    int written = 0;
    for (int i = 0; i < n; ++i) {
        history_[position_] = in[i];
        history_[position_ + kDecimatorTapCount] = in[i];
        position_ = position_ + 1 == kDecimatorTapCount ? 0 : position_ + 1;
        if (++phase_ < kFactor) {
            continue;
        }
        phase_ = 0;
        const float* window = history_ + position_;
        float acc = 0.0f;
        for (int k = 0; k < kDecimatorTapCount; ++k) {
            acc += kDecimatorTaps[k] * window[kDecimatorTapCount - 1 - k];
        }
        out[written++] = acc;
    }
    return written;
}

void Decimator::Reset() {
    for (float& sample : history_) {
        sample = 0.0f;
    }
    position_ = 0;
    phase_ = 0;
}

}
