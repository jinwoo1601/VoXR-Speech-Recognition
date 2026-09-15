// ============================================================================
// Purpose:  Data types for a game-supplied fill of a required slot the speaker omitted
// Layer:    Runtime.Commands
// Owns:     VoxrSlotResolution, VoxrResolvedSlot (public readonly structs)
// Depends:  (none)
// ============================================================================
namespace VoXR.Commands
{
    /// <summary>
    /// A game's answer to one question, asked while a command is being parsed: "this command
    /// would fire except for slot <c>X</c> -- do you know what <c>X</c> is?" It carries a value
    /// or nothing, plus a short reason a crew readback can quote ("main target", "only hostile").
    /// </summary>
    /// <remarks>
    /// The package remembers nothing between questions: a resolver is asked afresh on every
    /// utterance, so a resolution cannot go stale.
    /// The resolver producing one runs synchronously on the Unity main thread, inside the
    /// recognition callback, so it must be cheap -- a delegate that scans the world or blocks
    /// delays every recognised command. An exception thrown from a resolver propagates: the
    /// package neither swallows it nor substitutes <see cref="None"/>, so a bug in the game stays
    /// visible instead of reading as a command that quietly refused to fill.
    /// </remarks>
    public readonly struct VoxrSlotResolution
    {
        /// <summary>
        /// The value to fill the slot with. Null or empty means this is not a resolution -- the
        /// slot stays unfilled and the command is ruled incomplete exactly as it is today.
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Why the game chose this value ("main target", "only hostile"). Opaque to the package:
        /// it is carried through to <see cref="VoxrCommand.GetSlotResolutionReason"/> and the
        /// Editor diagnostics for crew readback and debugging, and is never parsed or validated.
        /// May be null.
        /// </summary>
        public readonly string Reason;

        /// <summary>
        /// True when this is a resolution at all. Derived from <see cref="Value"/> rather than
        /// stored, so <c>default(VoxrSlotResolution)</c> and a resolution carrying an empty value
        /// both mean "none" without a second way to say it.
        /// </summary>
        public bool HasValue => !string.IsNullOrEmpty(Value);

        public VoxrSlotResolution(string value, string reason)
        {
            Value = value;
            Reason = reason;
        }

        /// <summary>
        /// The "I do not know" answer. A resolver may equally return <c>default</c>.
        /// </summary>
        public static VoxrSlotResolution None => default;
    }

    /// <summary>
    /// The record that a slot was filled by a resolver rather than spoken, and the reason the
    /// resolver gave. The value is not repeated here -- it is in <see cref="VoxrCommand.Slots"/>,
    /// where a handler reads it without knowing or caring how it got there.
    /// </summary>
    public readonly struct VoxrResolvedSlot
    {
        public readonly string Name;

        public readonly string Reason;

        public VoxrResolvedSlot(string name, string reason)
        {
            Name = name;
            Reason = reason;
        }
    }
}
