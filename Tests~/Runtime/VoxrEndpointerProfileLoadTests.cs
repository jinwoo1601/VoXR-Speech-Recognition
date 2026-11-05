// ============================================================================
// Purpose:  PlayMode tests for the endpointer profile applied and logged at model load
// Layer:    Tests.Runtime
// Owns:     VoxrEndpointerProfileLoadTests (public class)
// Depends:  VoxrSpeechRecogniser, VoxrEndpointerProfile, EndpointerOverlay, ModelExtractor,
//           VoxrBridgeErrorCode, VoxrListeningMode
// ============================================================================
#if UNITY_EDITOR_WIN
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoXR.Tests.Runtime
{
    public class VoxrEndpointerProfileLoadTests
    {
        const string PlantFileName = "voxr-endpointer-cache-plant";

        GameObject _go;
        VoxrSpeechRecogniser _speech;
        List<string> _initErrors;
        string _dir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("EndpointerProfileLoadTest");
            _speech = _go.AddComponent<VoxrSpeechRecogniser>();
            _initErrors = new List<string>();
            _speech.OnError += (code, msg) => _initErrors.Add($"{code}: {msg}");

            string modelRelativePath = (string)
                typeof(VoxrSpeechRecogniser)
                    .GetField("modelRelativePath", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_speech);
            var extract = ModelExtractor.ExtractModelAsync(
                modelRelativePath,
                (code, msg) => _initErrors.Add($"{code}: {msg}")
            );
            while (!extract.IsCompleted)
                yield return null;

            _dir = extract.Result;
            Assert.IsNotNull(
                _dir,
                "The model could not be extracted — is the VOSK model available to the host "
                    + $"project? Errors: [{string.Join("; ", _initErrors)}]"
            );
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_go);
            yield return null;

            if (_dir != null)
            {
                EndpointerOverlay.Apply(_dir, EndpointerOverlay.ModelDefault);

                string plant = Path.Combine(_dir, PlantFileName);
                if (File.Exists(plant))
                    File.Delete(plant);
            }
        }

        void SetProfile(VoxrEndpointerProfile profile)
        {
            typeof(VoxrSpeechRecogniser)
                .GetField("endpointerProfile", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_speech, profile);
        }

        void AssertInitialised()
        {
            Assert.IsTrue(
                _speech.IsInitialised,
                "Recogniser failed to initialise — is the VOSK model available to the "
                    + $"host project? Errors: [{string.Join("; ", _initErrors)}]"
            );
        }

        string ConfPath => Path.Combine(_dir, "conf", "model.conf");

        string CopyPath => Path.Combine(_dir, EndpointerOverlay.ArchiveCopyFileName);

        [UnityTest]
        public IEnumerator Load_Default_LogsLine()
        {
            SetProfile(VoxrEndpointerProfile.Automatic);
            LogAssert.Expect(
                LogType.Log,
                "[VoxrSpeechRecogniser] Endpointer profile Responsive (Continuous default): "
                    + "rule2=0.45 rule3=0.75 rule4=1.0"
            );

            var init = _speech.InitialiseAsync();
            while (!init.IsCompleted)
                yield return null;
            AssertInitialised();

            string[] expected = File.ReadAllLines(CopyPath);
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i].StartsWith(EndpointerOverlay.Rule2Key + "="))
                    expected[i] = EndpointerOverlay.Rule2Key + "=0.45";
            }

            CollectionAssert.AreEqual(
                expected,
                File.ReadAllLines(ConfPath),
                "The Continuous default is Responsive: the archive's conf, rule2 at 0.45."
            );
        }

        [UnityTest]
        public IEnumerator Load_Responsive_Rule2Is045()
        {
            SetProfile(VoxrEndpointerProfile.Responsive);
            LogAssert.Expect(
                LogType.Log,
                "[VoxrSpeechRecogniser] Endpointer profile Responsive (explicit choice): "
                    + "rule2=0.45 rule3=0.75 rule4=1.0"
            );

            var init = _speech.InitialiseAsync();
            while (!init.IsCompleted)
                yield return null;
            AssertInitialised();

            CollectionAssert.Contains(
                File.ReadAllLines(ConfPath),
                EndpointerOverlay.Rule2Key + "=0.45",
                "The decoder's conf must carry the preset's rule2."
            );
        }

        [UnityTest]
        public IEnumerator Load_ModelDefault_CacheHit()
        {
            SetProfile(VoxrEndpointerProfile.ModelDefault);
            const string Line =
                "[VoxrSpeechRecogniser] Endpointer profile ModelDefault (explicit choice): "
                + "rule2=0.5 rule3=0.75 rule4=1.0";
            LogAssert.Expect(LogType.Log, Line);
            LogAssert.Expect(LogType.Log, Line);

            var first = _speech.InitialiseAsync();
            while (!first.IsCompleted)
                yield return null;
            AssertInitialised();

            string stampPath = Path.Combine(_dir, ModelExtractor.StampFileName);
            byte[] stamp = File.ReadAllBytes(stampPath);

            string plant = Path.Combine(_dir, PlantFileName);
            File.WriteAllText(plant, "planted");

            _speech.ReleaseNativeResources();
            var second = _speech.InitialiseAsync();
            while (!second.IsCompleted)
                yield return null;
            AssertInitialised();

            Assert.IsTrue(File.Exists(plant), "Load 2 must serve the cache, not re-extract.");
            CollectionAssert.AreEqual(
                stamp,
                File.ReadAllBytes(stampPath),
                "Applying the profile must leave the stamp untouched (F9)."
            );
            CollectionAssert.AreEqual(
                File.ReadAllBytes(CopyPath),
                File.ReadAllBytes(ConfPath),
                "Model default's conf is the archive's copy."
            );
        }

        [UnityTest]
        public IEnumerator Load_PushToTalkHint_ModelDefault()
        {
            SetProfile(VoxrEndpointerProfile.Automatic);
            _speech.ListeningModeHint = VoxrListeningMode.PushToTalk;
            LogAssert.Expect(
                LogType.Log,
                "[VoxrSpeechRecogniser] Endpointer profile ModelDefault (Push-to-talk default): "
                    + "rule2=0.5 rule3=0.75 rule4=1.0"
            );

            var init = _speech.InitialiseAsync();
            while (!init.IsCompleted)
                yield return null;
            AssertInitialised();

            CollectionAssert.AreEqual(
                File.ReadAllBytes(CopyPath),
                File.ReadAllBytes(ConfPath),
                "The push-to-talk default is Model default: the archive's own conf."
            );
        }

        [UnityTest]
        public IEnumerator Load_UnparseableConf_Warns()
        {
            EndpointerOverlay.Apply(_dir, EndpointerOverlay.ModelDefault);
            byte[] archive = File.ReadAllBytes(CopyPath);
            try
            {
                File.AppendAllText(CopyPath, "\n" + EndpointerOverlay.Rule2Key + "=0.6\n");

                SetProfile(VoxrEndpointerProfile.Responsive);
                LogAssert.Expect(
                    LogType.Warning,
                    "[VoxrSpeechRecogniser] Endpointer profile Responsive (explicit choice): "
                        + "rule2=0.6 rule3=0.75 rule4=1.0 — not applied: "
                        + EndpointerOverlay.Rule2Key
                        + " appears more than once ("
                        + ConfPath
                        + ")"
                );

                var init = _speech.InitialiseAsync();
                while (!init.IsCompleted)
                    yield return null;
                AssertInitialised();

                CollectionAssert.AreEqual(
                    File.ReadAllBytes(CopyPath),
                    File.ReadAllBytes(ConfPath),
                    "A conf that cannot be parsed is served as the archive's own bytes."
                );
            }
            finally
            {
                File.WriteAllBytes(CopyPath, archive);
            }
        }

        [UnityTest]
        public IEnumerator Load_ModelDefaultUnparseableConf_Warns()
        {
            EndpointerOverlay.Apply(_dir, EndpointerOverlay.ModelDefault);
            byte[] archive = File.ReadAllBytes(CopyPath);
            try
            {
                File.AppendAllText(CopyPath, "\n" + EndpointerOverlay.Rule2Key + "=0.6\n");

                SetProfile(VoxrEndpointerProfile.ModelDefault);
                LogAssert.Expect(
                    LogType.Warning,
                    "[VoxrSpeechRecogniser] Endpointer profile ModelDefault (explicit choice): "
                        + "rule2=0.6 rule3=0.75 rule4=1.0 — nothing to apply, but the conf has a problem: "
                        + EndpointerOverlay.Rule2Key
                        + " appears more than once ("
                        + ConfPath
                        + ")"
                );

                var init = _speech.InitialiseAsync();
                while (!init.IsCompleted)
                    yield return null;
                AssertInitialised();

                CollectionAssert.AreEqual(
                    File.ReadAllBytes(CopyPath),
                    File.ReadAllBytes(ConfPath),
                    "A conf that cannot be parsed is served as the archive's own bytes."
                );
            }
            finally
            {
                File.WriteAllBytes(CopyPath, archive);
            }
        }
    }
}
#endif
