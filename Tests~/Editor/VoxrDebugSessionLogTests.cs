using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VoXR;
using VoXR.Commands;
using VoXR.Editor;

namespace VoXR.Tests.Editor
{
    public class VoxrDebugSessionLogTests
    {
        static VoxrMatchDiagnostics MakeDiagnostics(
            VoxrWord[] words,
            VoxrMatchAttempt[] attempts,
            string inputText = "launch missiles"
        ) => new VoxrMatchDiagnostics(inputText, words, attempts, 42);

        [Test]
        public void BuildEntry_CopiesWordsAttemptsAndSlots()
        {
            var words = new[]
            {
                new VoxrWord("launch", 0.9f, 0.0f, 0.3f),
                new VoxrWord("missiles", 0.8f, 0.3f, 0.7f),
            };
            var slots = new[] { new VoxrDiagnosticSlotMatch("weapon", "missiles", 1, 1, 0.8f) };
            var attempts = new[]
            {
                new VoxrMatchAttempt(
                    "launch_weapon",
                    "launch {weapon}",
                    0.95f,
                    0.6f,
                    0.85f,
                    0.4f,
                    slots,
                    null,
                    true
                ),
            };

            var entry = VoxrDebugSessionLog.BuildEntry(null, MakeDiagnostics(words, attempts));

            Assert.AreEqual("launch missiles", entry.inputText);
            Assert.AreEqual(42, entry.frame);

            Assert.AreEqual(2, entry.words.Length);
            Assert.AreEqual("missiles", entry.words[1].text);
            Assert.AreEqual(0.8f, entry.words[1].confidence);
            Assert.AreEqual(0.3f, entry.words[1].startTime);
            Assert.AreEqual(0.7f, entry.words[1].endTime);

            Assert.AreEqual(1, entry.attempts.Length);
            var a = entry.attempts[0];
            Assert.AreEqual("launch_weapon", a.intent);
            Assert.AreEqual("launch {weapon}", a.pattern);
            Assert.AreEqual(0.95f, a.score);
            Assert.AreEqual(0.6f, a.minScore);
            Assert.AreEqual(0.85f, a.aggregateConfidence);
            Assert.AreEqual(0.4f, a.minConfidence);
            Assert.IsTrue(a.accepted);

            Assert.AreEqual(1, a.slots.Length);
            Assert.AreEqual("weapon", a.slots[0].name);
            Assert.AreEqual("missiles", a.slots[0].value);
            Assert.AreEqual(1, a.slots[0].startWord);
            Assert.AreEqual(1, a.slots[0].endWord);
            Assert.AreEqual(0.8f, a.slots[0].confidence);
        }

        [Test]
        public void BuildEntry_NullStringsBecomeEmpty()
        {
            var attempts = new[]
            {
                new VoxrMatchAttempt(null, null, 0f, 0.6f, 0f, 0.4f, null, "no match", false),
            };

            var entry = VoxrDebugSessionLog.BuildEntry(
                null,
                MakeDiagnostics(Array.Empty<VoxrWord>(), attempts, null)
            );

            Assert.AreEqual("", entry.inputText);
            Assert.AreEqual("", entry.attempts[0].intent);
            Assert.AreEqual("", entry.attempts[0].pattern);
            Assert.AreEqual("no match", entry.attempts[0].rejectReason);
            Assert.AreEqual("", entry.attempts[0].tiedRival);
            Assert.IsFalse(entry.attempts[0].tiedRivalIsSibling);
            Assert.IsFalse(entry.attempts[0].accepted);
            Assert.AreEqual(0, entry.attempts[0].slots.Length);
            Assert.AreEqual(0, entry.words.Length);
        }

        /// <summary>
        /// A registration-order coin flip and a clean win are identical in every other field,
        /// so the export has to carry the rival — and which kind of tie it was — or whole-session
        /// analysis cannot tell them apart. JsonUtility only serialises public fields, hence the
        /// assertions on the serialised JSON rather than DTO reads alone.
        /// </summary>
        [Test]
        public void BuildEntry_RecordsTiedRivalAndWhetherItWasASibling()
        {
            var attempts = new[]
            {
                new VoxrMatchAttempt(
                    "set_mode",
                    "weapons mode",
                    1f,
                    0.6f,
                    0.9f,
                    0.4f,
                    null,
                    null,
                    true,
                    "set_nav_mode (pattern 0)",
                    true
                ),
                new VoxrMatchAttempt(
                    "raise_shields",
                    "shields up",
                    1f,
                    0.6f,
                    0.9f,
                    0.4f,
                    null,
                    null,
                    true,
                    "activate_defence (pattern 1)",
                    false
                ),
                new VoxrMatchAttempt(
                    "cease_fire",
                    "cease fire",
                    1f,
                    0.6f,
                    0.9f,
                    0.4f,
                    null,
                    null,
                    true
                ),
            };

            var entry = VoxrDebugSessionLog.BuildEntry(
                null,
                MakeDiagnostics(Array.Empty<VoxrWord>(), attempts)
            );

            Assert.AreEqual("set_nav_mode (pattern 0)", entry.attempts[0].tiedRival);
            Assert.IsTrue(entry.attempts[0].tiedRivalIsSibling);
            Assert.AreEqual("activate_defence (pattern 1)", entry.attempts[1].tiedRival);
            Assert.IsFalse(entry.attempts[1].tiedRivalIsSibling);
            Assert.AreEqual("", entry.attempts[2].tiedRival);
            Assert.IsFalse(entry.attempts[2].tiedRivalIsSibling);

            string json = JsonUtility.ToJson(entry);

            StringAssert.Contains("\"tiedRival\":\"set_nav_mode (pattern 0)\"", json);
            StringAssert.Contains("\"tiedRivalIsSibling\":true", json);
            StringAssert.Contains("\"tiedRival\":\"activate_defence (pattern 1)\"", json);
            StringAssert.Contains("\"tiedRival\":\"\"", json);
            StringAssert.Contains("\"tiedRivalIsSibling\":false", json);
        }

        [Test]
        public void BuildEntry_NullSenderYieldsEmptyActiveSets()
        {
            var entry = VoxrDebugSessionLog.BuildEntry(
                null,
                MakeDiagnostics(Array.Empty<VoxrWord>(), Array.Empty<VoxrMatchAttempt>())
            );

            Assert.IsNotNull(entry.activeSets);
            Assert.AreEqual(0, entry.activeSets.Length);
        }

        [Test]
        public void BuildEntry_RecordsSenderActiveSets()
        {
            var go = new GameObject("SessionLogTestRecogniser");
            try
            {
                var recogniser = go.AddComponent<VoxrCommandRecogniser>();
                recogniser.Configure(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        new VoxrCommandSet(
                            "combat",
                            new[]
                            {
                                new VoxrCommandDefinition(
                                    "cease_fire",
                                    new[] { new[] { "cease", "fire" } }
                                ),
                            }
                        ),
                    }
                );
                recogniser.SetActiveSets("combat");

                var entry = VoxrDebugSessionLog.BuildEntry(
                    recogniser,
                    MakeDiagnostics(Array.Empty<VoxrWord>(), Array.Empty<VoxrMatchAttempt>())
                );

                CollectionAssert.AreEqual(new[] { "combat" }, entry.activeSets);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildEntry_SerialisesToJson()
        {
            var attempts = new[]
            {
                new VoxrMatchAttempt(
                    "cease_fire",
                    "cease fire",
                    1f,
                    0.6f,
                    0.9f,
                    0.4f,
                    null,
                    null,
                    true
                ),
            };
            var entry = VoxrDebugSessionLog.BuildEntry(
                null,
                MakeDiagnostics(Array.Empty<VoxrWord>(), attempts, "cease fire")
            );

            string json = JsonUtility.ToJson(entry);

            StringAssert.Contains("\"inputText\":\"cease fire\"", json);
            StringAssert.Contains("\"intent\":\"cease_fire\"", json);
            StringAssert.Contains("\"accepted\":true", json);
        }

        /// <summary>
        /// This test is itself running under the Test Runner, so the hook's RunStarted
        /// callback must already have fired. If the hook assembly failed to compile or its
        /// callbacks were never registered, this is the assertion that catches it.
        /// </summary>
        [Test]
        public void TestRunActive_IsSetWhileTheTestRunnerDrivesThisRun()
        {
            Assert.IsTrue(
                VoxrDebugSessionLog.TestRunActive,
                "Test Runner hook did not flag the run — the session log would export "
                    + "on exit from an in-editor test run and evict real playtest logs."
            );
        }

        [Test]
        public void TestRunActive_SurvivesAsSessionState()
        {
            // Round-trips through SessionState rather than a static field, so the flag
            // outlives the domain reload that entering Play Mode triggers.
            bool original = VoxrDebugSessionLog.TestRunActive;
            try
            {
                VoxrDebugSessionLog.TestRunActive = false;
                Assert.IsFalse(VoxrDebugSessionLog.TestRunActive);

                VoxrDebugSessionLog.TestRunActive = true;
                Assert.IsTrue(VoxrDebugSessionLog.TestRunActive);
            }
            finally
            {
                VoxrDebugSessionLog.TestRunActive = original;
            }
        }

        /// <summary>
        /// A barred round and a round's second choice are what issue #144 added to the export,
        /// and an attempt that carries neither is indistinguishable from one whose fields were
        /// dropped on the way into the DTO. JsonUtility serialises public fields only, so the
        /// assertions run against the serialised form as well as the DTO — the same shape
        /// BuildEntry_RecordsTiedRivalAndWhetherItWasASibling uses, and for the same reason.
        /// </summary>
        [Test]
        public void BuildEntry_RecordsBarredAndRunnerUp()
        {
            var attempts = new[]
            {
                // A refused round: no slots, not accepted, and a runner-up behind it.
                new VoxrMatchAttempt(
                    "approach_target",
                    "approach target {target}",
                    0.75f,
                    0.6f,
                    0.9f,
                    0.4f,
                    null,
                    "barred",
                    false,
                    null,
                    false,
                    true,
                    "set_level",
                    0.5f
                ),
                // An ordinary accepted round that had a second choice.
                new VoxrMatchAttempt(
                    "cease_fire",
                    "cease fire",
                    1f,
                    0.6f,
                    0.9f,
                    0.4f,
                    null,
                    null,
                    true,
                    null,
                    false,
                    false,
                    "set_mode",
                    0.75f
                ),
                // The null-runner-up case, which must land as an empty string rather than a
                // JSON null — the same treatment tiedRival gets, so a log reader has one rule.
                new VoxrMatchAttempt("engage", "engage", 1f, 0.6f, 0.9f, 0.4f, null, null, true),
            };

            var entry = VoxrDebugSessionLog.BuildEntry(
                null,
                MakeDiagnostics(Array.Empty<VoxrWord>(), attempts)
            );

            Assert.IsTrue(entry.attempts[0].barred);
            Assert.IsFalse(entry.attempts[0].accepted);
            Assert.AreEqual("barred", entry.attempts[0].rejectReason);
            Assert.AreEqual("set_level", entry.attempts[0].runnerUpIntent);
            Assert.AreEqual(0.5f, entry.attempts[0].runnerUpScore);

            Assert.IsFalse(entry.attempts[1].barred, "an ordinary round is not a barred one");
            Assert.AreEqual("set_mode", entry.attempts[1].runnerUpIntent);
            Assert.AreEqual(0.75f, entry.attempts[1].runnerUpScore);

            Assert.IsFalse(entry.attempts[2].barred);
            Assert.AreEqual("", entry.attempts[2].runnerUpIntent, "null becomes empty, not null");
            Assert.AreEqual(
                -1f,
                entry.attempts[2].runnerUpScore,
                "and -1 is the no-runner-up score"
            );

            string json = JsonUtility.ToJson(entry);

            StringAssert.Contains("\"barred\":true", json);
            StringAssert.Contains("\"barred\":false", json);
            StringAssert.Contains("\"runnerUpIntent\":\"set_level\"", json);
            StringAssert.Contains("\"runnerUpIntent\":\"set_mode\"", json);
            StringAssert.Contains("\"runnerUpIntent\":\"\"", json);

            // Round-tripped rather than string-matched: JsonUtility's float formatting is its
            // own business, and pinning its spelling here would fail for a reason that has
            // nothing to do with whether the score survived the export.
            var roundTripped = JsonUtility.FromJson<VoxrDebugSessionLog.Entry>(json);
            Assert.AreEqual(0.5f, roundTripped.attempts[0].runnerUpScore, 1e-6f);
            Assert.AreEqual(0.75f, roundTripped.attempts[1].runnerUpScore, 1e-6f);
            Assert.AreEqual(-1f, roundTripped.attempts[2].runnerUpScore, 1e-6f);
            Assert.IsTrue(roundTripped.attempts[0].barred);
            Assert.IsFalse(roundTripped.attempts[1].barred);
        }

        /// <summary>
        /// The exported log is read by tooling and by whoever receives a field report, and the
        /// schema version is the only thing telling them which shape they have. Issue #144 added
        /// three attempt fields and changed what a "no match" attempt means, which is a version
        /// bump; nothing pinned the number before, so a silent bump — or a silent failure to
        /// bump — was invisible. Reflected because the constant is private and deliberately
        /// stays that way.
        /// <para>
        /// Retargeted to 4 by issue #148, which added <c>resolved</c> and <c>resolvedReason</c>
        /// to every slot: the same rule, applied a second time. Re-pointing this assertion at
        /// the new version is what the test is FOR — the failure it exists to catch is a shape
        /// change that arrives without one.
        /// </para>
        /// </summary>
        [Test]
        public void SchemaVersion_IsFour()
        {
            var field = typeof(VoxrDebugSessionLog).GetField(
                "SchemaVersion",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            Assert.IsNotNull(
                field,
                "VoxrDebugSessionLog.SchemaVersion is gone or renamed — the exported log's "
                    + "schemaVersion is no longer pinned by anything"
            );
            Assert.AreEqual(
                4,
                (int)field.GetRawConstantValue(),
                "the log's shape changed with issue #148 — every slot now carries resolved and "
                    + "resolvedReason — so consumers need the bump"
            );
        }

        // ======== Resolver-filled slots in the export (issue #148, Phase 3) ========

        /// <summary>
        /// F22. A resolver-filled slot was never spoken, and an export that cannot say so
        /// reports an argument the game supplied as one the speaker gave. Both slots ride the
        /// same attempt deliberately: the contrast is the test, and a DTO mapping that marked
        /// every slot — or none — would pass half of a test written on one slot alone.
        /// </summary>
        [Test]
        public void BuildEntry_MarksAResolverFilledSlotAndLeavesASpokenOneUnmarked()
        {
            var slots = new[]
            {
                // Spoken: the 5-arg form, unchanged, as every pre-#148 construction site uses it.
                new VoxrDiagnosticSlotMatch("weapon", "missiles", 2, 3, 0.9f),
                // Resolver-filled: no word span and no confidence, because no word was said.
                new VoxrDiagnosticSlotMatch("track", "alpha", -1, -1, -1f, "main target"),
            };
            var attempts = new[]
            {
                new VoxrMatchAttempt(
                    "launch_weapon",
                    "launch {weapon} at {track}",
                    0.75f,
                    0.6f,
                    0.9f,
                    0.4f,
                    slots,
                    null,
                    true
                ),
            };

            var entry = VoxrDebugSessionLog.BuildEntry(
                null,
                MakeDiagnostics(Array.Empty<VoxrWord>(), attempts)
            );

            Assert.AreEqual(2, entry.attempts[0].slots.Length);
            var spoken = SlotNamed(entry.attempts[0], "weapon");
            var filled = SlotNamed(entry.attempts[0], "track");

            Assert.IsFalse(spoken.resolved, "the speaker said this one");
            Assert.AreEqual("", spoken.resolvedReason, "so no resolver gave a reason for it");
            Assert.AreEqual(2, spoken.startWord, "and its real span is untouched");
            Assert.AreEqual(3, spoken.endWord);
            Assert.AreEqual(0.9f, spoken.confidence, 1e-6f);

            Assert.IsTrue(filled.resolved, "the game supplied this one");
            Assert.AreEqual("main target", filled.resolvedReason, "and said why");
            Assert.AreEqual("alpha", filled.value, "the value still reaches the log");
            Assert.AreEqual(-1, filled.startWord, "with no span, because nobody spoke it");
            Assert.AreEqual(-1, filled.endWord);
            Assert.AreEqual(-1f, filled.confidence, 1e-6f);

            // JsonUtility serialises public fields only, so the export is checked as JSON too —
            // the shape BuildEntry_RecordsTiedRivalAndWhetherItWasASibling established.
            string json = JsonUtility.ToJson(entry);

            StringAssert.Contains("\"resolved\":true", json);
            StringAssert.Contains("\"resolved\":false", json);
            StringAssert.Contains("\"resolvedReason\":\"main target\"", json);
        }

        /// <summary>
        /// D-17, and the row the two-field shape exists for. A resolver may legitimately state
        /// no reason: D-8 normalises its null <c>Reason</c> to <c>string.Empty</c> on the way
        /// into <c>VoxrCommand.ResolvedSlots</c>, and JsonUtility writes <c>""</c> for a null
        /// string anyway — so in the exported JSON an unexplained resolution and a spoken slot
        /// carry the SAME <c>resolvedReason</c>. This test asserts that they do, and that
        /// <c>resolved</c> still tells them apart; with the reason string alone there would be
        /// no field on which the two differ, and F22's "distinguishably" would be true only by
        /// reading the -1 spans.
        /// <para>
        /// Driven through a live recogniser rather than hand-built structs, because the case is
        /// only reachable through D-8's normalisation and a hand-built <c>""</c> could be
        /// dismissed as a shape the pipeline never produces.
        /// </para>
        /// </summary>
        [Test]
        public void BuildEntry_AReasonlessResolutionIsStillDistinguishableFromASpokenSlot()
        {
            var go = new GameObject("SessionLogResolverRecogniser");
            try
            {
                var recogniser = go.AddComponent<VoxrCommandRecogniser>();
                ConfigureResolvable(recogniser);

                // Null Reason: legal, and reads as "filled, reason unstated".
                recogniser.RegisterSlotResolver(
                    "track",
                    _ => new VoxrSlotResolution("alpha", null)
                );

                recogniser.InjectText(BareLaunchOrder);

                var entry = VoxrDebugSessionLog.BuildEntry(
                    recogniser,
                    recogniser.LastMatchDiagnostics
                );

                Assert.AreEqual(1, entry.attempts.Length);
                var attempt = entry.attempts[0];
                Assert.IsTrue(
                    attempt.accepted,
                    "precondition: the resolver completed the command, so there is a resolved "
                        + "slot to log at all"
                );

                var spoken = SlotNamed(attempt, "weapon");
                var filled = SlotNamed(attempt, "track");

                // The string cannot separate them, and this is the assertion that says so.
                Assert.AreEqual("", spoken.resolvedReason);
                Assert.AreEqual("", filled.resolvedReason);
                Assert.AreEqual(
                    spoken.resolvedReason,
                    filled.resolvedReason,
                    "the reason string is byte-identical on both, which is exactly why it cannot "
                        + "be the discriminator"
                );

                // The bool can, and it is the only thing that does.
                Assert.IsFalse(spoken.resolved, "the speaker said this one");
                Assert.IsTrue(
                    filled.resolved,
                    "and this one is still marked resolver-filled with no reason to mark it by"
                );
                Assert.AreEqual("alpha", filled.value);
                Assert.AreEqual(-1, filled.startWord, "the span corroborates; it is not the signal");

                // Round-tripped rather than string-matched: the claim is that the distinction
                // survives export, and JsonUtility's spelling of the rest is its own business.
                var roundTripped = JsonUtility.FromJson<VoxrDebugSessionLog.Entry>(
                    JsonUtility.ToJson(entry)
                );
                var rtSpoken = SlotNamed(roundTripped.attempts[0], "weapon");
                var rtFilled = SlotNamed(roundTripped.attempts[0], "track");

                Assert.AreEqual(
                    rtSpoken.resolvedReason,
                    rtFilled.resolvedReason,
                    "the round trip recovers no distinction the reason string never carried"
                );
                Assert.IsFalse(rtSpoken.resolved);
                Assert.IsTrue(
                    rtFilled.resolved,
                    "so the bool is what survives the export, and the only thing that does"
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // -------- Resolver fixture helpers --------

        // Eight required elements with {track} stranded: (7 x 1 - 1) / 8 = 0.75, clear of the
        // 0.60 gate. A resolver is only ever offered a candidate that already passed the score
        // gate (F7, D-5), so a shorter pattern would pin the SCORE gate's silence and call it
        // the resolver's. Same shape the Phase 2 resolver fixtures use, for the same reason.
        const string BareLaunchOrder = "launch all missiles from tube three at";

        static void ConfigureResolvable(VoxrCommandRecogniser recogniser)
        {
            recogniser.Configure(
                new[]
                {
                    new VoxrSlotDefinition("track", new[] { "alpha", "bravo" }),
                    new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                    new VoxrSlotDefinition("quantity", new[] { "all", "one", "two" }),
                    new VoxrSlotDefinition("tube", new[] { "one", "two", "three" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[]
                        {
                            new[]
                            {
                                "launch",
                                "{quantity}",
                                "{weapon}",
                                "from",
                                "tube",
                                "{tube}",
                                "at",
                                "{track}",
                            },
                        },
                        allowPartialMatch: true
                    ),
                }
            );
            recogniser.BufferWindow = 0f;
            recogniser.CommandCooldown = 0f;
            recogniser.PendingTimeout = 30f;
        }

        static VoxrDebugSessionLog.SlotDto SlotNamed(
            VoxrDebugSessionLog.AttemptDto attempt,
            string name
        )
        {
            for (int i = 0; i < attempt.slots.Length; i++)
            {
                if (attempt.slots[i].name == name)
                    return attempt.slots[i];
            }

            Assert.Fail($"the exported attempt carries no slot named '{name}'");
            return null;
        }
    }
}
