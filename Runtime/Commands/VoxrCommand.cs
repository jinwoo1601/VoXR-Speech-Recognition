// ============================================================================
// Purpose:  Data types for parsed command results (slot match, command, command result)
// Layer:    Runtime.Commands
// Owns:     VoxrSlotMatch, VoxrCommand, VoxrCommandResult (public readonly structs)
// Depends:  (none)
// ============================================================================
using System;

namespace VoXR.Commands
{
    public readonly struct VoxrSlotMatch
    {
        public readonly string Name;

        /// <summary>
        /// The matched value. For a <see cref="VoxrSlotType.NumberSequence"/> slot this is the
        /// number words as spoken ("two seven zero"), never a numeric string ("270") -- convert
        /// with <see cref="VoxrNumberParser"/>. For an <see cref="VoxrSlotType.Enumerated"/> slot
        /// it is the canonical value, so a spoken alias arrives already resolved ("jackals"
        /// yields "jackal").
        /// </summary>
        public readonly string Value;

        public VoxrSlotMatch(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => $"{Name}={Value}";
    }

    public readonly struct VoxrCommand
    {
        public readonly string Intent;

        public readonly VoxrSlotMatch[] Slots;

        public readonly float Confidence;

        public readonly float Score;

        public readonly string RawText;

        public readonly int MatchedPatternIndex;

        // Append-last is an invariant, not an implementation detail: a resolver-filled slot is
        // always appended after the parser's matched slots in Slots, never interleaved, because
        // the Editor diagnostics index-match Slots[s] against the parser's per-slot word spans.
        // Interleaving would silently mislabel every diagnostic slot after the first resolved one,
        // and it is what lets the matched count be recovered as Slots.Length - ResolvedSlots.Length.
        /// <summary>
        /// The slots a registered resolver filled because the speaker omitted them, each with the
        /// reason the resolver gave. Empty for a command whose slots were all spoken.
        /// </summary>
        /// <remarks>
        /// The values live in <see cref="Slots"/> alongside the spoken ones, so a handler that
        /// does not care reads them through <see cref="GetSlot"/> and never learns the difference.
        /// </remarks>
        public readonly VoxrResolvedSlot[] ResolvedSlots;

        readonly string[] _registeredSlotNames;

        public VoxrCommand(string intent, VoxrSlotMatch[] slots, float confidence, float score,
            string rawText, string[] registeredSlotNames = null, int matchedPatternIndex = -1)
            : this(intent, slots, confidence, score, rawText, registeredSlotNames,
                matchedPatternIndex, null)
        { }

        // The all-fields constructor the copy-withs below route through. It is private rather than
        // an added optional parameter on the public one because ResolvedSlots is filled inside the
        // package only: keeping it off the public signature means no existing construction site
        // has to be read or edited to gain it.
        VoxrCommand(string intent, VoxrSlotMatch[] slots, float confidence, float score,
            string rawText, string[] registeredSlotNames, int matchedPatternIndex,
            VoxrResolvedSlot[] resolvedSlots)
        {
            Intent = intent;
            Slots = slots ?? Array.Empty<VoxrSlotMatch>();
            Confidence = confidence;
            Score = score;
            RawText = rawText;
            MatchedPatternIndex = matchedPatternIndex;
            _registeredSlotNames = registeredSlotNames;
            ResolvedSlots = resolvedSlots ?? Array.Empty<VoxrResolvedSlot>();
        }

        // A copy carrying a different score (issue #113), for re-arming a pending with a fill
        // whose re-score is not admissible. It lives here rather than at the call site because
        // _registeredSlotNames is private: a rebuild through the public constructor would
        // silently drop it, and GetSlot would stop distinguishing a registered-but-unmatched
        // slot from one the pattern never declared.
        internal VoxrCommand WithScore(float score)
        {
            return new VoxrCommand(Intent, Slots, Confidence, score, RawText,
                _registeredSlotNames, MatchedPatternIndex, ResolvedSlots);
        }

        // A copy whose slots include the resolver-filled ones, for the resolution pass. Same
        // reason as WithScore for living here, plus one of its own: Slots and ResolvedSlots have
        // to move together or the append-last invariant above is broken by the caller.
        internal VoxrCommand WithResolvedSlots(VoxrSlotMatch[] slots, VoxrResolvedSlot[] resolved)
        {
            return new VoxrCommand(Intent, slots, Confidence, Score, RawText,
                _registeredSlotNames, MatchedPatternIndex, resolved);
        }

        /// <summary>
        /// Returns the value of a named slot, or an empty string if the slot was not matched.
        /// </summary>
        /// <remarks>
        /// The value's shape depends on the slot type. For a
        /// <see cref="VoxrSlotType.NumberSequence"/> slot it is the number words as spoken --
        /// "orient heading two seven zero" yields <c>"two seven zero"</c>, not <c>"270"</c>, so
        /// <c>int.TryParse</c> on the result always fails silently. Convert with
        /// <see cref="VoxrNumberParser.ParseDigitSequence"/> for digit-by-digit utterances or
        /// <see cref="VoxrNumberParser.ParseCardinal"/> for cardinal phrases ("two hundred"); both
        /// throw <see cref="FormatException"/> on words they do not accept (null or empty returns
        /// <c>0</c>), so the canonical pattern is to try the digit path and fall back to the
        /// cardinal one. See the Command Recognition guide, "NumberSequence Slots", for the full
        /// snippet. For an <see cref="VoxrSlotType.Enumerated"/> slot the value is the canonical
        /// value, with any spoken alias already resolved.
        /// </remarks>
        public string GetSlot(string name)
        {
            int idx = FindSlotIndex(name);
            if (idx >= 0)
                return Slots[idx].Value;

#if DEBUG
            if (_registeredSlotNames != null)
            {
                bool registered = false;
                for (int i = 0; i < _registeredSlotNames.Length; i++)
                {
                    if (string.Equals(_registeredSlotNames[i], name, StringComparison.Ordinal))
                    {
                        registered = true;
                        break;
                    }
                }
                if (!registered)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[VoxrCommand] GetSlot(\"{name}\") called but no slot with that name is registered. " +
                        "Check for typos in the slot name.");
                }
            }
#endif

            return string.Empty;
        }

        /// <summary>
        /// Returns true if the named slot was matched in this command. Presence only -- see
        /// <see cref="GetSlot"/> for the value and the shape it takes.
        /// </summary>
        public bool HasSlot(string name) => FindSlotIndex(name) >= 0;

        /// <summary>
        /// Returns the reason a registered resolver gave for filling the named slot, or null if
        /// the slot was spoken, unfilled, or never declared.
        /// </summary>
        /// <remarks>
        /// A non-null return always means the slot was resolver-filled rather than spoken -- that
        /// is the one question this answers, and it is why there is no separate
        /// <c>IsSlotResolved</c>. A resolver may leave its reason unstated, in which case the
        /// return is <see cref="string.Empty"/>: still resolver-filled, just unexplained. Test the
        /// return against null, never against emptiness.
        /// </remarks>
        public string GetSlotResolutionReason(string name)
        {
            for (int i = 0; i < ResolvedSlots.Length; i++)
            {
                if (string.Equals(ResolvedSlots[i].Name, name, StringComparison.Ordinal))
                    return ResolvedSlots[i].Reason;
            }
            return null;
        }

        int FindSlotIndex(string name)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (string.Equals(Slots[i].Name, name, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        public override string ToString() => $"{Intent} ({Slots.Length} slots, score={Score:F2})";
    }

    public readonly struct VoxrCommandResult
    {
        public readonly bool IsMatch;

        public readonly VoxrCommand Command;

        public readonly string RawText;

        public VoxrCommandResult(VoxrCommand command)
        {
            IsMatch = true;
            Command = command;
            RawText = command.RawText;
        }

        public VoxrCommandResult(string rawText)
        {
            IsMatch = false;
            Command = default;
            RawText = rawText;
        }
    }
}
