// ============================================================================
// Purpose:  P/Invoke bindings for voxr-frontend.dll, the native 48→16 kHz filter and AGC2 front end (Windows Editor)
// Layer:    Runtime.Native (UNITY_EDITOR_WIN only)
// Owns:     FrontEndNative (internal static class), voxr_fe_stats (internal struct)
// Depends:  (none)
// ============================================================================
#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace VoXR.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct voxr_fe_stats
    {
        public ulong input_samples;
        public ulong output_samples;
        public ulong digest;
        public float gain_db;
        public float pre_rms;
        public float post_rms;
    }

    [Preserve]
    internal static class FrontEndNative
    {
        const string LibraryName = "voxr-frontend";

        internal const int AbiVersion = 1;
        internal const int DecimationFactor = 3;
        internal const int FrameSamples = 160;

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern int voxr_fe_abi_version();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern IntPtr voxr_fe_create(float targetDb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern int voxr_fe_process(
            IntPtr handle,
            [In] float[] in48k,
            int n,
            [Out] short[] out16k,
            int cap);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern void voxr_fe_begin_stream(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern void voxr_fe_reset(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern int voxr_fe_get_stats(IntPtr handle, out voxr_fe_stats stats);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)] [Preserve]
        internal static extern void voxr_fe_destroy(IntPtr handle);
    }
}
#endif
