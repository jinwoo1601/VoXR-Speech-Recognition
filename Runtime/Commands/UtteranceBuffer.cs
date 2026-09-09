// ============================================================================
// Purpose:  Accumulates split VOSK results into a single utterance before parsing
// Layer:    Runtime.Commands
// Owns:     UtteranceBuffer (internal sealed class)
// Depends:  VoxrWord, VoxrCommandParser (NoConfidence, SplitSeparator)
// ============================================================================
using System;
using System.Collections.Generic;

namespace VoXR.Commands
{
    internal sealed class UtteranceBuffer
    {
        readonly List<string> _texts = new List<string>();
        VoxrWord[] _wordBuf = new VoxrWord[32];
        int _wordCount;

        // Per-token confidence for the buffered utterance, accumulated one result at a time and
        // indexed by token position in the text PeekText/Flush returns.
        //
        // Built HERE rather than from the joined text because alignment is a per-RESULT property:
        // Append takes text unconditionally but words only when a result supplies them, so the
        // joined token stream and the concatenated word stream can be offset. A single walk over
        // the join credited a wordless segment's token with a later segment's word and left the
        // real token with no data — which reads as -1 and BYPASSES minConfidence rather than
        // failing it. Filling each segment's own slice keeps a word inside the result it came
        // from.
        float[] _confBuf = new float[32];
        int _confCount;

        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();
        float _lastResultTime;

        internal bool IsActive { get; private set; }

        internal void Append(string text, VoxrWord[] words, float currentTime)
        {
            _texts.Add(text);
            if (words != null && words.Length > 0)
            {
                int needed = _wordCount + words.Length;
                if (needed > _wordBuf.Length)
                    Array.Resize(ref _wordBuf, Math.Max(needed, _wordBuf.Length * 2));
                Array.Copy(words, 0, _wordBuf, _wordCount, words.Length);
                _wordCount += words.Length;
            }

            int tokenCount = CountTokens(text);
            if (tokenCount > 0)
            {
                int confNeeded = _confCount + tokenCount;
                if (confNeeded > _confBuf.Length)
                    Array.Resize(ref _confBuf, Math.Max(confNeeded, _confBuf.Length * 2));
                FillSegmentConfidence(text, words, tokenCount, _confCount);
                _confCount += tokenCount;
            }

            _lastResultTime = currentTime;
            IsActive = true;
        }

        // Non-empty runs between spaces — the number of tokens this text contributes to the
        // joined utterance. Mirrors VoxrCommandParser.SplitSeparator + RemoveEmptyEntries (space
        // is the ONLY separator, so a tab or newline is part of a token) without allocating a
        // string[].
        static int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            bool inToken = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                    inToken = false;
                else if (!inToken)
                {
                    inToken = true;
                    count++;
                }
            }
            return count;
        }

        void FillSegmentConfidence(string text, VoxrWord[] words, int tokenCount, int offset)
        {
            if (words == null || words.Length == 0)
            {
                for (int i = 0; i < tokenCount; i++)
                    _confBuf[offset + i] = VoxrCommandParser.NoConfidence;
                return;
            }

            if (words.Length == tokenCount)
            {
                // One word per token, in order — the shape the decoder actually emits (measured
                // 1:1 against real libvosk over the committed fixture corpus, [unk] included).
                for (int i = 0; i < tokenCount; i++)
                    _confBuf[offset + i] = words[i].Confidence;
                return;
            }

            // Ragged within a single result — only a caller-supplied pairing reaches this. Walk
            // this segment's own tokens against its own words, so a mis-pairing stays inside the
            // result that caused it instead of shifting every later token.
            var segTokens = text.Split(
                VoxrCommandParser.SplitSeparator,
                StringSplitOptions.RemoveEmptyEntries
            );
            int j = 0;
            for (int i = 0; i < tokenCount; i++)
            {
                while (j < words.Length && string.IsNullOrEmpty(words[j].Text))
                    j++;

                if (
                    j < words.Length
                    && string.Equals(words[j].Text, segTokens[i], StringComparison.Ordinal)
                )
                {
                    _confBuf[offset + i] = words[j].Confidence;
                    j++;
                }
                else
                {
                    _confBuf[offset + i] = VoxrCommandParser.NoConfidence;
                }
            }
        }

        internal bool ShouldFlush(float currentTime, float bufferWindow)
        {
            return currentTime - _lastResultTime >= bufferWindow;
        }

        // Joins the buffered results without consuming them. Returns string.Empty (never
        // null) for an empty buffer. Used by the eager-flush speculative parse.
        internal string PeekText()
        {
            if (_texts.Count == 0)
                return string.Empty;

            // Fast path: single entry avoids StringBuilder overhead.
            if (_texts.Count == 1)
                return _texts[0];

            _sb.Clear();
            for (int i = 0; i < _texts.Count; i++)
            {
                if (i > 0) _sb.Append(' ');
                _sb.Append(_texts[i]);
            }
            return _sb.ToString();
        }

        internal string Flush()
        {
            IsActive = false;
            string text = PeekText();
            _texts.Clear();
            return text;
        }

        internal ReadOnlySpan<VoxrWord> GetWordsSpan()
        {
            return _wordCount > 0
                ? new ReadOnlySpan<VoxrWord>(_wordBuf, 0, _wordCount)
                : ReadOnlySpan<VoxrWord>.Empty;
        }

        // Aligned to the token positions of PeekText()/Flush(): ConfidenceCount equals that
        // text's token count by construction, because every Append contributes exactly
        // CountTokens(text) entries and PeekText joins with a single space, which adds no
        // tokens. Entries past ConfidenceCount are stale and are never read — every consumer
        // walks token indices.
        internal float[] ConfidenceBuffer => _confBuf;

        internal int ConfidenceCount => _confCount;

        internal void ClearWords()
        {
            _wordCount = 0;
            _confCount = 0;
        }

        internal void Reset()
        {
            _texts.Clear();
            _wordCount = 0;
            _confCount = 0;
            IsActive = false;
        }
    }
}
