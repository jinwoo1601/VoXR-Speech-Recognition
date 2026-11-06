// ============================================================================
// Purpose:  PlayMode parity of the Editor front end with the desktop harness's pinned per-fixture digests
// Layer:    Tests.Runtime
// Owns:     VoxrFrontEndParityTests (public class)
// Depends:  VoxrSpeechRecogniser, EditorMicBackend, voxr_fe_stats, VoxrWavReader, VoxrAudioTestSuiteAsset, VoxrAudioTestCase
// ============================================================================
#if UNITY_EDITOR_WIN
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoXR.Native;
using VoXR.Testing;

namespace VoXR.Tests.Runtime
{
    public class VoxrFrontEndParityTests
    {
        const float PinnedGainDb = -18f;
        const int EvenChunkSamples = 1237;
        const int OddChunkSamples = 4801;

        [System.Serializable]
        class PinFile
        {
            public float gain;
            public Pin[] cases;
        }

        [System.Serializable]
        class Pin
        {
            public string file;
            public string frontEndDigest;
            public long frontEndSamples;
        }

        GameObject _go;
        VoxrSpeechRecogniser _speech;
        List<string> _initErrors;

        static string PackageRoot
        {
            get
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(VoxrSpeechRecogniser).Assembly
                );
                Assert.IsNotNull(pkg, "PackageInfo not found for the VoXR runtime assembly");
                return pkg.resolvedPath;
            }
        }

        static string FixtureRoot => Path.Combine(PackageRoot, "Tests", "Fixtures");

        static VoxrAudioTestCase[] ManifestCases()
        {
            string path = Path.Combine(FixtureRoot, "audio", "manifest.json");
            Assert.IsTrue(File.Exists(path), $"manifest not found: {path}");
            var suite = ScriptableObject.CreateInstance<VoxrAudioTestSuiteAsset>();
            try
            {
                suite.FromJson(File.ReadAllText(path));
                return suite.ToArray();
            }
            finally
            {
                Object.DestroyImmediate(suite);
            }
        }

        static IEnumerable<string> ManifestFileNames()
        {
            foreach (var c in ManifestCases())
                yield return c.file;
        }

        static PinFile LoadPins()
        {
            string path = Path.Combine(PackageRoot, "NativeBridge~", "harness", "expectations.json");
            Assert.IsTrue(File.Exists(path), $"harness expectations not found: {path}");
            return JsonUtility.FromJson<PinFile>(File.ReadAllText(path));
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("FrontEndParityTest");
            _speech = _go.AddComponent<VoxrSpeechRecogniser>();
            _initErrors = new List<string>();
            _speech.OnError += (code, msg) => _initErrors.Add($"{code}: {msg}");

            var init = _speech.InitialiseAsync();
            while (!init.IsCompleted)
                yield return null;

            Assert.IsTrue(
                _speech.IsInitialised,
                "Recogniser failed to initialise — is the VOSK model available to the "
                    + $"host project? Errors: [{string.Join("; ", _initErrors)}]"
            );
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _speech.EditorBackend?.StopPlayback();
            Object.Destroy(_go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Parity_ManifestCase([ValueSource(nameof(ManifestFileNames))] string file)
        {
            int index = System.Array.FindIndex(ManifestCases(), c => c.file == file);
            Assert.GreaterOrEqual(index, 0, $"manifest entry vanished for {file}");
            int chunkSamples = index % 2 == 0 ? EvenChunkSamples : OddChunkSamples;

            PinFile pins = LoadPins();
            Assert.AreEqual(PinnedGainDb, pins.gain, "the harness pins were taken at another target");
            string name = Path.GetFileName(file);
            Pin pin = System.Array.Find(pins.cases, p => p.file == name);
            Assert.IsNotNull(pin, $"no harness pin for {name}");

            float[] samples = VoxrWavReader.ReadFile(
                Path.Combine(FixtureRoot, file.Replace('/', Path.DirectorySeparatorChar))
            );
            var errors = new List<string>();
            var backend = _speech.EditorBackend;
            Assert.IsTrue(
                backend.StartPlayback(samples, 48000, (c, m) => errors.Add(m), chunkSamples),
                $"StartPlayback refused: [{string.Join("; ", errors)}]"
            );

            int guard = 0;
            while (backend.TickPlayback(_speech.EditorDispatcher))
            {
                Assert.Less(++guard, 100000, "playback did not complete");
                yield return null;
            }

            voxr_fe_stats stats = backend.FrontEndStats;
            string context = $"{file}, chunk {chunkSamples}";
            Assert.AreEqual((ulong)samples.Length, stats.input_samples, $"input samples; {context}");
            Assert.AreEqual((ulong)pin.frontEndSamples, stats.output_samples, $"output samples; {context}");
            Assert.AreEqual(pin.frontEndDigest, stats.digest.ToString("x16"), $"digest; {context}");
        }
    }
}
#endif
