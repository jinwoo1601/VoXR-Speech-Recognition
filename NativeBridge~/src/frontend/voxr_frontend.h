// ============================================================================
// Purpose:  C ABI of the front-end unit: handle-based, blittable, caller-owned buffers
// Layer:    NativeBridge.Frontend
// Owns:     voxr_fe_stats (struct), voxr_fe_abi_version, voxr_fe_create, voxr_fe_process, voxr_fe_begin_stream, voxr_fe_reset, voxr_fe_get_stats, voxr_fe_destroy (exported functions)
// Depends:  (none)
// ============================================================================

#ifndef VOXR_FRONTEND_H
#define VOXR_FRONTEND_H

#include <stdint.h>

#if defined(_WIN32)
#if defined(voxr_frontend_EXPORTS)
#define VOXR_FRONTEND_EXPORT __declspec(dllexport)
#else
#define VOXR_FRONTEND_EXPORT __declspec(dllimport)
#endif
#define VOXR_FRONTEND_CALL __cdecl
#else
#define VOXR_FRONTEND_EXPORT __attribute__((visibility("default")))
#define VOXR_FRONTEND_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct voxr_fe_stats {
    uint64_t input_samples;
    uint64_t output_samples;
    uint64_t digest;
    float gain_db;
    float pre_rms;
    float post_rms;
} voxr_fe_stats;

VOXR_FRONTEND_EXPORT int32_t VOXR_FRONTEND_CALL voxr_fe_abi_version(void);
VOXR_FRONTEND_EXPORT void* VOXR_FRONTEND_CALL voxr_fe_create(float target_db);
VOXR_FRONTEND_EXPORT int32_t VOXR_FRONTEND_CALL voxr_fe_process(void* handle, const float* in48k, int32_t n,
                                                                int16_t* out16k, int32_t cap);
VOXR_FRONTEND_EXPORT void VOXR_FRONTEND_CALL voxr_fe_begin_stream(void* handle);
VOXR_FRONTEND_EXPORT void VOXR_FRONTEND_CALL voxr_fe_reset(void* handle);
VOXR_FRONTEND_EXPORT int32_t VOXR_FRONTEND_CALL voxr_fe_get_stats(void* handle, voxr_fe_stats* out_stats);
VOXR_FRONTEND_EXPORT void VOXR_FRONTEND_CALL voxr_fe_destroy(void* handle);

#ifdef __cplusplus
}
#endif

#endif
