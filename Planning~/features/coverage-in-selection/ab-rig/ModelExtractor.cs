// ============================================================================
// Purpose:  Stand-in for the shipped model extractor: hands back the model path as a completed task
// Layer:    Planning.AbRig
// Owns:     ModelExtractor (internal static class)
// Depends:  VoxrBridgeErrorCode
// ============================================================================
// Recogniser stage only (stage.sh's `recogniser` argument), in place of Runtime/ModelExtractor.cs,
// under its shipped name with the one member the staged speech recogniser calls
// (feat-joined-rig-recogniser, *Staging*). A completed task keeps InitialiseAsync's continuation
// on the rig's main thread; the shipped extractor yields, and with no SynchronizationContext
// would resume off it (plan-1 claim a).
using System;
using System.Threading.Tasks;

namespace VoXR
{
    internal static class ModelExtractor
    {
        internal static Task<string> ExtractModelAsync(
            string modelRelativePath,
            Action<VoxrBridgeErrorCode, string> onError
        ) => Task.FromResult(modelRelativePath);
    }
}
