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
