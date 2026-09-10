// ============================================================================
// Purpose:  Validates a grammar string before it can reach VOSK's crashing grammar API
// Layer:    Runtime
// Owns:     VoxrGrammarValidator (internal static class)
// Depends:  (none)
// ============================================================================
namespace VoXR
{
    // vosk_recognizer_new_grm does not report a bad grammar — it dereferences its way
    // through the parse and dies. Measured against libvosk 0.3.45, an empty array `[]`,
    // an array with an unescaped quote inside an element (`["a"b", "c"]`), an object
    // (`{"grammar": ["fire"]}`) and plain prose (`not json at all`) all raise SIGSEGV and
    // take the process with them, in the Editor and on device alike (#150); only a
    // well-formed array holding a non-string (`["fire", 42]`) fails politely by returning
    // NULL. A managed NULL check therefore cannot be reached for the shapes that matter,
    // so the grammar has to be proven well formed before the native call is made at all.
    //
    // The check is deliberately narrower than a JSON parser: it answers only "is this a
    // complete JSON array of one or more strings", which is the exact shape the grammar
    // API accepts, and it never builds a value. Hand-rolled over the string like the rest
    // of the JSON handling here (see VoxrJsonParser) — the package takes no JSON dependency.
    //
    // Every shape this check accepts was measured safe against libvosk 0.3.45, the empty
    // element string `[""]` included. In the other direction the check is deliberately
    // stricter than the decoder: trailing content after the array, an invalid backslash
    // escape and a raw control character inside a string were each measured to be tolerated
    // by that libvosk, which parses them leniently — they are refused anyway, because a loud
    // rejection naming the offending index is better authoring feedback than a grammar word
    // the decoder silently drops, and because pinning this check to one vendored version's
    // lenient JSON parsing would be brittle. The rest of the rejected set is load-bearing
    // rather than tidiness: beyond the shapes #150 recorded, a whitespace-only string, an
    // unterminated string (`["a`) and a bare `[` were each measured to segfault too.
    internal static class VoxrGrammarValidator
    {
        // Total: never throws, for any input including null, empty, whitespace-only or
        // truncated. A null or empty grammar returns false here because it is not a
        // well-formed array; the caller is the one that reads null/empty as "clear the
        // grammar" and must test for it before asking this question.
        internal static bool IsWellFormedGrammar(string grammarJson, out string reason)
        {
            if (string.IsNullOrEmpty(grammarJson))
            {
                reason = "the grammar is null or empty";
                return false;
            }

            int i = SkipWhitespace(grammarJson, 0);
            if (i >= grammarJson.Length || grammarJson[i] != '[')
            {
                reason = $"expected '[' at index {i}";
                return false;
            }
            i++;

            // VOSK crashes on an empty array, so it is rejected here rather than left to
            // the "expected a string" branch, which would describe it less usefully.
            int afterBracket = SkipWhitespace(grammarJson, i);
            if (afterBracket < grammarJson.Length && grammarJson[afterBracket] == ']')
            {
                reason = "empty array";
                return false;
            }

            while (true)
            {
                i = SkipWhitespace(grammarJson, i);
                if (i >= grammarJson.Length)
                {
                    reason = $"the grammar ends where a string was expected (index {i})";
                    return false;
                }
                if (grammarJson[i] != '"')
                {
                    reason = $"expected a string at index {i}";
                    return false;
                }
                if (!ScanString(grammarJson, ref i, out reason))
                    return false;

                i = SkipWhitespace(grammarJson, i);
                if (i >= grammarJson.Length)
                {
                    reason = $"the grammar ends before the closing ']' (index {i})";
                    return false;
                }
                if (grammarJson[i] == ',')
                {
                    i++;
                    continue;
                }
                if (grammarJson[i] == ']')
                {
                    i++;
                    break;
                }

                reason = $"expected ',' or ']' at index {i}";
                return false;
            }

            i = SkipWhitespace(grammarJson, i);
            if (i != grammarJson.Length)
            {
                reason = $"trailing content after the closing ']' at index {i}";
                return false;
            }

            reason = null;
            return true;
        }

        // On entry i is the opening quote; on success it lands just past the closing one.
        static bool ScanString(string s, ref int i, out string reason)
        {
            int start = i;
            i++;

            while (i < s.Length)
            {
                char c = s[i];

                if (c == '"')
                {
                    i++;
                    reason = null;
                    return true;
                }

                if (c == '\\')
                {
                    if (i + 1 >= s.Length)
                    {
                        reason = $"the grammar ends inside an escape at index {i}";
                        return false;
                    }

                    char esc = s[i + 1];
                    if (esc == 'u')
                    {
                        if (i + 5 >= s.Length)
                        {
                            reason = $"the grammar ends inside a \\u escape at index {i}";
                            return false;
                        }
                        for (int h = i + 2; h <= i + 5; h++)
                        {
                            if (!IsHexDigit(s[h]))
                            {
                                reason = $"invalid \\u escape at index {i}";
                                return false;
                            }
                        }
                        i += 6;
                        continue;
                    }

                    if (
                        esc == '"'
                        || esc == '\\'
                        || esc == '/'
                        || esc == 'b'
                        || esc == 'f'
                        || esc == 'n'
                        || esc == 'r'
                        || esc == 't'
                    )
                    {
                        i += 2;
                        continue;
                    }

                    reason = $"invalid escape '\\{esc}' at index {i}";
                    return false;
                }

                if (c < ' ')
                {
                    reason =
                        $"raw control character U+{(int)c:X4} inside the string starting at index {start}";
                    return false;
                }

                i++;
            }

            reason = $"unterminated string starting at index {start}";
            return false;
        }

        static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
                i++;
            return i;
        }

        static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
