// ============================================================================
// Purpose:  Per-case scoring for the joined mode: each final parsed alone, the fire gate, the fire, outcome class and slot credit
// Layer:    Planning.AbRig
// Owns:     JoinedScorer (internal static class), JoinedScorer.Outcome (internal sealed class)
// Depends:  JoinedInput, VoxrCommandParser, VoxrCommandDefinition, VoxrCommand, VoxrCommandResult, VoxrSlotMatch, VoxrWord
// ============================================================================
// feat-joined-rig-parser Phase 3 (*Fire gate*, *The fire*, *Slot matching*, *Two or more
// finals*). Each final of a case goes to Parse(text, words) alone; every result passing the
// gate joins one pool, first final first and each final's results in Parse's order (utterance
// order, ruled 2026-10-14). The first pooled result is the fire and decides the class; the rest
// are counted as extra fires, never a class, never netted.
using System;
using System.Collections.Generic;
using VoXR;
using VoXR.Commands;

namespace AbRig
{
    static class JoinedScorer
    {
        // The recogniser's serialized defaults (minScore :31, minConfidence :27): the same for
        // both sides, printed in every scored header; provisional (*Tuning record*).
        internal const float MinScore = 0.6f;
        internal const float MinConfidence = 0.4f;

        // Every class a scored case can take.
        internal static readonly string[] Classes =
        {
            "correct-fire",
            "correct-no-fire",
            "wrong-slots",
            "wrong-intent",
            "missed",
            "false-fire",
        };

        internal sealed class Outcome
        {
            internal string Class;
            internal bool HasPrimary;
            internal VoxrCommand Primary; // the fire; default when !HasPrimary
            internal double? SlotCredit; // matched / expected; null = "n/a" (none expected)
            internal int ExtraFires;
            internal int FinalCount;

            // The recogniser layer's (feat-joined-rig-recogniser, *Timing measures*, *Trace*):
            // each fire's frame time in order; the first's minus the end of audio, null without
            // a fire; a pending never confirmed; the grammar the staged recogniser applied; the
            // case's trace lines, each LF-ended. Unset at the parser layer.
            internal double[] FireTimes;
            internal double? Delay;
            internal bool Pending;
            internal string AppliedGrammar;
            internal string Trace;
        }

        internal static Outcome Score(
            VoxrCommandParser parser,
            VoxrCommandDefinition[] commands,
            JoinedInput.ManifestCase expected,
            JoinedInput.DumpCase dumped
        )
        {
            // 1-2. Each final alone; the gate-passing results pooled in final, then array, order.
            var pool = new List<VoxrCommand>();
            for (int f = 0; f < dumped.Finals.Count; f++)
            {
                VoxrCommandResult[] results = parser.Parse(
                    dumped.Finals[f],
                    ToVoxrWords(dumped.Words[f])
                );
                foreach (VoxrCommandResult r in results)
                    if (Fires(r.Command, commands))
                        pool.Add(r.Command);
            }

            // 3. The fire, the extra fires, the finals.
            var o = new Outcome
            {
                HasPrimary = pool.Count > 0,
                ExtraFires = Math.Max(0, pool.Count - 1),
                FinalCount = dumped.Finals.Count,
            };
            if (o.HasPrimary)
                o.Primary = pool[0];

            o.Class = Classify(o.HasPrimary, o.Primary, expected, out o.SlotCredit);
            return o;
        }

        // Steps 4-5, shared by both layers (feat-joined-rig-recogniser, *Recogniser-layer
        // scoring*): the class of the fire against the expectation, and the slot credit.
        internal static string Classify(
            bool hasPrimary,
            VoxrCommand primary,
            JoinedInput.ManifestCase expected,
            out double? slotCredit
        )
        {
            // 4. The class.
            string cls;
            KeyValuePair<string, string>[] slots = expected.ExpectedSlots;
            bool extraSlot = false;
            int matched = hasPrimary ? Matched(primary, slots, out extraSlot) : 0;
            if (expected.ExpectedIntent.Length == 0)
                cls = hasPrimary ? "false-fire" : "correct-no-fire";
            else if (!hasPrimary)
                cls = "missed";
            else if (
                !string.Equals(primary.Intent, expected.ExpectedIntent, StringComparison.Ordinal)
            )
                cls = "wrong-intent";
            else
                cls = matched == slots.Length && !extraSlot ? "correct-fire" : "wrong-slots";

            // 5. Slot credit: "n/a" whenever none is expected, whatever the fire; no weight.
            slotCredit = slots.Length == 0 ? (double?)null : (double)matched / slots.Length;
            return cls;
        }

        // *Fire gate*: the recogniser's flush gate (VoxrCommandRecogniser :1027-1029) minus
        // resolution — score, confidence (negative = none reported), and complete: no unfilled
        // required slot (VoxrCommandParser.HasUnfilledRequiredSlot) under the first definition of
        // the intent, ordinal; an intent with no definition counts as complete.
        static bool Fires(VoxrCommand cmd, VoxrCommandDefinition[] commands)
        {
            if (cmd.Score >= MinScore && (cmd.Confidence < 0f || cmd.Confidence >= MinConfidence))
            {
                foreach (VoxrCommandDefinition def in commands)
                    if (string.Equals(def.Intent, cmd.Intent, StringComparison.Ordinal))
                        return !VoxrCommandParser.HasUnfilledRequiredSlot(cmd, def);
                return true;
            }
            return false;
        }

        // *Slot matching*: an expected {name, value} matches an actual slot of that name with an
        // ordinally equal value, wherever it sits (literal: no actual slot is consumed; no case
        // repeats a slot name). `extra` = an actual slot outside the expected names. Null `Slots`
        // (a recogniser event's command) reads as none.
        static int Matched(VoxrCommand cmd, KeyValuePair<string, string>[] expected, out bool extra)
        {
            VoxrSlotMatch[] actual = cmd.Slots ?? Array.Empty<VoxrSlotMatch>();
            int matched = 0;
            foreach (KeyValuePair<string, string> e in expected)
            foreach (VoxrSlotMatch s in actual)
                if (
                    string.Equals(s.Name, e.Key, StringComparison.Ordinal)
                    && string.Equals(s.Value, e.Value, StringComparison.Ordinal)
                )
                {
                    matched++;
                    break;
                }

            extra = false;
            foreach (VoxrSlotMatch s in actual)
            {
                bool named = false;
                foreach (KeyValuePair<string, string> e in expected)
                    if (string.Equals(s.Name, e.Key, StringComparison.Ordinal))
                    {
                        named = true;
                        break;
                    }
                if (!named)
                    extra = true;
            }
            return matched;
        }

        // The dump's words as VoxrJsonParser.ParseWordsFromJson builds them for the recogniser.
        static VoxrWord[] ToVoxrWords(JoinedInput.Word[] words)
        {
            var result = new VoxrWord[words.Length];
            for (int i = 0; i < words.Length; i++)
                result[i] = new VoxrWord(
                    words[i].Text,
                    words[i].Conf,
                    words[i].Start,
                    words[i].End
                );
            return result;
        }
    }
}
