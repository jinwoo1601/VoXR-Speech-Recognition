// ============================================================================
// Purpose:  Enum choosing the endpointer preset applied to the model at load
// Layer:    Runtime
// Owns:     VoxrEndpointerProfile (public enum)
// Depends:  (none)
// ============================================================================
namespace VoXR
{
    // Serialized as the integer: members are appended, never renumbered.
    public enum VoxrEndpointerProfile
    {
        Automatic = 0,
        ModelDefault = 1,
        Responsive = 2,
    }
}
