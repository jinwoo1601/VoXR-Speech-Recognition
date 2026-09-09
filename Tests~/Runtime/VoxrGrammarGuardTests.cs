// ============================================================================
// Purpose:  PlayMode tests that SetGrammar refuses a malformed grammar before VOSK sees it
// Layer:    Tests.Runtime
// Owns:     VoxrGrammarGuardTests (public class)
// Depends:  VoxrSpeechRecogniser, VoxrBridgeErrorCode
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoXR.Tests.Runtime
{
    // Issue #150: vosk_recognizer_new_grm segfaults on a malformed grammar instead of
    // returning NULL, killing the Editor or the app on device with no catchable exception.
    // VoxrGrammarValidatorTests pins the check itself; these tests pin that SetGrammar
    // actually consults it — before either platform branch, so the Android bridge is
    // covered by the same guard — and that a well-formed grammar is still let through.
    //
    // Not gated on UNITY_EDITOR_WIN: the guard sits above both branches, and the
    // recogniser is deliberately left uninitialised, so no backend and no model is needed.
    public class VoxrGrammarGuardTests
    {
        static readonly Regex GrammarRejected = new Regex("rejected without being applied");

        GameObject _go;
        VoxrSpeechRecogniser _recogniser;
        List<VoxrBridgeErrorCode> _errorCodes;
        List<string> _errors;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GrammarGuardRecogniser");
            _recogniser = _go.AddComponent<VoxrSpeechRecogniser>();
            _errorCodes = new List<VoxrBridgeErrorCode>();
            _errors = new List<string>();
            _recogniser.OnError += (code, msg) =>
            {
                _errorCodes.Add(code);
                _errors.Add($"{code}: {msg}");
            };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null)
                Object.Destroy(_go);
            yield return null;
        }

        [Test]
        public void SetGrammar_ObjectShapedGrammar_RejectedLoudly()
        {
            LogAssert.Expect(LogType.Error, GrammarRejected);

            _recogniser.SetGrammar("{\"grammar\": [\"[unk]\"]}");

            AssertSingleRejection();
        }

        [Test]
        public void SetGrammar_EmptyArray_RejectedLoudly()
        {
            LogAssert.Expect(LogType.Error, GrammarRejected);

            _recogniser.SetGrammar("[]");

            AssertSingleRejection();
        }

        [Test]
        public void SetGrammar_WellFormedGrammar_RaisesNothing()
        {
            _recogniser.SetGrammar("[\"[unk]\", \"fire\"]");

            Assert.IsEmpty(
                _errors,
                $"A well-formed grammar must pass the guard: [{string.Join("; ", _errors)}]"
            );
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SetGrammar_NullOrEmpty_RaisesNothing()
        {
            // Null and empty are the documented "clear the grammar" request — both backends
            // fall through to vosk_recognizer_new — so the guard must not swallow them.
            _recogniser.SetGrammar(null);
            _recogniser.SetGrammar("");

            Assert.IsEmpty(
                _errors,
                $"Clearing the grammar must not be rejected: [{string.Join("; ", _errors)}]"
            );
            LogAssert.NoUnexpectedReceived();
        }

        void AssertSingleRejection()
        {
            Assert.AreEqual(
                1,
                _errors.Count,
                $"Expected exactly one rejection: [{string.Join("; ", _errors)}]"
            );
            Assert.AreEqual(VoxrBridgeErrorCode.ModelLoadFailed, _errorCodes[0]);
            StringAssert.Contains("vosk_recognizer_new_grm", _errors[0]);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
