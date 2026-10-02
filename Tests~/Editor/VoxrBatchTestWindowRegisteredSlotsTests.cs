// ============================================================================
// Purpose:  EditMode tests that the batch test window forwards registeredSlotNames to its runner
// Layer:    Tests.Editor
// Owns:     VoxrBatchTestWindowRegisteredSlotsTests (public class)
// Depends:  VoxrBatchTestWindow, VoxrBatchTestRunner, VoxrSlotAsset, VoxrCommandAsset, VoxrCommandSetAsset
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoXR.Commands;
using VoXR.Editor;
using VoXR.Testing;

namespace VoXR.Tests.Editor
{
    // F11: the window carries the game's registered slot names as a serialized field and hands
    // them to VoxrBatchTestRunner, so a corpus run scores a registered required slot as the
    // runtime does and reports "would ask resolver for '…'" where every unfilled required slot
    // is a registered one.
    //
    // The witness is the runner tests' long form — eight required elements, nothing optional —
    // built here from assets so the window's BuildSlots / BuildSets path is the one exercised.
    // "launch all missiles from tube three at" matches seven and strands {target}. Registered,
    // {target} is exempt from the score: 7 / 7 = 1.00. Unregistered, it is charged: (7 - 1) / 8
    // = 0.75, which clears the default minScore of 0.60, so the completeness gate is the reason.
    //
    // Both of CreateRunner's constructor calls are covered — the window picks between them on
    // whether activeSetNames is populated, and each carries the names separately.
    //
    // The window's fields and CreateRunner are private, which is the seam this reaches through:
    // testing the forwarding is not a reason to widen the window's own surface.
    public class VoxrBatchTestWindowRegisteredSlotsTests
    {
        const string IncompleteAboveGate = "launch all missiles from tube three at";

        VoxrBatchTestWindow _window;
        VoxrSlotAsset[] _slotAssets;
        VoxrCommandAsset _commandAsset;
        VoxrCommandSetAsset _setAsset;

        [SetUp]
        public void SetUp()
        {
            _slotAssets = new[]
            {
                CreateSlot("weapon", "missiles", "torpedoes"),
                CreateSlot("target", "hotel one", "hotel two"),
                CreateSlot("quantity", "all", "one", "two"),
                CreateSlot("tube", "one", "two", "three"),
            };

            _commandAsset = ScriptableObject.CreateInstance<VoxrCommandAsset>();
            _commandAsset.intent = "launch_weapon";
            _commandAsset.patterns = new[]
            {
                "launch {quantity} {weapon} from tube {tube} at {target}",
            };

            _setAsset = ScriptableObject.CreateInstance<VoxrCommandSetAsset>();
            _setAsset.setName = "combat";
            _setAsset.commands = new[] { _commandAsset };

            _window = ScriptableObject.CreateInstance<VoxrBatchTestWindow>();
            SetField("slotAssets", _slotAssets);
            SetField("commandSetAssets", new[] { _setAsset });
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
                UnityEngine.Object.DestroyImmediate(_window);
            if (_setAsset != null)
                UnityEngine.Object.DestroyImmediate(_setAsset);
            if (_commandAsset != null)
                UnityEngine.Object.DestroyImmediate(_commandAsset);
            if (_slotAssets != null)
                foreach (var slotAsset in _slotAssets)
                    if (slotAsset != null)
                        UnityEngine.Object.DestroyImmediate(slotAsset);
        }

        static VoxrSlotAsset CreateSlot(string slotName, params string[] values)
        {
            var asset = ScriptableObject.CreateInstance<VoxrSlotAsset>();
            asset.slotName = slotName;
            asset.values = values;
            return asset;
        }

        static FieldInfo WindowField(string name)
        {
            var field = typeof(VoxrBatchTestWindow).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field, $"the window must carry a '{name}' field");
            return field;
        }

        void SetField(string name, object value) => WindowField(name).SetValue(_window, value);

        // Selects which constructor CreateRunner reaches: an explicit active-set filter, or the
        // all-sets fallback it builds when none is set.
        void UseActiveSetNames(bool explicitFilter) =>
            SetField(
                "activeSetNames",
                explicitFilter ? new[] { _setAsset.setName } : Array.Empty<string>()
            );

        VoxrBatchTestRunner CreateRunner()
        {
            var method = typeof(VoxrBatchTestWindow).GetMethod(
                "CreateRunner",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(method, "the window must build its runner in CreateRunner");

            var runner = (VoxrBatchTestRunner)method.Invoke(_window, null);
            Assert.IsNotNull(runner, "the window failed to build a runner from the test fixture");
            return runner;
        }

        static VoxrTestCase IncompleteCase() =>
            new VoxrTestCase { input = IncompleteAboveGate, expectedIntent = "launch_weapon" };

        // F11, review finding GAP-2: the names are a serialized array field — Unity's serializer
        // sees it, which a plain private field would not pass. Whether the list survives a domain
        // reload itself remains the human's manual Editor check.
        [Test]
        public void RegisteredSlotNames_SerializedObject_IsArrayProperty()
        {
            using (var serialized = new SerializedObject(_window))
            {
                var property = serialized.FindProperty("registeredSlotNames");

                Assert.IsNotNull(
                    property,
                    "the window must carry a serialized 'registeredSlotNames' field"
                );
                Assert.IsTrue(property.isArray, "'registeredSlotNames' must serialize as an array");
            }
        }

        // F11: with {target} registered, the window's runner exempts it from the score and
        // reports the point at which the runtime would consult a resolver.
        [TestCase(true)]
        [TestCase(false)]
        public void CreateRunner_RegisteredSlotNames_ReportsWouldAskResolver(bool explicitFilter)
        {
            UseActiveSetNames(explicitFilter);
            SetField("registeredSlotNames", new[] { "target" });

            var result = CreateRunner().Run(IncompleteCase());

            Assert.IsFalse(result.Passed, "a resolver verdict is a rejection");
            Assert.AreEqual(
                "expected intent 'launch_weapon' but rejected: would ask resolver for 'target'",
                result.FailureReason
            );
            Assert.AreEqual(1f, result.Score, 0.001f, "7 matched, {target} exempt: 7 / 7");
        }

        // F11: with no names the window's runner rules on the utterance alone, as before —
        // {target} is charged and the completeness gate refuses the command.
        [TestCase(true)]
        [TestCase(false)]
        public void CreateRunner_NoRegisteredSlotNames_KeepsRequiredSlotUnfilled(
            bool explicitFilter
        )
        {
            UseActiveSetNames(explicitFilter);
            SetField("registeredSlotNames", null);

            var result = CreateRunner().Run(IncompleteCase());

            Assert.IsFalse(result.Passed, "an incomplete command is refused");
            StringAssert.EndsWith("required slot unfilled", result.FailureReason);
            Assert.AreEqual(6f / 8f, result.Score, 0.001f, "{target} charged: (7 - 1) / 8");
        }
    }
}
