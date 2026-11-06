// ============================================================================
// Purpose:  48 to 16 kHz decimator: the committed low-pass table evaluated at every third input over a mirrored history
// Layer:    NativeBridge.Frontend
// Owns:     voxr::Decimator (class)
// Depends:  kDecimatorTapCount, kDecimatorTaps
// ============================================================================

#ifndef VOXR_FRONTEND_DECIMATOR_H
#define VOXR_FRONTEND_DECIMATOR_H

#include "decimator_taps.h"

namespace voxr {

class Decimator {
public:
    static constexpr int kFactor = 3;

    Decimator();

    int Process(const float* in, int n, float* out);
    void Reset();

private:
    float history_[2 * kDecimatorTapCount];
    int position_;
    int phase_;
};

}

#endif
