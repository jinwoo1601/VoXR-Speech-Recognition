// ============================================================================
// Purpose:  C exports of the probe front-end DLL: create, process one 10 ms frame, destroy
// Layer:    Planning.AgcFilter.Probe
// Owns:     probe_fe_create, probe_fe_process, probe_fe_destroy (exported functions)
// Depends:  ProbeChain, FrameReading
// ============================================================================

#include "agc2_chain.h"

extern "C" {

__declspec(dllexport) void* __cdecl probe_fe_create(float target_db) {
    return new ProbeChain(target_db);
}

__declspec(dllexport) void __cdecl probe_fe_process(void* handle, float* frame, FrameReading* reading) {
    *reading = static_cast<ProbeChain*>(handle)->Process(frame);
}

__declspec(dllexport) void __cdecl probe_fe_destroy(void* handle) {
    delete static_cast<ProbeChain*>(handle);
}

}
