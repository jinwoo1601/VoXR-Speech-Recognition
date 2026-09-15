using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VoXR.Commands;

namespace VoXR.Tests.Runtime
{
    public class VoxrDynamicSlotTests
    {
        GameObject _go;
        VoxrCommandRecogniser _recogniser;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestDynamicSlots");
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
                        { "a one", "alpha one" },
                    }),
                new VoxrSlotDefinition("weapon",
                    new[] { "missiles", "torpedoes" }),
            };
        }

        static VoxrCommandDefinition[] MakeCommands()
        {
            return new[]
            {
                new VoxrCommandDefinition("launch_weapon", new[]
                {
                    new[] { "launch", "{weapon}", "target", "{target}" },
                }),
                new VoxrCommandDefinition("cease_fire", new[]
                {
                    new[] { "cease", "fire" },
                }),
            };
        }

        void ConfigureSync()
        {
            _recogniser.Configure(MakeSlots(), MakeCommands());
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
        }

        // -------- Registration API --------

        [Test]
        public void RegisterSlotValueProvider_NullSlotName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _recogniser.RegisterSlotValueProvider(null, () => new[] { "a" }));
        }

        [Test]
        public void RegisterSlotValueProvider_NullProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _recogniser.RegisterSlotValueProvider("target", null));
        }

        [Test]
        public void UnregisterSlotValueProvider_NullSlotName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _recogniser.UnregisterSlotValueProvider(null));
        }

        [Test]
        public void UnregisterSlotValueProvider_NotRegistered_ReturnsFalse()
        {
            Assert.IsFalse(_recogniser.UnregisterSlotValueProvider("target"));
        }

        [Test]
        public void RegisterSlotValueProvider_Overwrite_AcceptsNewProvider()
        {
            ConfigureSync();

            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });
            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel two" });
            _recogniser.NotifySlotChanged();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            _recogniser.InjectText("launch missiles target hotel two");
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual("hotel two", received.Value.GetSlot("target"));

            received = null;
            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsFalse(received.HasValue, "Overwritten provider should exclude hotel one");
        }

        // -------- Resolver registration API --------

        [Test]
        public void RegisterSlotResolver_NullSlotName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _recogniser.RegisterSlotResolver(null, () => VoxrSlotResolution.None));
        }

        [Test]
        public void RegisterSlotResolver_NullResolver_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _recogniser.RegisterSlotResolver("target", null));
        }

        [Test]
        public void UnregisterSlotResolver_NullSlotName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _recogniser.UnregisterSlotResolver(null));
        }

        [Test]
        public void UnregisterSlotResolver_NotRegistered_ReturnsFalse()
        {
            Assert.IsFalse(_recogniser.UnregisterSlotResolver("target"));
        }

        [Test]
        public void UnregisterSlotResolver_Registered_ReturnsTrue()
        {
            _recogniser.RegisterSlotResolver("target", () => VoxrSlotResolution.None);

            Assert.IsTrue(_recogniser.UnregisterSlotResolver("target"));
        }

        [Test]
        public void RegisterSlotResolver_UnknownSlotName_ThrowsNothing()
        {
            ConfigureSync();

            // "bearing" is in no slot definition and no pattern
            Assert.DoesNotThrow(() =>
                _recogniser.RegisterSlotResolver("bearing",
                    () => new VoxrSlotResolution("090", "unknown slot")));

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            _recogniser.InjectText("launch missiles target hotel two");

            Assert.IsTrue(received.HasValue,
                "A resolver on a slot name absent from the grammar must change nothing");
            Assert.AreEqual("hotel two", received.Value.GetSlot("target"));
        }

        // -------- Resolution surface --------

        [Test]
        public void SlotResolution_DefaultAndEmptyValue_AreNotResolutions()
        {
            Assert.IsFalse(default(VoxrSlotResolution).HasValue);
            Assert.IsFalse(VoxrSlotResolution.None.HasValue);
            Assert.IsFalse(new VoxrSlotResolution("", "main target").HasValue);

            VoxrSlotResolution resolution = new VoxrSlotResolution("1721", "main target");
            Assert.IsTrue(resolution.HasValue);
            Assert.AreEqual("1721", resolution.Value);
            Assert.AreEqual("main target", resolution.Reason);
        }

        [Test]
        public void Command_WithNoResolvedSlots_ReportsNone()
        {
            ConfigureSync();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            _recogniser.InjectText("launch missiles target hotel two");

            Assert.IsTrue(received.HasValue);
            Assert.IsNotNull(received.Value.ResolvedSlots);
            Assert.AreEqual(0, received.Value.ResolvedSlots.Length);
            Assert.IsNull(received.Value.GetSlotResolutionReason("target"),
                "A spoken slot has no resolution reason");
            Assert.IsNull(received.Value.GetSlotResolutionReason("bearing"),
                "An unheard-of slot name has no resolution reason");
        }

        // -------- Parser narrowing --------

        [Test]
        public void Provider_ReturnsSubset_ExcludedValuesStopMatching()
        {
            ConfigureSync();

            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });
            _recogniser.NotifySlotChanged();

            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("launch missiles target hotel two");

            Assert.IsNotNull(unrecognised,
                "Excluded value 'hotel two' should not match after provider narrowing");
        }

        [Test]
        public void Provider_ReturnsSubset_ActiveValuesStillMatch()
        {
            ConfigureSync();

            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });
            _recogniser.NotifySlotChanged();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            _recogniser.InjectText("launch missiles target hotel one");

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual("hotel one", received.Value.GetSlot("target"));
        }

        [Test]
        public void Provider_ReturnsNull_UsesStaticValues()
        {
            ConfigureSync();

            _recogniser.RegisterSlotValueProvider("target", () => null);
            _recogniser.NotifySlotChanged();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            // All original values should still work
            _recogniser.InjectText("launch missiles target hotel two");

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual("hotel two", received.Value.GetSlot("target"));
        }

        [Test]
        public void Provider_ReturnsEmpty_NothingMatches()
        {
            ConfigureSync();

            _recogniser.RegisterSlotValueProvider("target", () => Array.Empty<string>());
            _recogniser.NotifySlotChanged();

            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("launch missiles target hotel one");

            Assert.IsNotNull(unrecognised,
                "Empty provider should cause all target values to fail matching");
        }

        [Test]
        public void Provider_FiltersAliases_OnlyActiveCanonicalTargets()
        {
            ConfigureSync();

            // Only allow "hotel one" — aliases pointing to "hotel two" or "alpha one" should be pruned
            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });
            _recogniser.NotifySlotChanged();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            // Alias "h one" -> "hotel one" should still work
            _recogniser.InjectText("launch missiles target h one");
            Assert.IsTrue(received.HasValue, "Alias 'h one' -> 'hotel one' should still match");
            Assert.AreEqual("hotel one", received.Value.GetSlot("target"));

            // Alias "h two" -> "hotel two" should be pruned
            received = null;
            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            _recogniser.InjectText("launch missiles target h two");
            Assert.IsFalse(received.HasValue,
                "Alias 'h two' -> excluded 'hotel two' should not match");
        }

        // -------- Buffer preservation --------

        [Test]
        public void NotifySlotChanged_DoesNotClearBuffer()
        {
            _recogniser.Configure(MakeSlots(), MakeCommands());
            _recogniser.BufferWindow = 5f;
            _recogniser.CommandCooldown = 0f;

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            _recogniser.InjectText("cease fire");
            Assert.IsFalse(received.HasValue, "Buffer should hold the command");

            // Narrowing should not flush or discard the buffer
            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });
            _recogniser.NotifySlotChanged();

            _recogniser.FlushPendingBuffer();
            Assert.IsTrue(received.HasValue,
                "NotifySlotChanged must not clear the utterance buffer");
            Assert.AreEqual("cease_fire", received.Value.Intent);
        }

        // -------- Grammar independence --------

        [Test]
        public void RebuildParser_DoesNotChangeGrammar()
        {
            ConfigureSync();

            // Capture grammar before
            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });

            string grammarBefore = _recogniser.TestGrammarJson;

            _recogniser.NotifySlotChanged();

            string grammarAfter = _recogniser.TestGrammarJson;
            Assert.AreEqual(grammarBefore, grammarAfter,
                "RebuildParser (via NotifySlotChanged) must not change grammar");
        }

        // -------- Integration --------

        [Test]
        public void Provider_UpdatedBetweenInjects_ReflectsLatest()
        {
            ConfigureSync();

            string[] activeTargets = { "hotel one" };
            _recogniser.RegisterSlotValueProvider("target", () => activeTargets);
            _recogniser.NotifySlotChanged();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            // "hotel one" should match
            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsTrue(received.HasValue);

            // Switch provider to "hotel two"
            received = null;
            activeTargets = new[] { "hotel two" };
            _recogniser.NotifySlotChanged();

            string unrecognised = null;
            _recogniser.OnUnrecognisedSpeech += text => unrecognised = text;

            // "hotel one" should no longer match
            _recogniser.InjectText("launch missiles target hotel one");
            Assert.IsFalse(received.HasValue, "hotel one should be excluded after provider update");

            // "hotel two" should now match
            unrecognised = null;
            _recogniser.InjectText("launch missiles target hotel two");
            Assert.IsTrue(received.HasValue, "hotel two should match after provider update");
            Assert.AreEqual("hotel two", received.Value.GetSlot("target"));
        }

        [Test]
        public void Register_WithoutNotify_DoesNotAffectParser()
        {
            ConfigureSync();

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            // Register provider that excludes "hotel two", but don't call NotifySlotChanged
            _recogniser.RegisterSlotValueProvider("target", () => new[] { "hotel one" });

            // "hotel two" should still match because parser hasn't been rebuilt
            _recogniser.InjectText("launch missiles target hotel two");
            Assert.IsTrue(received.HasValue,
                "Without NotifySlotChanged, excluded value should still match old parser");
            Assert.AreEqual("hotel two", received.Value.GetSlot("target"));
        }

        // -------- Error paths --------

        [Test]
        public void RebuildParser_BeforeConfigure_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _recogniser.RebuildParser());
        }

        [Test]
        public void RebuildGrammar_BeforeConfigure_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _recogniser.RebuildGrammar());
        }

        [Test]
        public void NotifySlotChanged_BeforeConfigure_NoOp()
        {
            Assert.DoesNotThrow(() => _recogniser.NotifySlotChanged());
        }

        // ======== Resolver behaviour (issue #148, Phase 2) ========

        // These need something this fixture's own MakeCommands cannot give them: an incomplete
        // candidate that CLEARS minScore. A resolver is offered only a candidate that already
        // passed the score gate (F7, made explicit by D-5), and `launch {weapon} target {target}`
        // with its trailing slot stranded scores 0.50 against a 0.60 gate — a resolver test built
        // on that shape would be asserting the SCORE gate's silence and calling it the resolver's.
        //
        // Eight elements, seven matched and {track} stranded: (7 x 1 - 1) / 8 = 0.75. That is the
        // same arithmetic VoxrPendingCommandTests.PartialMatch_AboveGateButIncomplete_Enters-
        // PendingInsteadOfFiring derives for its own above-gate candidate, and it is above the
        // gate by a margin rather than on it, so a scoring change cannot quietly turn these into
        // score-gate tests.
        const string BareLaunch = "launch all missiles from tube three at";

        void ConfigureResolvableSync(bool allowPartial = true)
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
                        allowPartialMatch: allowPartial
                    ),
                }
            );
            _recogniser.BufferWindow = 0f;
            _recogniser.CommandCooldown = 0f;
            _recogniser.PendingTimeout = 30f;
        }

        [Test]
        public void RegisterSlotResolver_Overwrite_TheSecondResolverFillsTheSlot()
        {
            // F1's second sentence, and the half Phase 1 could not write: "a second registration
            // replaces the first silently" is only observable once something reads the registry.
            // The sibling of RegisterSlotValueProvider_Overwrite_AcceptsNewProvider above.
            ConfigureResolvableSync();

            _recogniser.RegisterSlotResolver(
                "track",
                () => new VoxrSlotResolution("alpha", "first resolver")
            );
            _recogniser.RegisterSlotResolver(
                "track",
                () => new VoxrSlotResolution("bravo", "second resolver")
            );

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;

            _recogniser.InjectText(BareLaunch);

            Assert.IsTrue(received.HasValue, "the resolved command fires");
            Assert.AreEqual(
                "bravo",
                received.Value.GetSlot("track"),
                "the second registration wins — a replaced resolver must not still be consulted"
            );
            Assert.AreEqual(
                "second resolver",
                received.Value.GetSlotResolutionReason("track"),
                "and its reason travels with it, not the replaced one's"
            );
        }

        [Test]
        public void Resolver_RegisteredMidSession_TakesEffectWithoutNotifySlotChanged()
        {
            // F5. The inverse of Register_WithoutNotify_DoesNotAffectParser above, and the
            // contrast is the point: a value PROVIDER is inert until the parser is rebuilt,
            // because it narrows what the parser will match; a RESOLVER fills what the parser did
            // not match, so it changes no grammar and needs no rebuild at all.
            ConfigureResolvableSync(allowPartial: true);

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            // No resolver yet: today's behaviour, and the baseline the registration has to change.
            _recogniser.InjectText(BareLaunch);
            Assert.IsFalse(received.HasValue, "nothing fires with no resolver registered");
            Assert.IsTrue(pending.HasValue, "the speaker is asked for the missing argument");
            _recogniser.CancelPendingCommand();

            string grammarBefore = _recogniser.TestGrammarJson;
            _recogniser.RegisterSlotResolver(
                "track",
                () => new VoxrSlotResolution("alpha", "main target")
            );
            Assert.AreEqual(
                grammarBefore,
                _recogniser.TestGrammarJson,
                "registering a resolver must not change the decoder grammar"
            );

            // Deliberately NO NotifySlotChanged() and no RebuildParser() between here and the
            // next utterance. If either were needed this assert is what says so.
            _recogniser.InjectText(BareLaunch);

            Assert.IsTrue(
                received.HasValue,
                "a resolver registered mid-session takes effect on the very next utterance"
            );
            Assert.AreEqual("alpha", received.Value.GetSlot("track"));
        }

        [Test]
        public void Resolver_EmptyValue_LeavesTheCommandIncomplete()
        {
            // F15, behaviourally rather than on the struct. SlotResolution_DefaultAndEmptyValue_-
            // AreNotResolutions above asserts HasValue is false; this asserts that the recogniser
            // agrees — an empty value is not a half-resolution that fills the slot with "", which
            // would reach a handler as a command whose argument is the empty string.
            ConfigureResolvableSync(allowPartial: true);

            _recogniser.RegisterSlotResolver(
                "track",
                () => new VoxrSlotResolution("", "main target")
            );

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            _recogniser.InjectText(BareLaunch);

            Assert.IsFalse(received.HasValue, "an empty value is not a resolution");
            Assert.IsTrue(pending.HasValue, "so the command is still incomplete and still asks");
            Assert.IsFalse(pending.Value.HasSlot("track"), "and the slot is unfilled, not empty");
            Assert.AreEqual(
                0,
                pending.Value.ResolvedSlots.Length,
                "and nothing is reported as resolver-filled"
            );
        }

        [Test]
        public void Resolver_Throws_TheExceptionPropagatesOutOfInjectText()
        {
            // F20 / D-9. The precedent is BuildEffectiveSlots calling provider() unguarded: a
            // swallowed exception would turn a bug in the game into a resolution that merely
            // returned nothing, and the command would quietly go pending — a working-looking
            // feature with the real failure hidden behind it.
            ConfigureResolvableSync(allowPartial: true);

            var boom = new InvalidOperationException("the game's resolver threw");
            _recogniser.RegisterSlotResolver("track", () => throw boom);

            VoxrCommand? received = null;
            _recogniser.OnCommandRecognised += cmd => received = cmd;
            VoxrCommand? pending = null;
            _recogniser.OnCommandPending += cmd => pending = cmd;

            var thrown = Assert.Throws<InvalidOperationException>(
                () => _recogniser.InjectText(BareLaunch)
            );

            Assert.AreSame(boom, thrown, "the game's own exception, not one wrapped or replaced");
            Assert.IsFalse(received.HasValue, "and nothing fired");
            Assert.IsFalse(
                pending.HasValue,
                "nor was the throw quietly converted into 'no resolution' and a prompt"
            );
        }
    }
}
