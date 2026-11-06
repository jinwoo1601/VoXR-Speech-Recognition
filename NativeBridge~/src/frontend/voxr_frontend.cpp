// ============================================================================
// Purpose:  C ABI shim over voxr::FrontEnd: each export forwards to the handle's unit, no exception crosses create
// Layer:    NativeBridge.Frontend
// Owns:     voxr_fe_abi_version, voxr_fe_create, voxr_fe_process, voxr_fe_begin_stream, voxr_fe_reset, voxr_fe_get_stats, voxr_fe_destroy (exported function definitions)
// Depends:  voxr::FrontEnd, voxr_fe_stats
// ============================================================================

#include "voxr_frontend.h"

#include "front_end.h"

static_assert(sizeof(voxr_fe_stats) == 40, "voxr_fe_stats must be 40 bytes");

namespace {

constexpr int32_t kAbiVersion = 1;

voxr::FrontEnd* Unit(void* handle) {
    return static_cast<voxr::FrontEnd*>(handle);
}

}

extern "C" {

VOXR_FRONTEND_EXPORT int32_t VOXR_FRONTEND_CALL voxr_fe_abi_version(void) {
    return kAbiVersion;
}

VOXR_FRONTEND_EXPORT void* VOXR_FRONTEND_CALL voxr_fe_create(float target_db) {
    try {
        return new voxr::FrontEnd(target_db);
    } catch (...) {
        return nullptr;
    }
}

VOXR_FRONTEND_EXPORT int32_t VOXR_FRONTEND_CALL voxr_fe_process(void* handle, const float* in48k, int32_t n,
                                                                int16_t* out16k, int32_t cap) {
    if (handle == nullptr) {
        return -1;
    }
    return Unit(handle)->Process(in48k, n, out16k, cap);
}

VOXR_FRONTEND_EXPORT void VOXR_FRONTEND_CALL voxr_fe_begin_stream(void* handle) {
    if (handle != nullptr) {
        Unit(handle)->BeginStream();
    }
}

VOXR_FRONTEND_EXPORT void VOXR_FRONTEND_CALL voxr_fe_reset(void* handle) {
    if (handle != nullptr) {
        Unit(handle)->Reset();
    }
}

VOXR_FRONTEND_EXPORT int32_t VOXR_FRONTEND_CALL voxr_fe_get_stats(void* handle, voxr_fe_stats* out_stats) {
    if (handle == nullptr || out_stats == nullptr) {
        return -1;
    }
    const voxr::FrontEnd::Stats stats = Unit(handle)->GetStats();
    out_stats->input_samples = stats.input_samples;
    out_stats->output_samples = stats.output_samples;
    out_stats->digest = stats.digest;
    out_stats->gain_db = stats.gain_db;
    out_stats->pre_rms = stats.pre_rms;
    out_stats->post_rms = stats.post_rms;
    return 0;
}

VOXR_FRONTEND_EXPORT void VOXR_FRONTEND_CALL voxr_fe_destroy(void* handle) {
    delete Unit(handle);
}

}
