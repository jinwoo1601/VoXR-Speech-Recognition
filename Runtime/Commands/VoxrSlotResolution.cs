// ============================================================================
// Purpose:  Data types for a game-supplied fill of a required slot the speaker omitted
// Layer:    Runtime.Commands
// Owns:     VoxrSlotResolutionRequest, VoxrSlotResolution, VoxrResolvedSlot
//           (public readonly structs)
// Depends:  (none)
// ============================================================================
namespace VoXR.Commands
{
    /// <summary>
    /// The question itself: which slot is unfilled, and which command is asking. Handed to a
    /// resolver so a game can answer one command and refuse another, rather than answering
    /// blind -- slot names are global, so <c>{track}</c> in a destructive command is the same
    /// slot name as <c>{track}</c> in a harmless one, and without this the resolver could not
    /// tell them apart.
    /// </summary>
    /// <remarks>
    /// Every field is read straight off the candidate command the recogniser already holds, so
    /// building a request computes nothing: this runs once per distinct question per utterance,
    /// on the Unity main thread, inside the recognition callback.
    /// <para>
    /// The three fields are also exactly the recogniser's per-utterance memo key, which is what
    /// keeps them honest: two asks that differ in any field are asked separately, so no field
    /// can be served stale from an answer given for a different question. That invariant is why
    /// the raw transcript is deliberately absent -- it would either widen the key for no gain
    /// (it is constant across one utterance's candidates) or, left out of the key, hand a
    /// resolver a field that can disagree with the answer it is about to receive. A resolver
    /// that wants the words rather than the slot is doing the parser's job.
    /// </para>
    /// </remarks>
    public readonly struct VoxrSlotResolutionRequest
    {
        /// <summary>
        /// The unfilled required slot being asked about, exactly as it is spelled in the
        /// pattern and in <see cref="VoxrSlotDefinition.Name"/>. Never null.
        /// </summary>
        public readonly string SlotName;

        /// <summary>
        /// The intent of the command that would fire if this slot were filled. The field to
        /// branch on when a resolver must serve one command and refuse another.
        /// </summary>
        public readonly string Intent;

        /// <summary>
        /// Which of that command's patterns matched, as an index into the command definition's
        /// pattern list -- the same value as <see cref="VoxrCommand.MatchedPatternIndex"/>.
        /// Always a real pattern index here: the recogniser refuses to resolve a command whose
        /// index it cannot read a pattern with, so the <c>-1</c> that field can carry elsewhere
        /// never reaches a resolver.
        /// </summary>
        public readonly int MatchedPatternIndex;

        public VoxrSlotResolutionRequest(string slotName, string intent, int matchedPatternIndex)
        {
            SlotName = slotName;
            Intent = intent;
            MatchedPatternIndex = matchedPatternIndex;
        }
    }

    /// <summary>
    /// A game's answer to one <see cref="VoxrSlotResolutionRequest"/>, asked while a command is
    /// being parsed: "this command would fire except for slot <c>X</c> -- do you know what
    /// <c>X</c> is?" It carries a value or nothing, plus a short reason a crew readback can
    /// quote ("main target", "only hostile").
    /// </summary>
    /// <remarks>
    /// The package remembers nothing between questions: a resolver is asked afresh on every
    /// utterance, so a resolution cannot go stale. Within one utterance an answer is reused only
    /// for a question identical in every field of its <see cref="VoxrSlotResolutionRequest"/>.
    /// The resolver producing one runs synchronously on the Unity main thread, inside the
    /// recognition callback, so it must be cheap -- a delegate that scans the world or blocks
    /// delays every recognised command. It must also not RE-ENTER: calling <c>InjectText</c>,
    /// <c>Configure</c> or <c>SetActiveSets</c> from inside a resolver restarts the very parse
    /// that is asking the question and clears the per-utterance state the outer resolution is
    /// still using. <c>CancelPendingCommand()</c> is the one such call that is safe. See
    /// <c>VoxrCommandRecogniser.RegisterSlotResolver</c> for the full rule.
    /// <para>
    /// An exception thrown from a resolver propagates: the package neither swallows it nor
    /// substitutes <see cref="None"/>, so a bug in the game stays visible instead of reading as
    /// a command that quietly refused to fill.
    /// </para>
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
