// ============================================================================
// Purpose:  EditMode tests for the grammar well-formedness check that guards VOSK (#150)
// Layer:    Tests.Editor
// Owns:     VoxrGrammarValidatorTests (public class)
// Depends:  VoxrGrammarValidator, VoxrCommandParser
// ============================================================================
using NUnit.Framework;
using VoXR;
using VoXR.Commands;

namespace VoXR.Tests.Editor
{
    // vosk_recognizer_new_grm segfaults rather than returning NULL for every malformed
    // shape measured in #150, so this check is the only thing standing between an author
    // mistake and a dead process. The rejected cases below include all four crashers #150
    // recorded ([], prose, an object, and an unescaped quote inside an element) plus three
    // more that a later measurement against libvosk 0.3.45 with vosk-model-small-en-us-0.15
    // found to segfault as well: a whitespace-only string, an unterminated string and a
    // bare "[". The same measurement showed [""] returning non-NULL, which is why the empty
    // element string sits in the accepted set rather than the rejected one.
    public class VoxrGrammarValidatorTests
    {
        [TestCase("[\"[unk]\", \"fire\", \"cease\"]", TestName = "Accepts_TypicalGrammar")]
        [TestCase("[\"fire\"]", TestName = "Accepts_SingleElement")]
        [TestCase("  [ \"fire\" , \"cease\" ]  ", TestName = "Accepts_InteriorWhitespace")]
        [TestCase("\n[\n\"fire\",\n\t\"cease\"\n]\n", TestName = "Accepts_Newlines")]
        [TestCase("[\"say \\\"go\\\"\"]", TestName = "Accepts_EscapedQuoteInElement")]
        [TestCase("[\"A\"]", TestName = "Accepts_SingleLetterElement")]
        [TestCase("[\"\"]", TestName = "Accepts_EmptyElementString")]
        [TestCase("[\"fire\\u0020now\"]", TestName = "Accepts_UnicodeEscape")]
        public void WellFormed_IsAccepted(string grammarJson)
        {
            bool ok = VoxrGrammarValidator.IsWellFormedGrammar(grammarJson, out string reason);

            Assert.IsTrue(ok, $"Expected acceptance but was rejected: {reason}");
            Assert.IsNull(reason, "An accepted grammar must report no reason.");
        }

        [TestCase("[]", TestName = "Rejects_EmptyArray")]
        [TestCase("not json at all", TestName = "Rejects_Prose")]
        [TestCase("{\"grammar\": [\"fire\"]}", TestName = "Rejects_Object")]
        [TestCase("[\"a\"b\", \"c\"]", TestName = "Rejects_UnescapedQuoteInElement")]
        [TestCase("[\"fire\", 42]", TestName = "Rejects_NonStringElement")]
        [TestCase(null, TestName = "Rejects_Null")]
        [TestCase("", TestName = "Rejects_Empty")]
        [TestCase("   ", TestName = "Rejects_WhitespaceOnly")]
        [TestCase("[", TestName = "Rejects_OpenBracketOnly")]
        [TestCase("[\"a\"", TestName = "Rejects_MissingCloseBracket")]
        [TestCase("[\"a\",]", TestName = "Rejects_TrailingComma")]
        [TestCase("[\"a\"] trailing", TestName = "Rejects_TrailingContent")]
        [TestCase("[\"a\\q\"]", TestName = "Rejects_InvalidEscape")]
        [TestCase("[\"a\\u12g4\"]", TestName = "Rejects_BadHexDigitInUnicodeEscape")]
        [TestCase("[\"a\\u1", TestName = "Rejects_TruncatedUnicodeEscape")]
        [TestCase("[\"a\nb\"]", TestName = "Rejects_RawControlCharacterInString")]
        [TestCase("[\"a\", ]", TestName = "Rejects_TrailingCommaWithSpace")]
        [TestCase("[[\"a\"]]", TestName = "Rejects_NestedArray")]
        [TestCase("[null]", TestName = "Rejects_NullElement")]
        [TestCase("[{\"w\":\"a\"}]", TestName = "Rejects_ObjectElement")]
        public void Malformed_IsRejectedWithReason(string grammarJson)
        {
            bool ok = VoxrGrammarValidator.IsWellFormedGrammar(grammarJson, out string reason);

            Assert.IsFalse(ok, "A malformed grammar must not be accepted.");
            Assert.IsNotNull(reason, "A rejection must carry a reason.");
            Assert.IsNotEmpty(reason, "A rejection reason must not be empty.");
        }

        [Test]
        public void HostileInput_NeverThrows()
        {
            // Every truncation of a valid grammar, so every partial state of the scan —
            // mid-escape, mid-\u, between elements — is entered with the string ending
            // underneath it.
            const string valid = "[\"say \\\"go\\\"\", \"fire\\u0020now\", \"cease\"]";
            for (int length = 0; length <= valid.Length; length++)
            {
                string truncated = valid.Substring(0, length);
                Assert.DoesNotThrow(
                    () => VoxrGrammarValidator.IsWellFormedGrammar(truncated, out _),
                    $"Threw on truncation at length {length}: \"{truncated}\""
                );
            }

            string[] hostile =
            {
                null,
                "",
                " ",
                "\0",
                "[\"a\\",
                "[\"a\\u",
                "[\"a\\u12",
                "]",
                ",",
                "\"",
                "[,]",
                "[\"\\\\\"]",
                "[\"a\",,\"b\"]",
            };
            foreach (string input in hostile)
            {
                Assert.DoesNotThrow(
                    () => VoxrGrammarValidator.IsWellFormedGrammar(input, out _),
                    $"Threw on hostile input: \"{input}\""
                );
            }
        }

        // The whole point of the guard is that real package output keeps working, so pin
        // the generator's own product against it rather than trusting hand-written samples.
        [Test]
        public void PackageGeneratedGrammar_IsAccepted()
        {
            var slots = new[]
            {
                new VoxrSlotDefinition(
                    "target",
                    new[] { "hotel one", "hotel two", "alpha one", "bravo two" }
                ),
                new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes", "jackal" }),
                new VoxrSlotDefinition("quantity", new[] { "all", "one", "two", "three" }),
            };
            var commands = new[]
            {
                new VoxrCommandDefinition(
                    "launch_weapon",
                    new[]
                    {
                        new[] { "launch", "?a", "{?quantity}", "{weapon}", "target", "{target}" },
                        new[] { "fire", "{?quantity}", "{weapon}", "at", "{target}" },
                    }
                ),
                new VoxrCommandDefinition(
                    "cease_fire",
                    new[] { new[] { "cease", "fire" }, new[] { "disengage" } }
                ),
            };

            string grammarJson = VoxrCommandParser.GenerateGrammarJson(slots, commands);

            bool ok = VoxrGrammarValidator.IsWellFormedGrammar(grammarJson, out string reason);

            Assert.IsTrue(ok, $"Package-generated grammar was rejected ({reason}): {grammarJson}");
        }
    }
}
