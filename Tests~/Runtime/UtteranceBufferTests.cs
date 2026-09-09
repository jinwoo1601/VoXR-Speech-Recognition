// ============================================================================
// Purpose:  Pins UtteranceBuffer's per-segment token/confidence alignment (issue #146)
// Layer:    Tests.Runtime
// Owns:     UtteranceBufferTests (public class)
// Depends:  UtteranceBuffer, VoxrWord, VoxrCommandParser
// ============================================================================
using System;
using NUnit.Framework;
using VoXR;
using VoXR.Commands;

namespace VoXR.Tests.Runtime
{
    // UtteranceBuffer accumulates the per-token confidence array itself, one segment per
    // Append, rather than letting a single walk run over the joined utterance. These tests pin
    // why: Append takes `text` unconditionally but `words` only when a result supplies them, so
    // the joined token stream and the concatenated word stream can be OFFSET. One walk over the
    // join then credits a word to a token from a DIFFERENT buffered result.
    //
    // That is not a cosmetic misattribution. A span left with no data reads as
    // VoxrCommandParser.NoConfidence (-1), and -1 BYPASSES minConfidence rather than failing
    // it, so a misdirected word both voids the segment that earned it and lets the segment that
    // supplied nothing through the gate.
    public class UtteranceBufferTests
    {
        const float Nc = VoxrCommandParser.NoConfidence;

        static VoxrWord[] CeaseFireWordsAt(float confidence)
        {
            return new[]
            {
                new VoxrWord("cease", confidence, 0f, 0.3f),
                new VoxrWord("fire", confidence, 0.3f, 0.6f),
            };
        }

        // Only [0, ConfidenceCount) is meaningful — the backing array is pooled and grows, so
        // entries past the count are stale by design and no consumer reads them.
        static float[] LiveConfidences(UtteranceBuffer buffer)
        {
            var slice = new float[buffer.ConfidenceCount];
            Array.Copy(buffer.ConfidenceBuffer, slice, slice.Length);
            return slice;
        }

        static void AssertConfidences(float[] expected, UtteranceBuffer buffer, string because)
        {
            var actual = LiveConfidences(buffer);
            Assert.AreEqual(expected.Length, actual.Length, $"{because} (ConfidenceCount)");
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], 1e-5f, $"{because} (token {i})");
        }

        [Test]
        public void Append_WordlessThenCollidingWorded_CreditsOnlyTheSegmentThatSuppliedTheWords()
        {
            // The verified repro. A wordless result is buffered, then within the buffer window a
            // SECOND result arrives with the SAME text and real word data.
            var buffer = new UtteranceBuffer();

            buffer.Append("cease fire", null, 0f);
            buffer.Append("cease fire", CeaseFireWordsAt(0.2f), 0.1f);

            Assert.AreEqual("cease fire cease fire", buffer.PeekText());
            Assert.AreEqual(4, buffer.ConfidenceCount);

            // The words belong to the SECOND segment, so they land on tokens 2 and 3.
            //
            // THE BUG THIS PINS: one greedy walk over the joined utterance produced exactly the
            // reversed array — [0.2, 0.2, -1, -1]. Both words matched the joined stream's first
            // two tokens by text and were consumed there, crediting the WORDLESS segment with
            // confidence it never supplied and leaving the segment that DID supply it with no
            // data at all. Extraction round 2's span [2,4) then computed as -1, and because -1
            // bypasses minConfidence instead of failing it, a command fired that the pre-PR code
            // rejected.
            AssertConfidences(
                new[] { Nc, Nc, 0.2f, 0.2f },
                buffer,
                "words must stay inside the result that carried them"
            );
        }

        [Test]
        public void Append_WordedThenCollidingWordless_CreditsOnlyTheSegmentThatSuppliedTheWords()
        {
            // The same collision with the order swapped. Here the greedy walk happened to agree
            // with the per-segment build, which is precisely why the bug survived: whichever
            // round holds the words is the one the externally observable fire/no-fire outcome
            // reports, so the two orders look identical from outside. Only the array itself, or
            // the per-round diagnostics, tells them apart.
            var buffer = new UtteranceBuffer();

            buffer.Append("cease fire", CeaseFireWordsAt(0.2f), 0f);
            buffer.Append("cease fire", null, 0.1f);

            Assert.AreEqual("cease fire cease fire", buffer.PeekText());
            Assert.AreEqual(4, buffer.ConfidenceCount);

            AssertConfidences(
                new[] { 0.2f, 0.2f, Nc, Nc },
                buffer,
                "the leading segment carried the words, so the trailing one has no data"
            );
        }

        [Test]
        public void ConfidenceCount_AlwaysEqualsTheTokenCountOfPeekText()
        {
            // The invariant every consumer depends on: the array is indexed by token position in
            // PeekText()/Flush(), so its live length has to be that text's token count exactly —
            // including for the inputs that make counting fiddly. An empty append contributes no
            // token but still joins with a separating space, and a segment padded with leading,
            // trailing and repeated spaces contributes only its non-empty runs.
            var buffer = new UtteranceBuffer();

            buffer.Append("cease fire", null, 0f);
            buffer.Append("", null, 0.1f);
            buffer.Append(
                "   resume    fire   ",
                new[]
                {
                    new VoxrWord("resume", 0.9f, 0.7f, 1.0f),
                    new VoxrWord("fire", 0.8f, 1.0f, 1.3f),
                },
                0.2f
            );
            buffer.Append("disengage", null, 0.3f);

            string text = buffer.PeekText();
            int tokenCount = text.Split(
                VoxrCommandParser.SplitSeparator,
                StringSplitOptions.RemoveEmptyEntries
            ).Length;

            Assert.AreEqual(
                tokenCount,
                buffer.ConfidenceCount,
                "ConfidenceCount is the token count of PeekText() by construction — a mismatch "
                    + "means every later token's confidence is read from the wrong slot"
            );

            // And the values are still per-segment: the padded third append is the only one that
            // carried words, so it owns tokens 2 and 3 and nothing else does.
            AssertConfidences(
                new[] { Nc, Nc, 0.9f, 0.8f, Nc },
                buffer,
                "ragged appends keep each segment's words inside that segment"
            );
        }

        // --- The positional fast path is VERIFIED, not assumed (issue #146) ---
        //
        // Each test below appends ONE segment, so nothing about the join is in play: what is
        // under test is the single-result decision between the allocation-free positional fill
        // and the ragged aligned walk, and whether the verification lets the wrong pairing
        // through.

        [Test]
        public void Append_EqualLengthButMisPaired_FallsThroughToTheAlignedWalk()
        {
            // Text and words are the same LENGTH but in the wrong ORDER. Length equality is the
            // whole of what an unverified positional copy consults, so this is precisely the
            // shape such a copy gets silently wrong.
            var buffer = new UtteranceBuffer();

            buffer.Append(
                "cease fire",
                new[]
                {
                    new VoxrWord("fire", 0.9f, 0f, 0.3f),
                    new VoxrWord("cease", 0.8f, 0.3f, 0.6f),
                },
                0f
            );

            Assert.AreEqual(2, buffer.ConfidenceCount);

            // Derived from the live code. TryFillPositionalVerified compares token 0's span
            // ("cease") against words[0].Text ("fire"), mismatches, and returns false before
            // writing anything — so the whole segment goes to the ragged path.
            // FillAlignedConfidence then walks it: "fire" appears in a LATER token, so that
            // word is waiting for its own token and token 0 takes NoConfidence with only the
            // token cursor advancing; token 1 matches "fire" and takes 0.9. "cease" (0.8) is
            // left unconsumed — the token it belongs to is already behind the forward-only
            // cursor.
            //
            // PRE-HARDENING: [0.9, 0.8]. The `words.Length == tokenCount` branch copied
            // positionally without comparing the texts, so token "cease" was credited with
            // "fire"'s 0.9 and token "fire" with "cease"'s 0.8 — both values on the wrong
            // token, and the span minimum 0.8 instead of 0.9.
            AssertConfidences(
                new[] { Nc, 0.9f },
                buffer,
                "an equal-length but mis-paired segment must not be copied positionally"
            );
        }

        [Test]
        public void Append_EqualLengthAndCorrectlyPaired_FillsFromTheVerifiedPositionalPath()
        {
            // The other branch of the verification, and the shape the decoder actually emits:
            // one word per token, in order. Every token verifies, so the positional fill
            // completes and the segment text is never split.
            var buffer = new UtteranceBuffer();

            buffer.Append(
                "cease fire",
                new[]
                {
                    new VoxrWord("cease", 0.9f, 0f, 0.3f),
                    new VoxrWord("fire", 0.8f, 0.3f, 0.6f),
                },
                0f
            );

            Assert.AreEqual(2, buffer.ConfidenceCount);

            // PRE-HARDENING produced this same array, by the unverified copy. So this is
            // coverage rather than a regression pin — it is here so that a fix which rejected
            // the GOOD pairing along with the bad one could not pass unnoticed.
            AssertConfidences(
                new[] { 0.9f, 0.8f },
                buffer,
                "the decoder's own 1:1 shape must still fill positionally"
            );
        }

        [Test]
        public void Append_ForeignTrailingWord_IsDroppedRatherThanCreditedToItsToken()
        {
            // Verification succeeds on token 0 and fails on token 1, so it abandons partway and
            // the ragged walk rewrites the whole segment. (The abandoned pass and the ragged
            // walk necessarily AGREE on the verified prefix — a verified match is also a match
            // for the aligned walk — so the rewrite's completeness is not what this observes.
            // What it observes is token 1.)
            var buffer = new UtteranceBuffer();

            buffer.Append(
                "cease fire",
                new[]
                {
                    new VoxrWord("cease", 0.9f, 0f, 0.3f),
                    new VoxrWord("resume", 0.8f, 0.3f, 0.6f),
                },
                0f
            );

            Assert.AreEqual(2, buffer.ConfidenceCount);

            // Derived from the live code. Ragged walk: token 0 matches "cease" for 0.9; then
            // "resume" appears in NO remaining token, so it is foreign and dropped, the word
            // cursor runs out, and token 1 takes NoConfidence.
            //
            // PRE-HARDENING: [0.9, 0.8] — the positional copy handed token "fire" the
            // confidence of "resume", a word that is not in this transcript at all.
            AssertConfidences(
                new[] { 0.9f, Nc },
                buffer,
                "a word foreign to the transcript must not be credited to the token it "
                    + "happens to sit opposite"
            );
        }

        [Test]
        public void ClearWords_ZeroesTheConfidenceCount()
        {
            var buffer = new UtteranceBuffer();
            buffer.Append("cease fire", CeaseFireWordsAt(0.9f), 0f);
            Assert.AreEqual(2, buffer.ConfidenceCount, "setup: the segment was accumulated");

            buffer.ClearWords();

            // FlushBuffer calls this straight after parsing. If the count survived, the next
            // utterance's first token would index into the previous utterance's confidences.
            Assert.AreEqual(0, buffer.ConfidenceCount);
        }

        [Test]
        public void Reset_ZeroesTheConfidenceCount()
        {
            var buffer = new UtteranceBuffer();
            buffer.Append("cease fire", CeaseFireWordsAt(0.9f), 0f);
            Assert.AreEqual(2, buffer.ConfidenceCount, "setup: the segment was accumulated");

            buffer.Reset();

            Assert.AreEqual(0, buffer.ConfidenceCount);
            Assert.IsFalse(buffer.IsActive);
            Assert.AreEqual(string.Empty, buffer.PeekText());
        }
    }
}
