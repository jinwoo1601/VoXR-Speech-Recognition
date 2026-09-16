using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoXR.Commands;

namespace VoXR.Tests.Runtime
{
    public class VoxrPendingCommandTests
    {
        GameObject _go;
        VoxrCommandRecogniser _recogniser;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPendingCommands");
            _recogniser = _go.AddComponent<VoxrCommandRecogniser>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        // -------- Fixtures --------

        static VoxrSlotDefinition[] MakeSlots()
        {
            return new[]
            {
                new VoxrSlotDefinition("target",
                    new[] { "hotel one", "hotel two", "alpha one" },
                    new Dictionary<string, string>
                    {
                        { "h one", "hotel one" },
                        { "h two", "hotel two" },
                    }),
                new VoxrSlotDefinition("weapon",
                    new[] { "missiles", "torpedoes" }),
            };
        }

        static VoxrCommandDefinition[] MakeCommands(
            bool allowPartial = false, bool requiresConfirm = false)
        {
            return new[]
            {
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[] { "launch", "{weapon}", "target", "{target}" },
                }, allowPartial, requiresConfirm),
                new VoxrCommandDefinition("cease_fire", new[]
                {
                    new[] { "cease", "fire" },
                }),
            };
        }

        void ConfigureSync(bool allowPartial = false, bool requiresConfirm = false)
        {
            _recogniser.Configure(MakeSlots(), MakeCommands(allowPartial, requiresConfirm));
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f; // Long timeout so tests control resolution
        }

        // ======== AllowPartialMatch Basics ========

        [Test]
        public void PartialMatch_WithoutFlag_Rejected()
        {
            ConfigureSync(allowPartial: false);

            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;
            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            // "launch missiles" is missing {target} — score below minScore
            _recogniser.InjectText("launch missiles");

            Assert.IsFalse(received.HasValue, "Partial match should be rejected without AllowPartialMatch");
            Assert.IsNotNull(unrecognised);
        }

        [Test]
        public void PartialMatch_WithFlag_EntersPending()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target");

            Assert.IsTrue(pending.HasValue, "Partial match should enter pending");
            Assert.AreEqual("launch_weapon", pending.Value.Intent);
            Assert.IsTrue(pending.Value.HasSlot("weapon"));
            Assert.AreEqual("missiles", pending.Value.GetSlot("weapon"));
            Assert.IsFalse(recognised.HasValue, "Should not fire OnCommandRecognised yet");
            Assert.IsTrue(_recogniser.HasPendingCommand);
        }

        [Test]
        public void PartialMatch_AboveGateButIncomplete_EntersPendingInsteadOfFiring()
        {
            // Issue #73's routing half. The pending path is entered from BELOW minScore, so a
            // slot-missing candidate that cleared the gate never reached it — allowPartialMatch
            // was silently inapplicable to exactly the commands that scored well enough to fire
            // incomplete.
            //
            // The candidate sits clear of the gate rather than on it (issue #76): eight required
            // elements, seven matched and the trailing {target} stranded, so (7 x 1 - 1) / 8 =
            // 0.75. On the gate value itself the routing assertion would fail open — a scoring
            // change that dropped the candidate below minScore would still route it to pending,
            // for the ordinary below-gate reason, and prove nothing about completeness.
            _recogniser.Configure(
                new[]
                {
                    new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two" }),
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
                                "{target}",
                            },
                        },
                        allowPartialMatch: true
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch all missiles from tube three at");

            Assert.IsFalse(
                recognised.HasValue,
                "an incomplete command must not fire above the gate"
            );
            Assert.IsTrue(
                pending.HasValue,
                "it goes to slot-fill, which is where it always belonged"
            );
            // The pending command carries the parse score through untouched, so this pins the
            // hand-derived 0.75 without a second parser: it is the candidate that was routed.
            Assert.AreEqual(
                6f / 8f,
                pending.Value.Score,
                0.001f,
                "the hand-derived score no longer holds — re-derive it and argue the new value"
            );
            Assert.GreaterOrEqual(
                pending.Value.Score,
                _recogniser.MinScore,
                "and it must clear the gate the recogniser is running, or completeness is not "
                    + "what routed it"
            );
            Assert.AreEqual("missiles", pending.Value.GetSlot("weapon"));
            Assert.AreEqual("three", pending.Value.GetSlot("tube"));
            Assert.IsTrue(_recogniser.HasPendingCommand);

            _recogniser.InjectText("hotel one");

            Assert.IsTrue(recognised.HasValue, "and the follow-up completes it");
            Assert.AreEqual("hotel one", recognised.Value.GetSlot("target"));
        }

        [Test]
        public void LeadingRequiredMiss_OpensNoPending()
        {
            // DR-8 of issue #124, and the one place where the leading-miss bar is visible on the
            // PENDING path rather than the firing one. It needs no special-casing in the pending
            // code — pending is fed by RESULTS, and a barred round produces none — but "no
            // special-casing" is not the same as "no behaviour change", and this is the change.
            //
            // "missiles target" against launch {weapon} target {target}: "launch" was never
            // spoken, {weapon} takes "missiles", the "target" literal matches, and {target} is
            // left unfilled. Two matched against two missed, so the admission rule lets it
            // through at 0.25 — and BEFORE this feature that was enough to route it, because
            // allowPartialMatch sends any incomplete candidate to slot-fill regardless of score.
            // The recogniser would open a pending command, and then ask the speaker to fill in
            // the argument of a command whose VERB they never uttered.
            //
            // Under the bar the parse yields nothing, so there is no candidate to hold open and
            // the utterance is simply unrecognised. This is why DR-8 refuses to route barred
            // candidates to slot-fill: there is no command to hold open.
            ConfigureSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("missiles target");

            Assert.IsFalse(recognised.HasValue, "nothing fires — the verb was never spoken");
            Assert.IsFalse(
                pending.HasValue,
                "and nothing is held open: a barred candidate is not a command to complete"
            );
            Assert.IsFalse(_recogniser.HasPendingCommand);
            Assert.AreEqual(
                "missiles target",
                unrecognised,
                "zero results reaches the recogniser as OnUnrecognisedSpeech, as it already did"
            );

            // Control on the same grammar: spoken with its verb and missing the same argument,
            // the command still routes to slot-fill exactly as before. The bar took the phantom,
            // not the feature.
            _recogniser.InjectText("launch missiles target");

            Assert.IsTrue(pending.HasValue, "a genuine incomplete command still opens a pending");
            Assert.IsTrue(_recogniser.HasPendingCommand);
            Assert.AreEqual("missiles", pending.Value.GetSlot("weapon"));
        }

        [Test]
        public void LeadingRequiredMiss_SlotAnchor_OpensNoPending()
        {
            // The SLOT half of DR-2, on the pending path — the square neither existing pin
            // covers, and the one architecture §10 calls the only place uniformity over element
            // type changes observable behaviour at all.
            // LeadingRequiredMiss_OpensNoPending directly above bars a LITERAL anchor
            // ("launch"); LeadingMiss_AppliesToASlotAnchor_NotOnlyALiteralOne
            // (VoxrCommandParserTests) bars a SLOT anchor, but at Parse level, where the §6.2
            // erratum leaves it nothing to say about firing: a winner carrying an unfilled
            // required slot is refused by the recogniser's completeness check REGARDLESS of
            // score, so a slot-anchored phantom could not have fired anyway, bar or no bar.
            //
            // What the erratum does NOT reach is allowPartialMatch, because there the
            // completeness check does not refuse the candidate — it ROUTES it, onto the
            // pending/slot-fill path. So slot anchor x allowPartialMatch x pending is the one
            // combination where DR-2's uniformity subtracts something the SPEAKER experiences:
            // not a command that fails to fire, but a question they are never asked.
            //
            // Before the bar this utterance opened a pending and asked WHICH TRACK to steer —
            // the argument of a command whose subject was never named, and a question the
            // speaker cannot answer sensibly because they did not ask it. DR-8: a barred
            // candidate is not a command to hold open.
            //
            // NOTE the deliberate difference from the two confirmation/disambiguation pins that
            // close the same issue: this one needs NO score floor, and building it as if it did
            // would misrepresent the path. The partial branch in VoxrCommandRecogniser routes on
            // `cmd.Score > 0f && AllowPartialMatch && unfilled.Length > 0` and consults no
            // threshold whatever — the literal-anchor pin above is let through at 0.25 — so
            // there is no gate here that could refuse the phantom ahead of the bar and make this
            // test pass for the wrong reason. The other two sit BELOW `cmd.Score < minScore` and
            // are built to clear it.
            _recogniser.Configure(
                new[]
                {
                    new VoxrSlotDefinition("track", new[] { "alpha one", "bravo two" }),
                    new VoxrSlotDefinition("bearing", new[] { "north east", "south west" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "steer_track",
                        new[] { new[] { "{track}", "come", "to", "{bearing}" } },
                        allowPartialMatch: true
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            // {track} is the anchor and it matched nothing: -1 for the missed required slot,
            // +1 each for "come", "to" and the filled {bearing}, over a frame worth 4 — so
            // 2/4 = 0.50, with one unfilled required slot to prompt about. Every input the
            // partial branch reads is satisfied, and only the bar stops it.
            _recogniser.InjectText("come to south west");

            Assert.IsFalse(recognised.HasValue, "nothing fires — no track was ever named");
            Assert.IsFalse(
                pending.HasValue,
                "and nothing is held open: the speaker is not asked to complete a command they "
                    + "never gave"
            );
            Assert.IsFalse(_recogniser.HasPendingCommand);
            Assert.AreEqual("come to south west", unrecognised);

            // The control, and what makes the silence above about POSITION rather than
            // arithmetic: the same grammar, the same single missed required element, the same
            // 2/4 = 0.50, the same one unfilled slot — the miss moved to the tail. This one
            // still opens a pending, so neither the score, nor the flag, nor the unfilled count
            // explains what happened above.
            _recogniser.InjectText("alpha one come to");

            Assert.IsTrue(pending.HasValue, "a genuine incomplete command still opens a pending");
            Assert.IsTrue(_recogniser.HasPendingCommand);
            Assert.AreEqual("alpha one", pending.Value.GetSlot("track"));
            Assert.AreEqual(
                0.5f,
                pending.Value.Score,
                0.001f,
                "the hand-derived score no longer holds — re-derive it and argue the new value"
            );
        }

        [Test]
        public void IncompleteNewCommand_DoesNotCancelALivePending()
        {
            // The Step 4 half of #73, and the reason the completeness term has to be read twice.
            // hasCompleteNewCommand cancels any live pending command outright. Once an incomplete
            // command stops firing, letting it still set that flag would take the user's
            // half-finished command away and put nothing at all in its place.
            //
            // set_burn deliberately does NOT opt into partial matching, so the incomplete second
            // utterance is rejected rather than entering pending itself — which is what isolates
            // this to the cancellation and keeps it from passing for the wrong reason.
            //
            // Its pattern is eight elements rather than five (issue #76) so the incomplete
            // candidate lands clear of the gate at (7 x 1 - 1) / 8 = 0.75. On the gate value
            // itself this test fails open: a scoring change that pushed the candidate below
            // minScore would still leave the pending command alive — rejected on score, never
            // reaching the completeness term that Step 4 exists to apply.
            var slots = new[]
            {
                new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two" }),
                new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                new VoxrSlotDefinition("burn_level", new[] { "coast", "hard burn" }),
            };
            var commands = new[]
            {
                new VoxrCommandDefinition(
                    "launch_weapon",
                    new[] { new[] { "launch", "{weapon}", "target", "{target}" } },
                    allowPartialMatch: true
                ),
                new VoxrCommandDefinition(
                    "set_burn",
                    new[]
                    {
                        new[] { "helm", "set", "burn", "to", "{burn_level}", "on", "my", "mark" },
                    }
                ),
            };

            // Nothing fires and nothing pends for set_burn, so no event carries its score —
            // pin it against the same grammar directly, or the assertions below cannot tell
            // "refused as incomplete" from "never cleared the gate".
            var probe = new VoxrCommandParser(slots, commands).Parse("helm set burn to on my mark");
            Assert.AreEqual(1, probe.Length);
            Assert.AreEqual("set_burn", probe[0].Command.Intent);
            Assert.AreEqual(
                6f / 8f,
                probe[0].Command.Score,
                0.001f,
                "the hand-derived score no longer holds — re-derive it and argue the new value"
            );
            Assert.GreaterOrEqual(
                probe[0].Command.Score,
                _recogniser.MinScore,
                "and it must clear the gate the recogniser is running"
            );
            Assert.IsFalse(probe[0].Command.HasSlot("burn_level"), "with {burn_level} stranded");

            _recogniser.Configure(slots, commands);
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand, "precondition: a pending command is live");

            int cancelledCount = 0;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandCancelled += _ => cancelledCount++;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("helm set burn to on my mark");

            Assert.IsFalse(recognised.HasValue, "the incomplete command must not fire");
            Assert.AreEqual(
                0,
                cancelledCount,
                "and must not evict a pending command it cannot replace"
            );
            Assert.IsTrue(_recogniser.HasPendingCommand);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            _recogniser.InjectText("hotel one");

            Assert.IsTrue(confirmed.HasValue, "the pending command is still there to be completed");
            Assert.AreEqual("hotel one", confirmed.Value.GetSlot("target"));
        }

        [Test]
        public void PartialMatch_AllSlotsFilled_FiresNormally()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target hotel one");

            Assert.IsFalse(pending.HasValue, "Fully matched should not enter pending");
            Assert.IsTrue(recognised.HasValue, "Should fire normally");
            Assert.AreEqual("launch_weapon", recognised.Value.Intent);
        }

        [Test]
        public void PartialMatch_FollowUp_FillsSlot()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // Follow-up fills the missing {target} slot
            _recogniser.InjectText("hotel one");

            Assert.IsTrue(confirmed.HasValue, "Follow-up should confirm pending");
            Assert.AreEqual("launch_weapon", confirmed.Value.Intent);
            Assert.AreEqual("hotel one", confirmed.Value.GetSlot("target"));
            Assert.AreEqual("missiles", confirmed.Value.GetSlot("weapon"));
            Assert.IsTrue(recognised.HasValue, "Should also fire OnCommandRecognised");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void PartialMatch_FollowUp_WrongSlotValue_StaysPending()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;

            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // Follow-up with unrecognised text
            _recogniser.InjectText("something random");

            Assert.IsFalse(confirmed.HasValue, "Random speech should not complete pending");
            Assert.IsTrue(_recogniser.HasPendingCommand, "Should still be pending");
        }

        [Test]
        public void PartialMatch_TimeoutCancel_FiresCancelled()
        {
            ConfigureSync(allowPartial: true);
            _recogniser.PendingTimeout = 0.01f; // Very short timeout

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // Simulate time passing via manual Update call
            // We need to wait for timeout — use reflection to set CreatedTime in the past
            ForceTimeoutNow();

            Assert.IsTrue(cancelled.HasValue, "Should fire OnCommandCancelled on timeout");
            Assert.AreEqual("launch_weapon", cancelled.Value.Intent);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void PartialMatch_TimeoutFireAsIs_FiresWithPartialSlots()
        {
            ConfigureSync(allowPartial: true);
            _recogniser.PendingTimeout = 0.01f;
            _recogniser.PendingTimeoutBehavior = VoxrPendingTimeoutBehavior.FireAsIs;

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            ForceTimeoutNow();

            Assert.IsTrue(confirmed.HasValue, "Should fire OnCommandConfirmed");
            Assert.IsTrue(recognised.HasValue, "Should fire OnCommandRecognised");
            Assert.AreEqual("missiles", confirmed.Value.GetSlot("weapon"));
            Assert.IsFalse(confirmed.Value.HasSlot("target"), "Target should be unfilled");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        // ======== RequiresConfirmation Basics ========

        [Test]
        public void RequiresConfirmation_FullMatch_EntersPending()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target hotel one");

            Assert.IsTrue(pending.HasValue, "Should enter pending for confirmation");
            Assert.AreEqual("launch_weapon", pending.Value.Intent);
            Assert.IsFalse(recognised.HasValue, "Should not fire yet");
            Assert.IsTrue(_recogniser.HasPendingCommand);
        }

        [Test]
        public void RequiresConfirmation_Confirm_Fires()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            _recogniser.InjectText("confirm");

            Assert.IsTrue(confirmed.HasValue, "Should fire OnCommandConfirmed");
            Assert.IsTrue(recognised.HasValue, "Should fire OnCommandRecognised");
            Assert.AreEqual("launch_weapon", confirmed.Value.Intent);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void RequiresConfirmation_Cancel_Cancels()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            _recogniser.InjectText("cancel");

            Assert.IsTrue(cancelled.HasValue, "Should fire OnCommandCancelled");
            Assert.AreEqual("launch_weapon", cancelled.Value.Intent);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void RequiresConfirmation_AffirmativeConfirms()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            _recogniser.InjectText("affirmative");

            Assert.IsTrue(confirmed.HasValue, "Synonym 'affirmative' should confirm");
        }

        [Test]
        public void RequiresConfirmation_BelayThat_Cancels()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            _recogniser.InjectText("belay that");

            Assert.IsTrue(cancelled.HasValue, "Multi-word 'belay that' should cancel");
        }

        [Test]
        public void RequiresConfirmation_CustomVocabulary()
        {
            ConfigureSync(requiresConfirm: true);
            _recogniser.ConfirmVocabulary = new[] { "execute" };
            _recogniser.CancelVocabulary = new[] { "stand down" };

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            // Default "confirm" should NOT work with custom vocabulary
            _recogniser.InjectText("launch missiles target hotel one");
            _recogniser.InjectText("confirm");
            Assert.IsFalse(confirmed.HasValue, "Default 'confirm' should not work with custom vocab");

            // Custom "execute" should work
            _recogniser.CancelPendingCommand(); // Reset
            cancelled = null;
            _recogniser.InjectText("launch missiles target hotel one");
            _recogniser.InjectText("execute");
            Assert.IsTrue(confirmed.HasValue, "Custom 'execute' should confirm");
        }

        [Test]
        public void LeadingRequiredMiss_RequiresConfirmation_AsksNothing()
        {
            // The second of the three downstream subtractions the #124 CHANGELOG entry
            // publishes: requiresConfirmation "no longer asks you to confirm an action nobody
            // requested". True, and until now guarded by nothing (issue #128). It follows from
            // the same mechanism LeadingRequiredMiss_OpensNoPending pins — a barred round emits
            // no result, and every pending route is fed by results — but "follows from" is not
            // "is tested", and this is the most visible of the three: a modal confirmation
            // prompt for a destructive order, raised by a fragment of overheard speech.
            //
            // THREE required elements, not two, and that is the whole design of the fixture. The
            // confirmation branch sits BELOW `cmd.Score < minScore` in the recogniser's result
            // loop, so the obvious two-element fixture heard with its first word dropped scores
            // (2-1)/2 = 0.50, is discarded by the 0.60 gate before confirmation is ever
            // considered, and would pass this test with the bar DELETED — a decorative pin,
            // which is worse than none. At three the phantom scores (3-1)/3 = 0.667, clears the
            // gate with room, and the bar is the only thing left that can refuse it.
            //
            // No slots either, so IsIncomplete cannot be what refuses it, and allowPartialMatch
            // is off — AwaitingConfirmation is the only pending this grammar can open, so the
            // OnCommandPending assertions below can only be about the confirmation prompt.
            _recogniser.Configure(
                Array.Empty<VoxrSlotDefinition>(),
                new[]
                {
                    new VoxrCommandDefinition(
                        "purge_tanks",
                        new[] { new[] { "purge", "the", "tanks" } },
                        allowPartialMatch: false,
                        requiresConfirmation: true
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("the tanks");

            Assert.IsFalse(pending.HasValue, "nobody is asked to confirm a purge nobody ordered");
            Assert.IsFalse(_recogniser.HasPendingCommand);
            Assert.IsFalse(recognised.HasValue, "and nothing fires either");
            Assert.AreEqual("the tanks", unrecognised);

            // The control, and the reason this pin is not decorative: the SAME grammar, the same
            // count of missed required elements, the same (3-1)/3 = 0.667 — one interior word
            // dropped instead of the anchor — and it still asks. The gate is demonstrably clear
            // at this score, so position is the only difference between the prompt and the
            // silence above.
            _recogniser.InjectText("purge tanks");

            Assert.IsTrue(pending.HasValue, "a real order is still confirmed");
            Assert.AreEqual("purge_tanks", pending.Value.Intent);
            Assert.IsTrue(_recogniser.HasPendingCommand);
            Assert.IsFalse(recognised.HasValue, "…confirmed rather than fired, as ever");
            Assert.AreEqual(2f / 3f, pending.Value.Score, 0.001f);
            Assert.GreaterOrEqual(
                pending.Value.Score,
                _recogniser.MinScore,
                "and it clears the gate the recogniser is actually running, so the barred twin "
                    + "above was refused by the bar and not by the threshold"
            );
        }

        // ======== Combined AllowPartialMatch + RequiresConfirmation ========

        [Test]
        public void Combined_PartialThenFollowUpThenConfirm()
        {
            ConfigureSync(allowPartial: true, requiresConfirm: true);

            var events = new List<string>();
            _recogniser.OnCommandPending += cmd => events.Add($"pending:{cmd.Intent}");
            _recogniser.OnCommandConfirmed += cmd => events.Add($"confirmed:{cmd.Intent}");
            _recogniser.OnCommandRecognised += cmd => events.Add($"recognised:{cmd.Intent}");

            // Step 1: Partial match enters pending
            _recogniser.InjectText("launch missiles target");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("pending:launch_weapon", events[0]);

            // Step 2: Follow-up fills slot — but RequiresConfirmation means it re-enters pending
            _recogniser.InjectText("hotel one");
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("pending:launch_weapon", events[1]);
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // Step 3: Confirm fires
            _recogniser.InjectText("confirm");
            Assert.AreEqual(4, events.Count);
            Assert.AreEqual("confirmed:launch_weapon", events[2]);
            Assert.AreEqual("recognised:launch_weapon", events[3]);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        // ======== Arbitration ========

        [Test]
        public void Pending_NewCompleteCommand_PreemptsPending()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            // launch_weapon enters pending (requires confirmation)
            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // "cease fire" is a complete command — should preempt pending
            _recogniser.InjectText("cease fire");

            Assert.IsTrue(cancelled.HasValue, "Pending should be cancelled");
            Assert.AreEqual("launch_weapon", cancelled.Value.Intent);
            Assert.IsTrue(recognised.HasValue, "New command should fire");
            Assert.AreEqual("cease_fire", recognised.Value.Intent);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void Pending_FollowUpOnly_FollowUpWins()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;

            _recogniser.InjectText("launch missiles target");
            _recogniser.InjectText("hotel one");

            Assert.IsTrue(confirmed.HasValue, "Follow-up should complete pending");
            Assert.AreEqual("hotel one", confirmed.Value.GetSlot("target"));
        }

        [Test]
        public void ConfirmCancel_NoPending_PassesThrough()
        {
            ConfigureSync();

            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("confirm");

            Assert.IsNotNull(unrecognised,
                "'confirm' with no pending should pass through as unrecognised");
        }

        // ======== Deferred Grammar Rebuild ========

        [Test]
        public void RebuildGrammar_DuringPending_Deferred()
        {
            ConfigureSync(requiresConfirm: true);

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            string grammarBefore = _recogniser.TestGrammarJson;

            // This should be deferred
            _recogniser.RebuildGrammar();

            string grammarDuring = _recogniser.TestGrammarJson;
            Assert.AreEqual(grammarBefore, grammarDuring,
                "Grammar should not change during pending state");
        }

        [Test]
        public void RebuildParser_DuringPending_ExecutesImmediately()
        {
            ConfigureSync(requiresConfirm: true);

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // RebuildParser should not throw and should execute
            Assert.DoesNotThrow(() => _recogniser.RebuildParser());
        }

        [Test]
        public void DeferredRebuild_DrainsAfterPendingResolves()
        {
            ConfigureSync(requiresConfirm: true);

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            _recogniser.RebuildGrammar(); // Deferred
            Assert.IsTrue(_recogniser.TestGrammarRebuildDeferred, "Should be deferred");

            _recogniser.InjectText("confirm"); // Resolves pending

            Assert.IsFalse(_recogniser.TestGrammarRebuildDeferred,
                "Deferred flag should be cleared after pending resolves");
        }

        // ======== Public API ========

        [Test]
        public void CancelPendingCommand_Cancels()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            _recogniser.CancelPendingCommand();

            Assert.IsTrue(cancelled.HasValue, "Should fire OnCommandCancelled");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void CancelPendingCommand_NoPending_NoOp()
        {
            ConfigureSync();

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.CancelPendingCommand();

            Assert.IsFalse(cancelled.HasValue, "Should not fire when no pending");
        }

        [Test]
        public void HasPendingCommand_Property()
        {
            ConfigureSync(requiresConfirm: true);

            Assert.IsFalse(_recogniser.HasPendingCommand);

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            _recogniser.InjectText("confirm");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void PendingCommand_Property()
        {
            ConfigureSync(requiresConfirm: true);

            Assert.IsNull(_recogniser.PendingCommand);

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsNotNull(_recogniser.PendingCommand);
            Assert.AreEqual("launch_weapon", _recogniser.PendingCommand.Value.Intent);

            _recogniser.InjectText("cancel");
            Assert.IsNull(_recogniser.PendingCommand);
        }

        // ======== Lifecycle ========

        [Test]
        public void SetActiveSets_CancelsPending()
        {
            var slots = MakeSlots();
            var sets = new[]
            {
                new VoxrCommandSet("combat", MakeCommands(requiresConfirm: true)),
            };

            _recogniser.Configure(slots, sets);
            _recogniser.SetActiveSets("combat");
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            _recogniser.SetActiveSets("combat");

            Assert.IsTrue(cancelled.HasValue, "SetActiveSets should cancel pending");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void Configure_CancelsPending()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // Reconfigure
            _recogniser.Configure(MakeSlots(), MakeCommands());

            Assert.IsTrue(cancelled.HasValue, "Configure should cancel pending");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        // ======== Backward Compatibility ========

        [Test]
        public void DefaultDefinition_BothFlagsFalse()
        {
            var def = new VoxrCommandDefinition("test", new[] { new[] { "test" } });
            Assert.IsFalse(def.AllowPartialMatch);
            Assert.IsFalse(def.RequiresConfirmation);
        }

        [Test]
        public void NormalCommand_UnchangedBehavior()
        {
            ConfigureSync(); // No flags

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            _recogniser.InjectText("launch missiles target hotel one");

            Assert.IsTrue(recognised.HasValue, "Normal command should fire as before");
            Assert.IsFalse(pending.HasValue, "Normal command should not enter pending");
        }

        [Test]
        public void CeaseFireCommand_StillFiresNormally()
        {
            ConfigureSync(allowPartial: true); // Only launch_weapon has partial

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("cease fire");

            Assert.IsTrue(recognised.HasValue);
            Assert.AreEqual("cease_fire", recognised.Value.Intent);
        }

        // ======== Edge Cases ========

        [Test]
        public void NewPending_CancelsExistingPending()
        {
            // Use two commands that both allow partial
            var slots = MakeSlots();
            var commands = new[]
            {
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[] { "launch", "{weapon}", "target", "{target}" },
                }, allowPartialMatch: true),
                new VoxrCommandDefinition("engage_target", new[]
                {
                    new[] { "engage", "{target}" },
                }, allowPartialMatch: true),
            };

            _recogniser.Configure(slots, commands);
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            var cancelledIntents = new List<string>();
            _recogniser.OnCommandCancelled += cmd => cancelledIntents.Add(cmd.Intent);

            var pendingIntents = new List<string>();
            _recogniser.OnCommandPending += cmd => pendingIntents.Add(cmd.Intent);

            // First partial enters pending
            _recogniser.InjectText("launch missiles target");
            Assert.AreEqual(1, pendingIntents.Count);
            Assert.AreEqual("launch_weapon", pendingIntents[0]);

            // Second partial (different weapon) replaces the first pending
            _recogniser.InjectText("launch torpedoes target");
            Assert.AreEqual(2, pendingIntents.Count);
            Assert.AreEqual("launch_weapon", pendingIntents[1]);
            Assert.AreEqual(1, cancelledIntents.Count, "First pending should be cancelled");
            Assert.AreEqual("launch_weapon", cancelledIntents[0]);
        }

        [Test]
        public void FollowUp_WithAlias_Works()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;

            _recogniser.InjectText("launch missiles target");
            _recogniser.InjectText("h one");

            Assert.IsTrue(confirmed.HasValue, "Alias 'h one' should fill target slot");
            Assert.AreEqual("hotel one", confirmed.Value.GetSlot("target"));
        }

        [Test]
        public void MatchedPatternIndex_PopulatedCorrectly()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            _recogniser.InjectText("launch missiles target");

            Assert.IsTrue(pending.HasValue);
            Assert.AreEqual(0, pending.Value.MatchedPatternIndex,
                "Should match pattern index 0");
        }

        // -------- Admission and the partial path (issue #65, DR-7) --------

        [Test]
        public void PartialMatch_SparseFragment_DoesNotArmPending()
        {
            // The partial path is gated on `Score > 0f`, not on minScore — so it is the one
            // consumer keyed directly to the floor issue #65 §5.1 moved candidates across.
            // Zeroing the miss penalty put fragments over that floor, and each one arriving
            // here arms a slot-fill prompt and cancels any pending already in flight. DR-7 is
            // what keeps them out, and nothing else in the suite exercises this path.
            //
            // Seven required elements. "set burn now" matches three (set, burn, now) and
            // misses four (the, level, to, and the {burn_level} slot), so DR-7 refuses it.
            // Without the rule it scores (1 + 0 + 1 + 0 + 0 - 1 + 1) / 7 = 0.286 — under
            // minScore, above zero, with an unfilled required slot: precisely the shape that
            // enters pending.
            _recogniser.Configure(
                new[] { new VoxrSlotDefinition("burn_level", new[] { "coast", "hard burn" }) },
                new[]
                {
                    new VoxrCommandDefinition(
                        "set_burn",
                        new[]
                        {
                            new[] { "set", "the", "burn", "level", "to", "{burn_level}", "now" },
                        },
                        allowPartialMatch: true
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            int pendingCount = 0;
            string unrecognised = null;
            _recogniser.OnCommandPending += _ => pendingCount++;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("set burn now");

            Assert.AreEqual(0, pendingCount, "a fragment must not arm a slot-fill prompt");
            Assert.IsFalse(_recogniser.HasPendingCommand);
            Assert.AreEqual("set burn now", unrecognised);

            // Control, so the refusal above cannot be a mis-wired fixture: one more matched
            // literal and the same missed slot gives four matched against three missed, which
            // DR-7 admits, at (4 - 1) / 7 = 0.429 — still under minScore, so it enters pending
            // exactly as the partial path intends.
            _recogniser.InjectText("set the burn now");

            Assert.AreEqual(1, pendingCount, "a candidate with more evidence than gaps still arms");
            Assert.IsTrue(_recogniser.HasPendingCommand);
        }

        // -------- The follow-up exit and the two opt-in exits (issue #77) --------

        // Every test below needs a pending with TWO unfilled required slots, which is what makes
        // the follow-up exit's gap reachable at all: with one unfilled slot a follow-up either
        // completes the command or fills nothing, so a result that is still incomplete never
        // appears. Nothing else in the suite constructs this shape.
        //
        // "launch at on my mark" matches five required literals and strands both slots — five
        // matched against two missed, which DR-7 admits. Incompleteness is what routes it to
        // pending; it also happens to land below minScore, but that term is neither necessary
        // nor sufficient here, so do not "fix" this fixture by adjusting its score.
        void ConfigureTwoSlotPartial(bool requiresConfirm = false)
        {
            _recogniser.Configure(
                new[]
                {
                    new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                    new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[]
                        {
                            new[] { "launch", "{weapon}", "at", "{target}", "on", "my", "mark" },
                        },
                        allowPartialMatch: true,
                        requiresConfirmation: requiresConfirm
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;
        }

        void AssertBothArgumentsPending()
        {
            Assert.IsTrue(_recogniser.HasPendingCommand, "precondition: a pending command is live");
            Assert.IsFalse(
                _recogniser.PendingCommand.Value.HasSlot("weapon"),
                "precondition: {weapon} is unfilled"
            );
            Assert.IsFalse(
                _recogniser.PendingCommand.Value.HasSlot("target"),
                "precondition: and so is {target} — two unfilled required slots, not one"
            );
        }

        [Test]
        public void FollowUp_FillingOnlyOneOfTwoRequiredSlots_StaysPending()
        {
            // Issue #77 case 1. TryFollowUpSlotFill walks the unfilled slots in order, breaks at
            // the first it cannot fill, and returns a command as soon as ONE new slot is filled.
            // With two unfilled it therefore hands Step 5 a command still missing an argument —
            // and Step 5 fired it, which is precisely the shape #73 refuses on the flush path,
            // reached by the path #73 routes those commands to.
            ConfigureTwoSlotPartial();

            // The whole payload, not just the intent: the re-fire's argument is what a prompt
            // reads, and it is a separate surface from the handler state the assertions below
            // check. Recording only the intent would let a regression that re-announced the
            // STALE pre-fill command pass every assertion in this file.
            var pendingPayloads = new List<VoxrCommand>();
            _recogniser.OnCommandPending += pendingPayloads.Add;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            int cancelledCount = 0;
            _recogniser.OnCommandCancelled += _ => cancelledCount++;

            _recogniser.InjectText("launch at on my mark");
            AssertBothArgumentsPending();
            Assert.AreEqual(1, pendingPayloads.Count);

            // Fills {weapon} and nothing else — {target} has no candidate in this utterance.
            _recogniser.InjectText("missiles");

            Assert.IsFalse(
                recognised.HasValue,
                "a command still missing an argument must not fire out of the slot-fill exit"
            );
            Assert.IsFalse(confirmed.HasValue);
            Assert.AreEqual(0, cancelledCount, "and the half-finished command is not discarded");

            // The refusal has to keep the fill, or it is a refusal to make progress: without this
            // the test would pass just as well on a slot-fill that matched nothing at all.
            Assert.IsTrue(_recogniser.HasPendingCommand, "the pending stays live");
            Assert.AreEqual(
                "missiles",
                _recogniser.PendingCommand.Value.GetSlot("weapon"),
                "carrying the slot this utterance did fill"
            );
            Assert.IsFalse(
                _recogniser.PendingCommand.Value.HasSlot("target"),
                "and still waiting on the one it did not"
            );
            Assert.AreEqual(
                2,
                pendingPayloads.Count,
                "and it re-announces itself, so a prompt can show what is still missing"
            );
            Assert.AreEqual(
                "missiles",
                pendingPayloads[1].GetSlot("weapon"),
                "the re-announcement carries the UPDATED command, not the pre-fill one — a "
                    + "prompt reads this payload, so re-announcing the stale command would name "
                    + "a slot the user has already supplied"
            );
            Assert.IsFalse(
                pendingPayloads[1].HasSlot("target"),
                "and still reports the slot that is genuinely outstanding"
            );

            // The remaining slot arrives in a third utterance, and only now does it fire.
            _recogniser.InjectText("hotel one");

            Assert.IsTrue(confirmed.HasValue, "the completed command fires");
            Assert.AreEqual("missiles", confirmed.Value.GetSlot("weapon"));
            Assert.AreEqual(
                "hotel one",
                confirmed.Value.GetSlot("target"),
                "with the slot filled two utterances earlier still attached"
            );
            Assert.IsTrue(recognised.HasValue);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void FollowUp_AcrossTwoUtterances_StillReachesTheConfirmationGate()
        {
            // AdvanceSlotFill carries `Reason` over so the pending stays a PartialMatch. That
            // field is load-bearing in exactly one place — Complete's re-entry guard
            // `RequiresConfirmation && Reason == PartialMatch` — and nothing reached it before:
            // every other test that fills a slot strands only ONE, so it goes straight to
            // Complete without passing through AdvanceSlotFill. A regression writing
            // AwaitingConfirmation here would leave the rest of this file green while firing a
            // requiresConfirmation command with its confirmation gate skipped.
            //
            // This is also the path the fix repaired rather than merely guarded: before #77,
            // Complete re-entered on the FIRST fill with UnfilledSlots emptied, which made every
            // further fill a no-op and stranded {target} permanently.
            ConfigureTwoSlotPartial(requiresConfirm: true);

            var events = new List<string>();
            _recogniser.OnCommandPending += cmd => events.Add($"pending:{cmd.Intent}");
            _recogniser.OnCommandConfirmed += cmd => events.Add($"confirmed:{cmd.Intent}");
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch at on my mark");
            AssertBothArgumentsPending();

            _recogniser.InjectText("missiles");
            Assert.IsFalse(recognised.HasValue, "a partial fill does not fire");
            Assert.AreEqual(
                "missiles",
                _recogniser.PendingCommand.Value.GetSlot("weapon"),
                "and the second slot is still fillable — the pre-#77 bug emptied UnfilledSlots "
                    + "here and stranded {target} for good"
            );

            _recogniser.InjectText("hotel one");

            Assert.IsFalse(
                recognised.HasValue,
                "the COMPLETING fill must not fire either — this command requires confirmation"
            );
            Assert.IsTrue(_recogniser.HasPendingCommand, "it re-enters pending for confirmation");
            Assert.AreEqual(
                "hotel one",
                _recogniser.PendingCommand.Value.GetSlot("target"),
                "carrying both slots into the confirmation stage"
            );

            _recogniser.InjectText("confirm");

            Assert.IsTrue(recognised.HasValue, "and only the confirm phrase fires it");
            Assert.AreEqual("missiles", recognised.Value.GetSlot("weapon"));
            Assert.AreEqual("hotel one", recognised.Value.GetSlot("target"));
            Assert.IsFalse(_recogniser.HasPendingCommand);
            CollectionAssert.AreEqual(
                new[]
                {
                    "pending:launch_weapon", // entered, both slots absent
                    "pending:launch_weapon", // re-announced after {weapon} filled
                    "pending:launch_weapon", // re-entered for confirmation once complete
                    "confirmed:launch_weapon",
                },
                events
            );
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator FollowUp_PartialFill_RestartsThePendingTimeoutWindow()
        {
            // pendingTimeout measures how long the command waits for the speaker, so a fill
            // restarts it: the default 5 s otherwise has to cover every utterance of a multi-slot
            // exchange, and a speaker answering one slot at a time would be cut off mid-answer.
            // No test could observe this before — every timeout test goes through
            // TestForceTimeoutNow, which overwrites CreatedTime outright before the only check
            // that reads it. Read the field directly instead.
            ConfigureTwoSlotPartial();

            _recogniser.InjectText("launch at on my mark");
            AssertBothArgumentsPending();
            float createdOnEntry = _recogniser.EditorPendingCommand.Value.CreatedTime;

            yield return null;

            // Without this the test cannot tell "restarted" from "carried over" — both would read
            // back the same value — so it would pass no matter which the code did.
            Assert.Greater(
                Time.time,
                createdOnEntry,
                "precondition: the clock advanced between entry and fill"
            );

            _recogniser.InjectText("missiles");

            Assert.IsTrue(_recogniser.HasPendingCommand, "precondition: the fill kept it alive");
            Assert.Greater(
                _recogniser.EditorPendingCommand.Value.CreatedTime,
                createdOnEntry,
                "answering buys another window — what bounds the pending is silence, not the "
                    + "elapsed time since it was first armed"
            );
        }

        [UnityTest]
        public IEnumerator CompletingFill_RestartsTheWindowForConfirmation()
        {
            // The same rule on the other re-entry. A command that reaches its confirmation stage
            // by being filled slot-by-slot must not arrive there with less time to confirm than
            // one that was complete when it was first heard.
            ConfigureTwoSlotPartial(requiresConfirm: true);

            _recogniser.InjectText("launch at on my mark");
            _recogniser.InjectText("missiles");
            Assert.IsTrue(_recogniser.HasPendingCommand, "precondition: still filling");
            float createdOnFill = _recogniser.EditorPendingCommand.Value.CreatedTime;

            yield return null;

            Assert.Greater(
                Time.time,
                createdOnFill,
                "precondition: the clock advanced before the completing fill"
            );

            _recogniser.InjectText("hotel one");

            Assert.IsTrue(
                _recogniser.HasPendingCommand,
                "precondition: complete now, so it re-entered pending for confirmation"
            );
            Assert.Greater(
                _recogniser.EditorPendingCommand.Value.CreatedTime,
                createdOnFill,
                "confirmation is a fresh question and gets a fresh window"
            );
        }
#endif

        [Test]
        public void ConfirmVocabulary_OnAPartialMatchPending_FiresAsIs_ByDesign()
        {
            // Issue #77 case 2, ruled deliberate and documented in
            // Documentation~/command-recognition.md rather than fixed. A confirm phrase resolves
            // the pending at Step 1, before any completeness test, so it fires the command with
            // whatever is filled — here nothing at all. Pinned because the ruling is the only
            // thing separating it from case 1 above: opting into allowPartialMatch is what
            // authorises it, and a handler for such a command must tolerate absent arguments.
            ConfigureTwoSlotPartial();

            _recogniser.InjectText("launch at on my mark");
            AssertBothArgumentsPending();

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;

            _recogniser.InjectText("confirm");

            Assert.IsTrue(confirmed.HasValue, "confirming a partial match fires it as-is");
            Assert.IsFalse(confirmed.Value.HasSlot("weapon"), "with {weapon} absent");
            Assert.IsFalse(confirmed.Value.HasSlot("target"), "and {target} absent");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void TimeoutFireAsIs_OnATwoSlotPartialMatchPending_FiresAsIs_ByDesign()
        {
            // Issue #77 case 3, ruled deliberate for the same reason and documented alongside it.
            // PartialMatch_TimeoutFireAsIs_FiresWithPartialSlots already covers one unfilled slot;
            // this pins the shape the issue named as untested, where the command reaches the
            // handler missing every argument it has.
            ConfigureTwoSlotPartial();
            _recogniser.PendingTimeoutBehavior = VoxrPendingTimeoutBehavior.FireAsIs;

            _recogniser.InjectText("launch at on my mark");
            AssertBothArgumentsPending();

            VoxrCommand? confirmed = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            ForceTimeoutNow();

            Assert.IsTrue(confirmed.HasValue, "FireAsIs fires it as-is");
            Assert.IsTrue(recognised.HasValue);
            Assert.IsFalse(confirmed.Value.HasSlot("weapon"), "with {weapon} absent");
            Assert.IsFalse(confirmed.Value.HasSlot("target"), "and {target} absent");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void Timeout_DefaultCancel_OnAPartlyFilledPending_Cancels()
        {
            // The other side of the #77 fix. Keeping the pending alive means an unanswered
            // slot-fill now reaches the timeout instead of firing on the follow-up, so the
            // default behaviour has to be the one that discards it — the #73 stance that firing
            // nothing beats firing a command whose argument the handler never receives.
            ConfigureTwoSlotPartial();

            _recogniser.InjectText("launch at on my mark");
            _recogniser.InjectText("missiles");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            ForceTimeoutNow();

            Assert.IsTrue(cancelled.HasValue, "the partly filled command is discarded");
            Assert.AreEqual(
                "missiles",
                cancelled.Value.GetSlot("weapon"),
                "and reports what it had got as far as filling"
            );
            Assert.IsFalse(recognised.HasValue, "nothing fires");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        // ======== AwaitingDisambiguation — the third reason (issue #74 item 3) ========
        //
        // Extends this file's reason × outcome matrix rather than starting a new one, which is
        // itself the acceptance check for DR-4's argument: widening VoxrPendingCommand beat
        // adding a parallel VoxrAmbiguousPending, and the evidence is that timeout, cancel,
        // re-entry and disable-safety are solved once here rather than twice.

        void ConfigureAmbiguous()
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("differ only at element 3")
            );

            // Before Configure: the flag is frozen into the parser there, and setting it after
            // is invisible in the Editor because the parser records ties whenever the flag is set
            // OR UNITY_EDITOR is defined.
            _recogniser.DisambiguateSiblingTies = true;
            _recogniser.Configure(
                new[] { new VoxrSlotDefinition("ship", new[] { "alpha" }) },
                new[]
                {
                    new VoxrCommandDefinition(
                        "set_mode",
                        new[] { new[] { "set", "{ship}", "mode", "on" } }
                    ),
                    new VoxrCommandDefinition(
                        "set_level",
                        new[] { new[] { "set", "{ship}", "level", "on" } }
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;
        }

        [Test]
        public void TimeoutFireAsIs_OnADisambiguationPending_Cancels_ByDesign()
        {
            // DR-6, and the argument is semantic rather than a preference. FireAsIs means "the
            // intent is known, fire it with the slots I have" — under ambiguity the INTENT
            // itself is unknown, which is a different situation wearing the same flag. Firing
            // the first-registered after a pause coin-flips anyway, merely later, which is
            // incoherent with an integrator who opted in specifically to stop coin-flipping.
            ConfigureAmbiguous();
            _recogniser.PendingTimeout = 0.01f;
            _recogniser.PendingTimeoutBehavior = VoxrPendingTimeoutBehavior.FireAsIs;

            VoxrCommand? cancelled = null;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("set alpha on");
            Assert.IsTrue(_recogniser.HasPendingCommand, "the question was asked");

            ForceTimeoutNow();

            Assert.IsTrue(cancelled.HasValue, "an unanswered ambiguity is abandoned");
            Assert.IsFalse(recognised.HasValue, "and fires nothing, even under FireAsIs");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void TimeoutFireAsIs_OnAConfirmationPending_StillFires()
        {
            // The control for the test above: FireAsIs is untouched under the other reasons, so
            // the degrade is scoped to the one situation DR-6 argues about.
            ConfigureSync(requiresConfirm: true);
            _recogniser.PendingTimeout = 0.01f;
            _recogniser.PendingTimeoutBehavior = VoxrPendingTimeoutBehavior.FireAsIs;

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            ForceTimeoutNow();

            Assert.IsTrue(recognised.HasValue, "as it has always done");
            Assert.AreEqual("launch_weapon", recognised.Value.Intent);
        }

        [Test]
        public void TimeoutCancel_OnADisambiguationPending_Cancels()
        {
            ConfigureAmbiguous();
            _recogniser.PendingTimeout = 0.01f;
            _recogniser.PendingTimeoutBehavior = VoxrPendingTimeoutBehavior.Cancel;

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("set alpha on");
            ForceTimeoutNow();

            Assert.IsTrue(cancelled.HasValue);
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        [Test]
        public void Disambiguation_PendingHasNoUnfilledSlots_SoSlotFillNeverClaimsIt()
        {
            // §4.8 pinned rather than trusted. The reason it is safe is a two-step argument
            // through issue #73 — a command missing a required argument is routed to PartialMatch
            // before the fire path, so anything that reached the tie was complete — and a future
            // change to either end could break it silently. TryFollowUpSlotFill returns null on
            // an empty list, so an empty list is what has to hold.
            ConfigureAmbiguous();

            _recogniser.InjectText("set alpha on");

            var pending = _recogniser.EditorPendingCommand;
            Assert.IsTrue(pending.HasValue);
            Assert.AreEqual(VoxrPendingReason.AwaitingDisambiguation, pending.Value.Reason);
            Assert.IsTrue(
                pending.Value.UnfilledSlots == null || pending.Value.UnfilledSlots.Length == 0,
                "so the follow-up slot-fill path declines it and the choice arm gets the answer"
            );
        }

        [Test]
        public void Disambiguation_Reconfigure_CancelsItLikeAnyOtherPending()
        {
            // The DR-4 dividend, asserted: none of Cancel, the reconfigure cancels, or the
            // disable path gained a reason branch, and this is what says so from outside.
            ConfigureAmbiguous();

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("set alpha on");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            _recogniser.Configure(MakeSlots(), MakeCommands());

            Assert.IsTrue(cancelled.HasValue, "reconfiguring abandons the question");
            Assert.IsFalse(_recogniser.HasPendingCommand);
        }

        // ======== Issue #113: a non-positive follow-up re-score ========

        // Two definitions registered under one intent. Nothing in the package rejects that, and
        // it is what makes the shape reachable: the two halves of the follow-up fire path resolve
        // the intent differently. IsIncomplete goes through CommandSetManager's dictionary, which
        // BuildLookup fills last-write-wins, so it reads the SHORT definition and calls the
        // merged command complete. ScoreFollowUp scans the parser's command array and breaks on
        // the first match, so it re-scores against the LONG one and charges the command for five
        // required slots the matched pattern never had.
        static VoxrSlotDefinition[] MakeDuplicateIntentSlots()
        {
            return new[]
            {
                new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two" }),
                new VoxrSlotDefinition("fuse", new[] { "impact" }),
                new VoxrSlotDefinition("spread", new[] { "wide" }),
                new VoxrSlotDefinition("yield", new[] { "low" }),
                new VoxrSlotDefinition("bearing", new[] { "north" }),
                new VoxrSlotDefinition("altitude", new[] { "high" }),
            };
        }

        static VoxrCommandDefinition[] MakeDuplicateIntentCommands()
        {
            return new[]
            {
                // First in the array — the one ScoreFollowUp resolves. It scores -3/9 on
                // "launch missiles target" and so never wins the parse; its only effect on the
                // run is to supply the re-score's pattern.
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[]
                    {
                        "launch", "{weapon}", "target", "{target}",
                        "{fuse}", "{spread}", "{yield}", "{bearing}", "{altitude}",
                    },
                }, allowPartialMatch: true),
                // Last in the array — the one the dictionary keeps, and the pattern the parse
                // actually matches.
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[] { "launch", "{weapon}", "target", "{target}" },
                }, allowPartialMatch: true),
            };
        }

        // These fixtures register two definitions under one intent deliberately — that IS the
        // divergence #113 came out of — and issue #120 now reports it at construction. So the
        // warning is part of what they assert rather than incidental noise.
        static void ExpectDuplicateIntentWarning()
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "Intent 'launch_weapon' is registered by 2 command definitions"
                )
            );
        }

        void ConfigureDuplicateIntent()
        {
            ExpectDuplicateIntentWarning();
            _recogniser.Configure(MakeDuplicateIntentSlots(), MakeDuplicateIntentCommands());
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;
        }

        // Same divergence, but the short definition keeps a second required slot, so the
        // follow-up fill is PARTIAL. This is the shape the floor must NOT refuse: issue #77's
        // re-arm is how a multi-slot exchange advances at all.
        static VoxrCommandDefinition[] MakePartialFillDuplicateIntentCommands()
        {
            return new[]
            {
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[]
                    {
                        "launch", "{weapon}", "target", "{target}",
                        "{fuse}", "{spread}", "{yield}", "{bearing}", "{altitude}",
                    },
                }, allowPartialMatch: true),
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[] { "launch", "{weapon}", "target", "{target}", "{fuse}" },
                }, allowPartialMatch: true),
            };
        }

        void ConfigurePartialFillDuplicateIntent()
        {
            ExpectDuplicateIntentWarning();
            _recogniser.Configure(
                MakeDuplicateIntentSlots(), MakePartialFillDuplicateIntentCommands());
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;
        }

        [Test]
        public void FollowUpFill_PartialAndNonPositive_StillProgressesAndCanComplete()
        {
            // The floor sits BELOW the completeness split, and this is why. Placed above it, it
            // refused partial fills too — which is not a floor but a stall: the fill was
            // discarded, so every later answer re-derived the same non-positive score and was
            // refused again, and the command could never be completed by any speech at all.
            // Here the exchange has to run to completion.
            ConfigurePartialFillDuplicateIntent();

            var pendingEvents = new List<VoxrCommand>();
            _recogniser.OnCommandPending += cmd => pendingEvents.Add(cmd);
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            // (1 + 1 + 1 - 1 - 1) / 5 = 0.20 against the short definition; the long one is
            // -3/9 and is inadmissible, so the short one is the only candidate.
            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand);
            Assert.AreEqual(1, pendingEvents.Count);

            // Fills {target}, stops at {fuse}. Re-scores -1/9 against the LONG definition while
            // the short one still calls it incomplete — so it must re-arm, not be refused.
            _recogniser.InjectText("hotel one");

            Assert.IsFalse(recognised.HasValue, "a partial fill still does not fire");
            Assert.IsTrue(_recogniser.HasPendingCommand, "and the pending is still live");
            Assert.AreEqual(2, pendingEvents.Count, "the re-arm reports progress (issue #77)");

            var pending = _recogniser.EditorPendingCommand;
            Assert.IsTrue(pending.HasValue);
            Assert.IsTrue(
                pending.Value.Command.HasSlot("target"), "the fill is KEPT, not discarded");
            Assert.AreEqual(
                new[] { "fuse" }, pending.Value.UnfilledSlots, "and only {fuse} is outstanding");

            // The stored score is the retained 0.20, never the -1/9 that was re-scored. This is
            // the half that moving the floor alone would have missed: a pending carries its
            // command into three fire paths that re-test nothing — Complete, the confirm-word
            // arm, and FireAsIs on timeout.
            Assert.Greater(
                pending.Value.Command.Score, 0f,
                "a pending must never come to hold a score its own fire paths would refuse"
            );

            // And the exchange completes, which it could not do at all before the fix.
            _recogniser.InjectText("impact");

            Assert.IsTrue(recognised.HasValue, "the last slot lands and the command fires");
            Assert.AreEqual("hotel one", recognised.Value.GetSlot("target"));
            Assert.AreEqual("impact", recognised.Value.GetSlot("fuse"));
            // (2 literals + 3 filled - 4 missed) / 9 = +1/9, back above the floor.
            Assert.AreEqual(1f / 9f, recognised.Value.Score, 0.001f);
        }

        [Test]
        public void FollowUpFill_ReScoreNonPositive_DoesNotReachAHandler()
        {
            // Issue #113. Both flush paths floor candidates at `Score <= 0` before anything
            // reaches a subscriber, and scoring.md §1 states the rule as absolute: a candidate
            // scoring zero or less is discarded and never competes. ScoreFollowUp had no such
            // floor, and this is the construction that walks a -1/9 command straight through
            // OnCommandConfirmed.
            ConfigureDuplicateIntent();

            VoxrCommand? confirmed = null;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandConfirmed += cmd => confirmed = cmd;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand, "{target} is unfilled, so it pends");

            _recogniser.InjectText("hotel one");

            Assert.IsFalse(
                confirmed.HasValue,
                confirmed.HasValue
                    ? $"fired with Score {confirmed.Value.Score:F4}"
                    : "OnCommandConfirmed must not receive a non-positive score"
            );
            Assert.IsFalse(recognised.HasValue, "and neither must OnCommandRecognised");
        }

        [Test]
        public void FollowUpFill_ReScoreNonPositive_LeavesThePendingAsItStands()
        {
            // The refusal is not a re-arm. The merged command is complete by slots, so
            // AdvanceSlotFill would install a pending with nothing left to fill — one
            // TryFollowUpSlotFill declines forever and FireAsIs would eventually fire carrying
            // this same score. Leaving the pending untouched keeps pendingTimeout the thing that
            // ends the exchange, and keeps the command it would fire the one that legitimately
            // scored 0.5 on the first utterance.
            ConfigureDuplicateIntent();

            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;

            _recogniser.InjectText("launch missiles target");
            _recogniser.InjectText("hotel one");

            Assert.IsTrue(_recogniser.HasPendingCommand, "the pending survives the refusal");
            Assert.IsFalse(cancelled.HasValue, "and is not cancelled by it");

            var pending = _recogniser.EditorPendingCommand;
            Assert.IsTrue(pending.HasValue);
            Assert.IsFalse(
                pending.Value.Command.HasSlot("target"),
                "the refused fill is not kept — the pending is exactly what it was"
            );
        }

        // ======== Issue #133: a pending opening is not "unrecognised" ========
        //
        // Two of the three branches that enter a pending and `continue` left
        // `anyThresholdFiltered` clear, so the utterance reached the tail of the result loop
        // with acceptedCount == 0 and was reported unrecognised in the same frame
        // OnCommandPending asked the integrator to prompt the speaker. The disambiguation
        // branch already set the flag and carried a comment naming that exact harm; these two
        // now match it.

        [Test]
        public void PartialMatchPending_DoesNotAlsoReportUnrecognised()
        {
            ConfigureSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            int unrecognisedCount = 0;
            _recogniser.OnUnrecognisedSpeech += _ => unrecognisedCount++;

            _recogniser.InjectText("launch missiles target");

            Assert.IsTrue(pending.HasValue, "the partial-match branch is the one under test");
            Assert.AreEqual(
                0,
                unrecognisedCount,
                "a prompt to fill a slot is not a report that the speech was not understood"
            );
        }

        [Test]
        public void ConfirmationPending_DoesNotAlsoReportUnrecognised()
        {
            ConfigureSync(requiresConfirm: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            int unrecognisedCount = 0;
            _recogniser.OnUnrecognisedSpeech += _ => unrecognisedCount++;

            _recogniser.InjectText("launch missiles target hotel one");

            Assert.IsTrue(pending.HasValue, "the confirmation branch is the one under test");
            Assert.AreEqual(
                0,
                unrecognisedCount,
                "a prompt to confirm is not a report that the speech was not understood"
            );
        }

        // ======== Slot resolver x pending machinery (issue #148, Phase 2) ========

        // The resolver rows need an incomplete candidate that CLEARS minScore, which this
        // fixture's own MakeCommands cannot give: a resolver is offered only a candidate that
        // already passed the score gate (F7, made explicit by D-5), and `launch {weapon} target
        // {target}` with its trailing slot stranded scores 0.50 against a 0.60 gate. A resolver
        // test built on that shape asserts the SCORE gate's silence and calls it the resolver's.
        //
        // Eight elements, seven matched and {track} stranded: (7 x 1 - 1) / 8 = 0.75 — the same
        // arithmetic PartialMatch_AboveGateButIncomplete_EntersPendingInsteadOfFiring derives,
        // and above the gate by a margin rather than on it.
        const string BareLaunch = "launch all missiles from tube three at";

        // The same grammar two slots short instead of one: {tube} and {track} both stranded, for
        // (6 x 1 - 2) / 8 = 0.50. BELOW the gate, so it is the ordinary allowPartialMatch pending
        // and no resolution is offered to it — which is what makes it a clean way to get a live
        // pending underneath a later utterance.
        const string PartialLaunch = "launch all missiles from tube at";

        void ConfigureResolvableSync(bool allowPartial = true, bool requiresConfirm = false)
        {
            _recogniser.Configure(
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
                        allowPartial,
                        requiresConfirm
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;
        }

        [Test]
        public void Resolver_ReturnsNone_StillEntersPendingWithTheSlotUnfilled()
        {
            // #148(b) and F13: not resolved means today's behaviour, unchanged. The resolver is
            // registered and IS consulted — it simply answers "I do not know" — so this is the
            // difference between a feature that declines and a feature that is absent.
            ConfigureResolvableSync(allowPartial: true);

            int calls = 0;
            _recogniser.RegisterSlotResolver(
                "track",
                _ =>
                {
                    calls++;
                    return VoxrSlotResolution.None;
                }
            );

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText(BareLaunch);

            Assert.AreEqual(1, calls, "the resolver was asked, and answered nothing");
            Assert.IsFalse(recognised.HasValue, "so nothing fires");
            Assert.IsTrue(pending.HasValue, "and the speaker is asked, exactly as before");
            Assert.IsFalse(pending.Value.HasSlot("track"));
            Assert.AreEqual(0, pending.Value.ResolvedSlots.Length);
            Assert.IsNull(
                unrecognised,
                "and the integrator is not told the speech was not understood in the same frame "
                    + "it was asked to prompt about it"
            );

            var stored = _recogniser.EditorPendingCommand;
            Assert.IsTrue(stored.HasValue);
            Assert.AreEqual(
                new[] { "track" },
                stored.Value.UnfilledSlots,
                "and the pending's own unfilled list is untouched by the refusal"
            );
        }

        [Test]
        public void Resolver_ReturnsNone_WithoutAllowPartialMatch_IsStillRejected()
        {
            // F13's other arm. The resolver here returns `default` rather than the named None, to
            // pin F4's claim that a resolver which returns nothing needs no ceremony.
            ConfigureResolvableSync(allowPartial: false);

            _recogniser.RegisterSlotResolver("track", _ => default(VoxrSlotResolution));

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText(BareLaunch);

            Assert.IsFalse(recognised.HasValue, "nothing fires");
            Assert.IsFalse(pending.HasValue, "and without the flag nothing is held open either");
            Assert.AreEqual(BareLaunch, unrecognised, "the utterance is reported unrecognised");
        }

        [Test]
        public void Resolver_ForOnlyOneOfTwoMissingSlots_FillsNeitherAndPendsWithBothUnfilled()
        {
            // F8, all-or-nothing. A partial application would fire nothing either — the command
            // is still incomplete — so the observable difference is in the PENDING: a half-filled
            // pending asks the speaker one fewer question than it should, and the slot the game
            // silently filled is never spoken about again.
            //
            // Twelve elements, ten matched and {tube} and {track} both stranded:
            // (10 x 1 - 2) / 12 = 0.667, clear of the 0.60 gate, so D-5 does not decline this
            // candidate before all-or-nothing gets to. The eight-element pattern above cannot be
            // used: two slots short it scores 0.50 and the score gate would answer for the rule
            // under test. No token of the utterance is a {tube} or {quantity} value except the
            // one meant for it, so there is nothing later in the sentence for a stranded slot to
            // reach forward and claim.
            _recogniser.Configure(
                new[]
                {
                    new VoxrSlotDefinition("track", new[] { "alpha", "bravo" }),
                    new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                    new VoxrSlotDefinition("quantity", new[] { "all" }),
                    new VoxrSlotDefinition("tube", new[] { "three", "four" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[]
                        {
                            new[]
                            {
                                "helm",
                                "launch",
                                "{quantity}",
                                "{weapon}",
                                "from",
                                "tube",
                                "{tube}",
                                "at",
                                "bearing",
                                "north",
                                "east",
                                "{track}",
                            },
                        },
                        allowPartialMatch: true
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;

            // Only {track} has a resolver. {tube} has none, so nothing may be applied.
            _recogniser.RegisterSlotResolver(
                "track",
                _ => new VoxrSlotResolution("alpha", "main target")
            );

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText("helm launch all missiles from tube at bearing north east");

            Assert.IsFalse(recognised.HasValue, "a command two slots short still does not fire");
            Assert.IsTrue(pending.HasValue, "it goes to slot-fill, as it did before the feature");
            Assert.GreaterOrEqual(
                pending.Value.Score,
                _recogniser.MinScore,
                "and it must CLEAR the score gate, or D-5 declined it and all-or-nothing was "
                    + "never the rule that answered"
            );
            Assert.IsFalse(
                pending.Value.HasSlot("track"),
                "the one slot that COULD resolve is left unfilled, because the other could not"
            );
            Assert.IsFalse(pending.Value.HasSlot("tube"));
            Assert.AreEqual(
                0,
                pending.Value.ResolvedSlots.Length,
                "and the command reports nothing as resolver-filled"
            );

            var stored = _recogniser.EditorPendingCommand;
            Assert.IsTrue(stored.HasValue);
            Assert.AreEqual(
                new[] { "tube", "track" },
                stored.Value.UnfilledSlots,
                "both slots are still outstanding — the speaker is asked about everything they "
                    + "were asked about before the feature existed"
            );
        }

        [Test]
        public void Resolver_OnACommandRequiringConfirmation_StillAsksBeforeFiring()
        {
            // F12. Resolution changes exactly one thing, the completeness answer; it may not move
            // a command across any other gate. The confirmation gate is the one a speaker would
            // notice being skipped, because the command it skips is a command they never
            // completed out loud.
            ConfigureResolvableSync(allowPartial: true, requiresConfirm: true);

            _recogniser.RegisterSlotResolver(
                "track",
                _ => new VoxrSlotResolution("alpha", "main target")
            );

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText(BareLaunch);

            Assert.IsFalse(
                recognised.HasValue,
                "a resolved command is still only a command — it asks before it fires"
            );
            Assert.IsTrue(pending.HasValue, "the confirmation is requested");
            Assert.IsTrue(_recogniser.HasPendingCommand);
            Assert.AreEqual(
                "alpha",
                pending.Value.GetSlot("track"),
                "and the command it is asking about already carries the resolved argument"
            );

            _recogniser.InjectText("confirm");

            Assert.IsTrue(recognised.HasValue, "and confirming fires it");
            Assert.AreEqual(
                "alpha",
                recognised.Value.GetSlot("track"),
                "still carrying the resolution across the gate"
            );
            Assert.AreEqual("main target", recognised.Value.GetSlotResolutionReason("track"));
        }

        [Test]
        public void Resolver_BelowMinScoreWithAllowPartialMatch_StillEntersPendingUnfilled()
        {
            // D-5's regression guard, and it is a guard on EXISTING behaviour rather than a test
            // of the resolver — which is why every assertion below is about the pending.
            //
            // Without the score gate on the resolution pass, this candidate resolves, its
            // unfilled-slot list comes back empty, the allowPartialMatch branch is skipped
            // because there is nothing to prompt for, and the command falls through to the
            // REJECT — turning a pending into an unrecognised utterance. That is resolution
            // moving a command across a gate other than completeness, which F13 forbids and the
            // one rule of architecture section 2 forbids in general.
            //
            // Reuses the fixture's own four-element grammar precisely because it scores 0.50:
            // "launch missiles target" is below the 0.60 gate with {target} stranded, which is
            // the shape the guard is about.
            ConfigureSync(allowPartial: true);

            _recogniser.RegisterSlotResolver(
                "target",
                _ => new VoxrSlotResolution("hotel one", "main target")
            );

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("launch missiles target");

            Assert.IsTrue(pending.HasValue, "the below-gate incomplete command still pends");
            Assert.Less(
                pending.Value.Score,
                _recogniser.MinScore,
                "and it is genuinely below the gate, or this guards nothing"
            );
            Assert.IsFalse(
                pending.Value.HasSlot("target"),
                "with its slot unfilled: a resolver may not reach a candidate the gate refused"
            );
            Assert.IsFalse(recognised.HasValue);
            Assert.IsNull(unrecognised, "and it is not rejected");
            Assert.IsTrue(_recogniser.HasPendingCommand);
        }

        [Test]
        public void Resolver_CompletesAFollowUpFill_FiresInsteadOfReArmingThePending()
        {
            // F11's second half, and D-3. The speaker fills one of the two outstanding slots by
            // voice; the game knows the other. Without resolution on this site the pending
            // re-arms and asks again for something the game could have answered — and the SAME
            // utterance would have completed had no pending been live, which is the invisible
            // split section 6 argues against.
            ConfigureResolvableSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;

            _recogniser.InjectText(PartialLaunch);
            Assert.IsTrue(pending.HasValue, "two slots short, so a pending opens");
            var stored = _recogniser.EditorPendingCommand;
            Assert.IsTrue(stored.HasValue);
            Assert.AreEqual(new[] { "tube", "track" }, stored.Value.UnfilledSlots);

            // Registered only now, so the pending above is the pre-feature one in every respect.
            _recogniser.RegisterSlotResolver(
                "track",
                _ => new VoxrSlotResolution("alpha", "main target")
            );

            // Fills {tube} by voice and stops: {track} is not in this utterance. Before the
            // feature that is a re-arm (issue #77), because the fill is progress and not the
            // command.
            _recogniser.InjectText("three");

            Assert.IsTrue(
                recognised.HasValue,
                "the follow-up completes the command instead of re-arming the pending"
            );
            Assert.AreEqual("three", recognised.Value.GetSlot("tube"), "the spoken fill");
            Assert.AreEqual("alpha", recognised.Value.GetSlot("track"), "and the resolved one");
            Assert.AreEqual("main target", recognised.Value.GetSlotResolutionReason("track"));
            Assert.IsNull(
                recognised.Value.GetSlotResolutionReason("tube"),
                "the slot the speaker filled is not reported as resolver-filled"
            );
            Assert.IsFalse(_recogniser.HasPendingCommand, "and the question is closed");
        }

        [Test]
        public void Resolver_WithAPendingLive_ProducesTheSameOutcomeAsWithNone()
        {
            // F11's first half. The failure it guards is not a crash: it is the same utterance,
            // with the same resolvers and the same grammar, producing a different outcome
            // depending only on whether a pending happened to be live — and nothing in the
            // suite, the session log or the debug window would show it.
            ConfigureResolvableSync(allowPartial: true);

            _recogniser.RegisterSlotResolver(
                "track",
                _ => new VoxrSlotResolution("alpha", "main target")
            );

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            // With no pending live.
            _recogniser.InjectText(BareLaunch);
            Assert.IsTrue(recognised.HasValue, "the resolved command fires");
            var withoutPending = recognised.Value;
            Assert.IsFalse(_recogniser.HasPendingCommand);

            // Now put a pending underneath it. This one is below the gate and two slots short, so
            // no resolution reaches it and it is the ordinary allowPartialMatch pending.
            recognised = null;
            pending = null;
            _recogniser.InjectText(PartialLaunch);
            Assert.IsTrue(pending.HasValue, "a pending is live");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            // The identical utterance, spoken over it.
            _recogniser.InjectText(BareLaunch);

            Assert.IsTrue(recognised.HasValue, "and it still fires");
            var withPending = recognised.Value;

            Assert.AreEqual(withoutPending.Intent, withPending.Intent);
            Assert.AreEqual(withoutPending.GetSlot("track"), withPending.GetSlot("track"));
            Assert.AreEqual(
                withoutPending.GetSlotResolutionReason("track"),
                withPending.GetSlotResolutionReason("track")
            );
            Assert.AreEqual(withoutPending.Score, withPending.Score, 1e-5f);
            // The sharpest of the four. A command that came through the follow-up path instead
            // carries the pending's transcript glued to this one's, so this is what says the same
            // utterance was read the same way rather than merely landing on the same intent.
            Assert.AreEqual(
                withoutPending.RawText,
                withPending.RawText,
                "the same utterance, read as the same utterance — not merged into the pending's"
            );
            Assert.IsFalse(
                _recogniser.HasPendingCommand,
                "the pending's own cancellation is the only difference F11 allows"
            );
        }

        [Test]
        public void Resolver_ThatCancelsThePending_DoesNotThrow()
        {
            // D-15. Step 3b is the first game code ever to run between the follow-up fill and its
            // use, and "cancel the pending and ask the crew myself" is a plausible thing for a
            // game's resolver to do. The fill was merged INTO that pending, so every read in the
            // follow-up branch assumes it is still there — including the shipped Complete(...)
            // one, which is why guarding the new read alone would leave a safe new path beside a
            // crashing old one.
            //
            // The resolver answers None deliberately: a resolution would make the fresh candidate
            // complete, Step 4 would preempt, and the follow-up branch — the one that dereferences
            // the pending — would never be entered at all.
            ConfigureResolvableSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            _recogniser.InjectText(PartialLaunch);
            Assert.IsTrue(pending.HasValue, "a pending is live and {tube} is fillable from below");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            int calls = 0;
            _recogniser.RegisterSlotResolver(
                "track",
                _ =>
                {
                    calls++;
                    _recogniser.CancelPendingCommand();
                    return VoxrSlotResolution.None;
                }
            );

            // The utterance carries a {tube} value, so the follow-up slot-fill produces a result
            // before the resolver is ever called — which is the precondition of the hazard.
            Assert.DoesNotThrow(() => _recogniser.InjectText(BareLaunch));

            Assert.AreEqual(1, calls, "the resolver ran, so the pending really was cancelled");
            Assert.IsTrue(
                _recogniser.HasPendingCommand,
                "and the utterance still ran to its own end: the fresh candidate is incomplete "
                    + "and opens a pending of its own"
            );
            var stored = _recogniser.EditorPendingCommand;
            Assert.IsTrue(stored.HasValue);
            Assert.AreEqual(
                new[] { "track" },
                stored.Value.UnfilledSlots,
                "the NEW candidate's pending, not the cancelled one resurrected"
            );
        }

        [Test]
        public void Resolver_ThatCancelsThePendingFromTheFollowUpSite_DoesNotThrow()
        {
            // The blocker (F-1), and the site Resolver_ThatCancelsThePending_DoesNotThrow above
            // cannot reach. That one covers Step 3b's discard; this covers Step 5's own
            // resolution attempt, which is routinely the utterance's FIRST resolver call — Step
            // 3b resolves nothing at all when the parse produced no results, so the discard at
            // the top of the method cannot have covered this.
            //
            // The follow-up utterance parses to ZERO results deliberately: "three" is a {tube}
            // value and nothing else, so no round scores above zero and Step 3b is skipped
            // entirely. What reaches the resolver is the Step 5 attempt, and by returning a
            // VALUE it takes the arm that ends in _pending.Complete(...) — which dereferences a
            // pending its own resolver has just cleared.
            ConfigureResolvableSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            _recogniser.InjectText(PartialLaunch);
            Assert.IsTrue(pending.HasValue, "precondition: a pending two slots short");
            Assert.IsTrue(_recogniser.HasPendingCommand);

            int calls = 0;
            VoxrCommand? cancelled = null;
            _recogniser.OnCommandCancelled += cmd => cancelled = cmd;
            _recogniser.RegisterSlotResolver(
                "track",
                _ =>
                {
                    calls++;
                    _recogniser.CancelPendingCommand();
                    return new VoxrSlotResolution("alpha", "asking the crew myself");
                }
            );

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            Assert.DoesNotThrow(() => _recogniser.InjectText("three"));

            Assert.AreEqual(1, calls, "the resolver ran, so the pending really was cancelled");
            Assert.IsTrue(cancelled.HasValue, "and the cancellation reached the integrator");
            Assert.IsFalse(_recogniser.HasPendingCommand, "the pending is gone, not resurrected");
            Assert.IsFalse(
                recognised.HasValue,
                "the fill was merged INTO the pending the resolver cancelled, so there is nothing "
                    + "left to complete"
            );
            Assert.AreEqual(
                "three",
                unrecognised,
                "and the utterance falls out of the follow-up branch and runs to its own end — "
                    + "returning here would hand the speaker silence after their own game "
                    + "cancelled the exchange"
            );
        }

        [Test]
        public void Resolver_ThatCancelsThePendingFromTheFollowUpSite_DoesNotThrowWhenItRefuses()
        {
            // F-1's other arm. A resolver that answers None short-circuits TryResolveMissingSlots
            // before the completeness re-test, so the branch below takes AdvanceSlotFill rather
            // than Complete — a different dereference of the same cleared pending, and it threw
            // too. Guarding one read would have left the other.
            ConfigureResolvableSync(allowPartial: true);

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            _recogniser.InjectText(PartialLaunch);
            Assert.IsTrue(pending.HasValue, "precondition: a pending two slots short");

            int calls = 0;
            _recogniser.RegisterSlotResolver(
                "track",
                _ =>
                {
                    calls++;
                    _recogniser.CancelPendingCommand();
                    return VoxrSlotResolution.None;
                }
            );

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;
            int pendingCount = 0;
            _recogniser.OnCommandPending += _ => pendingCount++;

            Assert.DoesNotThrow(() => _recogniser.InjectText("three"));

            Assert.AreEqual(1, calls, "the resolver ran, so the pending really was cancelled");
            Assert.IsFalse(_recogniser.HasPendingCommand, "and nothing re-armed it");
            Assert.AreEqual(0, pendingCount, "the partial fill has no pending left to re-arm");
            Assert.IsFalse(recognised.HasValue);
            Assert.AreEqual("three", unrecognised, "the utterance is reported, not swallowed");
        }

        [Test]
        public void Resolver_BelowMinConfidenceWithAllowPartialMatch_StillEntersPendingUnfilled()
        {
            // F-3, and the sibling of
            // Resolver_BelowMinScoreWithAllowPartialMatch_StillEntersPendingUnfilled above: the
            // second of Step 3b's three floors, guarding EXISTING behaviour rather than the
            // resolver.
            //
            // An above-minScore incomplete command on an allowPartialMatch definition enters
            // pending today and the speaker is asked for the missing slot; the confidence gate
            // sits BELOW that branch in Step 7 and is never reached. Resolve it and the branch is
            // skipped, the candidate meets a gate it was never measured against, and it is
            // dropped with anyThresholdFiltered set — which suppresses OnUnrecognisedSpeech too.
            // No command, no pending, no prompt, no report: the utterance disappears.
            //
            // Real word confidences are essential. Injected text with no word data yields
            // Confidence == -1, which DISABLES the gate rather than failing it, so a test built
            // on bare InjectText could never reach the floor under test.
            ConfigureResolvableSync(allowPartial: true);
            SetMinConfidence(_recogniser, 0.9f);

            int calls = 0;
            _recogniser.RegisterSlotResolver(
                "track",
                _ =>
                {
                    calls++;
                    return new VoxrSlotResolution("alpha", "main target");
                }
            );

            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            var words = VoXR.VoxrSpeechRecogniser.CreateSimulatedWords(BareLaunch, 0.5f);
            _recogniser.InjectText(BareLaunch, words);

            Assert.AreEqual(
                0,
                calls,
                "a resolver may not reach a candidate the confidence gate is about to refuse"
            );
            Assert.IsTrue(pending.HasValue, "the incomplete command still pends, exactly as before");
            Assert.AreEqual(
                0.5f,
                pending.Value.Confidence,
                1e-5f,
                "real word data reached it — at -1 the gate is disabled and this guards nothing"
            );
            Assert.GreaterOrEqual(
                pending.Value.Score,
                _recogniser.MinScore,
                "and it is above the SCORE floor, or the wrong floor answered"
            );
            Assert.IsFalse(pending.Value.HasSlot("track"), "with its slot unfilled");
            Assert.AreEqual(0, pending.Value.ResolvedSlots.Length);
            Assert.IsFalse(recognised.HasValue);
            Assert.IsNull(unrecognised, "and the utterance is not reported unrecognised");
            Assert.IsTrue(_recogniser.HasPendingCommand);
        }

        [Test]
        public void Resolver_OnCooldownWithAllowPartialMatch_StillEntersPendingUnfilled()
        {
            // F-3's third floor, and the one that costs most. Step 4 tests completeness and
            // confidence but NOT the cooldown, so a resolved candidate on cooldown sets
            // hasCompleteNewCommand — which cancels a live pending outright — and then Step 7
            // debounces it. The exchange is destroyed by a command that never fired. Even with
            // no pending live, the observable loss is the same as the confidence arm's: the
            // prompt the speaker would have got disappears.
            ConfigureResolvableSync(allowPartial: true);
            _recogniser.CommandCooldown = 10f;

            int calls = 0;
            _recogniser.RegisterSlotResolver(
                "track",
                _ =>
                {
                    calls++;
                    return new VoxrSlotResolution("alpha", "main target");
                }
            );

            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            // Spoken in full, so it fires on its own merits and puts launch_weapon on cooldown.
            // A complete candidate is offered to TryResolveMissingSlots and asks nothing, which
            // is why the count below starts at zero.
            _recogniser.InjectText("launch all missiles from tube three at alpha");
            Assert.IsTrue(recognised.HasValue, "precondition: the intent has just fired");
            Assert.AreEqual(0, calls, "precondition: a complete command asks no question");

            recognised = null;
            _recogniser.InjectText(BareLaunch);

            Assert.AreEqual(
                0,
                calls,
                "a resolver may not reach a candidate whose intent is on cooldown"
            );
            Assert.IsFalse(recognised.HasValue, "nothing fires while the cooldown stands");
            Assert.IsTrue(
                pending.HasValue,
                "and the speaker is still asked for the missing slot, exactly as before"
            );
            Assert.IsFalse(pending.Value.HasSlot("track"));

            var stored = _recogniser.EditorPendingCommand;
            Assert.IsTrue(stored.HasValue);
            Assert.AreEqual(new[] { "track" }, stored.Value.UnfilledSlots);
        }

        [Test]
        public void Resolver_OnAFollowUpFillThatReScoresNonPositive_IsNotOfferedAndThePendingReArms()
        {
            // F-12. D-6's `Score > 0f` on Step 5's resolution gate had no test at all: the three
            // #113 tests above register no resolver, so they pass with the floor deleted.
            //
            // This path has no minScore gate by design (#77, #113); its fire-floor is the
            // `Score <= 0` refusal, which sits BELOW the completeness split precisely so a
            // partial fill re-arms instead of stalling. Resolving a non-positive fill moves it
            // across that split and turns today's "keep the progress and ask again" into "refuse
            // and report unrecognised" — the exact stall the placement exists to prevent, and
            // one the speaker cannot talk their way out of, since the same fill re-scores
            // non-positive every time.
            //
            // Reuses the #113 duplicate-intent divergence, which is what puts a non-positive
            // re-score in hand at all: ScoreFollowUp takes the FIRST definition (nine elements)
            // while IsIncomplete reads the LAST (five), so the fill re-scores -1/9 while still
            // being called incomplete.
            ConfigurePartialFillDuplicateIntent();

            int calls = 0;
            _recogniser.RegisterSlotResolver(
                "fuse",
                _ =>
                {
                    calls++;
                    return new VoxrSlotResolution("impact", "standing weapons order");
                }
            );

            var pendingEvents = new List<VoxrCommand>();
            _recogniser.OnCommandPending += cmd => pendingEvents.Add(cmd);
            VoxrCommand? recognised = null;
            _recogniser.OnCommandRecognised += cmd => recognised = cmd;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            // 0.20 against the short definition — below minScore, so Step 3b's own floor keeps
            // the resolver away from it and this pending is the pre-feature one.
            _recogniser.InjectText("launch missiles target");
            Assert.IsTrue(_recogniser.HasPendingCommand, "precondition: a pending two slots short");
            Assert.AreEqual(1, pendingEvents.Count);
            Assert.AreEqual(0, calls, "precondition: nothing was resolved on the way in");

            // Fills {target}, stops at {fuse}, and re-scores -1/9.
            _recogniser.InjectText("hotel one");

            Assert.AreEqual(
                0,
                calls,
                "a fill the fire-floor would refuse is not offered resolution — completing it "
                    + "here would carry it across the completeness split and into that refusal"
            );
            Assert.IsFalse(recognised.HasValue, "a partial fill still does not fire");
            Assert.IsNull(unrecognised, "and it is not reported unrecognised either");
            Assert.IsTrue(_recogniser.HasPendingCommand, "the pending is still live");
            Assert.AreEqual(2, pendingEvents.Count, "and it RE-ARMS, reporting progress (#77)");

            var stored = _recogniser.EditorPendingCommand;
            Assert.IsTrue(stored.HasValue);
            Assert.IsTrue(
                stored.Value.Command.HasSlot("target"),
                "the spoken fill is kept, not discarded by a refusal"
            );
            Assert.AreEqual(
                new[] { "fuse" },
                stored.Value.UnfilledSlots,
                "and only {fuse} is outstanding, so the exchange can still be finished by voice"
            );
        }

        // -------- Helpers --------

        // minConfidence has no test setter, so it is reached through the serialized field the
        // Inspector writes — the same reflection idiom VoxrCommandRecogniserInjectionTests uses
        // for this field and VoxrCommandParserTests uses for minScore.
        static void SetMinConfidence(VoxrCommandRecogniser recogniser, float value)
        {
            var field = typeof(VoxrCommandRecogniser).GetField(
                "minConfidence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            Assert.IsNotNull(field, "VoxrCommandRecogniser.minConfidence");
            field.SetValue(recogniser, value);
        }

        void ForceTimeoutNow()
        {
            _recogniser.TestForceTimeoutNow();
        }
    }
}
