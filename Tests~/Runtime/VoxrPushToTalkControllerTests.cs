using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoXR;
using VoXR.Commands;

namespace VoXR.Tests.Runtime
{
    public class VoxrPushToTalkControllerTests
    {
        GameObject _go;
        VoxrPushToTalkController _controller;
        VoxrSpeechRecogniser _speech;
        VoxrCommandRecogniser _command;

        GameObject _authoredGo;

        GameObject _countingGo;

        int _startedCount;
        int _endedCount;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPTTController");
            _speech = _go.AddComponent<VoxrSpeechRecogniser>();
            _command = _go.AddComponent<VoxrCommandRecogniser>();
            _controller = _go.AddComponent<VoxrPushToTalkController>();

            _controller.SpeechRecogniser = _speech;
            _controller.CommandRecogniser = _command;
            _controller.InitialiseOnStart = false;
            _command.SpeechRecogniser = _speech;

            _startedCount = 0;
            _endedCount = 0;
            _controller.OnTalkStarted.AddListener(() => _startedCount++);
            _controller.OnTalkEnded.AddListener(() => _endedCount++);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            if (_authoredGo != null)
                Object.DestroyImmediate(_authoredGo);
            _authoredGo = null;
            if (_countingGo != null)
                Object.DestroyImmediate(_countingGo);
            _countingGo = null;
        }

        static VoxrSlotDefinition[] MakeSlots() => new[]
        {
            new VoxrSlotDefinition("target", new[] { "hotel one", "alpha one" }),
            new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
        };

        static VoxrCommandDefinition[] MakeCommands(bool allowPartial = false) => new[]
        {
            new VoxrCommandDefinition("launch_weapon",
                new[] { new[] { "launch", "{weapon}", "target", "{target}" } },
                allowPartial, false),
            new VoxrCommandDefinition("cease_fire",
                new[] { new[] { "cease", "fire" } }),
        };

        void ConfigureBuffered()
        {
            _command.Configure(MakeSlots(), MakeCommands());
            _command.BufferWindow = 1.5f;
            _command.CommandCooldown = 0f;
        }

        void ConfigureSync(bool allowPartial = false)
        {
            _command.Configure(MakeSlots(), MakeCommands(allowPartial));
            _command.BufferWindow = 0f;
            _command.CommandCooldown = 0f;
            _command.PendingTimeout = 30f;
        }

        [Test]
        public void AddComponent_DefaultState_ListeningModeIsPushToTalk()
        {
            Assert.AreEqual(VoxrListeningMode.PushToTalk, _controller.ListeningMode);
        }

        [Test]
        public void PressTalk_SpeechRecogniserNull_IsNoOp()
        {
            _controller.SpeechRecogniser = null;

            Assert.DoesNotThrow(() => _controller.PressTalk());
            Assert.AreEqual(0, _startedCount);
        }

        [Test]
        public void PressTalk_FirstPress_FiresOnTalkStarted()
        {
            _controller.PressTalk();
            Assert.AreEqual(1, _startedCount);
        }

        [Test]
        public void PressTalk_CalledTwice_FiresOnTalkStartedOnce()
        {
            _controller.PressTalk();
            _controller.PressTalk();
            Assert.AreEqual(1, _startedCount);
        }

        [Test]
        public void ReleaseTalk_WithoutPriorPress_IsNoOp()
        {
            _controller.ReleaseTalk();
            Assert.AreEqual(0, _endedCount);
        }

        [Test]
        public void ReleaseTalk_AfterPress_FiresOnTalkEnded()
        {
            _controller.PressTalk();
            _controller.ReleaseTalk();
            Assert.AreEqual(1, _endedCount);
        }

        [Test]
        public void ReleaseTalk_WithCommandRecogniser_CallsFlushPendingBuffer()
        {
            ConfigureBuffered();

            int recognised = 0;
            _command.OnCommandRecognised += _ => recognised++;

            _controller.PressTalk();
            _command.InjectText("cease fire");
            Assert.AreEqual(0, recognised,
                "Buffered injection must not fire before release");

            _controller.ReleaseTalk();
            Assert.AreEqual(1, recognised,
                "Release must flush the buffered command through the command recogniser");
        }

        [Test]
        public void ReleaseTalk_CancelPendingFalse_DoesNotCallCancelPendingCommand()
        {
            ConfigureSync(allowPartial: true);
            _controller.CancelPendingOnRelease = false;

            int cancelled = 0;
            _command.OnCommandCancelled += _ => cancelled++;

            _controller.PressTalk();
            _command.InjectText("launch missiles target");
            Assume.That(_command.HasPendingCommand, Is.True,
                "Precondition: command recogniser should be in pending state");

            _controller.ReleaseTalk();

            Assert.IsTrue(_command.HasPendingCommand,
                "Pending command should survive release when CancelPendingOnRelease is false");
            Assert.AreEqual(0, cancelled);
        }

        [Test]
        public void ReleaseTalk_CancelPendingTrue_CallsCancelPendingCommand()
        {
            ConfigureSync(allowPartial: true);
            _controller.CancelPendingOnRelease = true;

            int cancelled = 0;
            _command.OnCommandCancelled += _ => cancelled++;

            _controller.PressTalk();
            _command.InjectText("launch missiles target");
            Assume.That(_command.HasPendingCommand, Is.True,
                "Precondition: command recogniser should be in pending state");

            _controller.ReleaseTalk();

            Assert.IsFalse(_command.HasPendingCommand,
                "Pending command should be cancelled on release when the flag is true");
            Assert.AreEqual(1, cancelled);
        }

        [Test]
        public void ListeningMode_SetToContinuous_FiresOnTalkStarted()
        {
            _controller.ListeningMode = VoxrListeningMode.Continuous;

            Assert.AreEqual(VoxrListeningMode.Continuous, _controller.ListeningMode);
            Assert.AreEqual(1, _startedCount);
        }

        [Test]
        public void ListeningMode_SetToPushToTalk_WhenPressed_FiresOnTalkEnded()
        {
            _controller.ListeningMode = VoxrListeningMode.Continuous;
            Assume.That(_startedCount, Is.EqualTo(1));

            _controller.ListeningMode = VoxrListeningMode.PushToTalk;

            Assert.AreEqual(VoxrListeningMode.PushToTalk, _controller.ListeningMode);
            Assert.AreEqual(1, _endedCount);
        }

        [Test]
        public void PressTalk_InContinuousMode_IsNoOp()
        {
            _controller.ListeningMode = VoxrListeningMode.Continuous;
            int startedAfterMode = _startedCount;

            _controller.PressTalk();

            Assert.AreEqual(startedAfterMode, _startedCount,
                "PressTalk must not re-fire OnTalkStarted in continuous mode");
        }

        [Test]
        public void OnDisable_WhilePressed_DoesNotFireOnTalkEnded()
        {
            _controller.PressTalk();
            Assume.That(_startedCount, Is.EqualTo(1));

            _controller.enabled = false;

            Assert.AreEqual(0, _endedCount,
                "OnDisable is a lifecycle event — OnTalkEnded must not fire");
        }

        [Test]
        public void OnDisable_InContinuousMode_PreservesWantRecognisingFlag()
        {
            _controller.ListeningMode = VoxrListeningMode.Continuous;
            Assume.That(_startedCount, Is.EqualTo(1));

            _controller.enabled = false;
            _controller.enabled = true;

            Assert.AreEqual(1, _startedCount,
                "Re-enable must not re-fire OnTalkStarted (lifecycle resume is silent)");
            Assert.AreEqual(0, _endedCount);

            _controller.ListeningMode = VoxrListeningMode.PushToTalk;

            Assert.AreEqual(1, _endedCount,
                "Switching back to PushToTalk after a disable/enable cycle must fire " +
                "OnTalkEnded exactly once — proves _wantRecognising was preserved");
        }

        [Test]
        public void OnEnable_WhenWantRecognisingTrue_DoesNotFireOnTalkStarted()
        {
            _controller.ListeningMode = VoxrListeningMode.Continuous;
            int startedBefore = _startedCount;

            _controller.enabled = false;
            _controller.enabled = true;

            Assert.AreEqual(startedBefore, _startedCount);
        }

        VoxrPushToTalkController AuthorContinuousInactive(bool componentEnabled = true)
        {
            _authoredGo = new GameObject("AuthoredContinuousPTT");
            _authoredGo.SetActive(false);

            var speech = _authoredGo.AddComponent<VoxrSpeechRecogniser>();
            var controller = _authoredGo.AddComponent<VoxrPushToTalkController>();

            controller.SpeechRecogniser = speech;
            controller.InitialiseOnStart = false;
            controller.InitialMode = VoxrListeningMode.Continuous;
            controller.enabled = componentEnabled;

            return controller;
        }

        [Test]
        public void AuthoredContinuous_FirstEnable_FiresOnTalkStarted()
        {
            var controller = AuthorContinuousInactive();
            int started = 0;
            int ended = 0;
            controller.OnTalkStarted.AddListener(() => started++);
            controller.OnTalkEnded.AddListener(() => ended++);

            _authoredGo.SetActive(true);

            Assert.AreEqual(
                1,
                started,
                "A scene authored on Continuous starts recognition on enable, so it must "
                    + "announce it exactly once — an indicator wired only to OnTalkStarted is "
                    + "otherwise dark while the mic is live"
            );
            Assert.AreEqual(0, ended);
        }

        [Test]
        public void AuthoredContinuous_DisableEnableCycle_DoesNotRefireOnTalkStarted()
        {
            var controller = AuthorContinuousInactive();
            int started = 0;
            controller.OnTalkStarted.AddListener(() => started++);

            _authoredGo.SetActive(true);
            Assume.That(started, Is.EqualTo(1), "Precondition: the authored start announced once");

            controller.enabled = false;
            controller.enabled = true;

            Assert.AreEqual(
                1,
                started,
                "The startup announcement is a one-off: a lifecycle resume must stay silent, "
                    + "exactly as it does for a press or a runtime switch to Continuous"
            );
        }

        [Test]
        public void AuthoredContinuous_ThenSwitchToPushToTalk_FiresOnTalkEndedOnce()
        {
            var controller = AuthorContinuousInactive();
            int ended = 0;
            controller.OnTalkEnded.AddListener(() => ended++);

            _authoredGo.SetActive(true);
            controller.ListeningMode = VoxrListeningMode.PushToTalk;

            Assert.AreEqual(
                1,
                ended,
                "Proves the authored enable recorded the want-to-recognise flag, not just "
                    + "the event — the switch away only stops and fires when that flag is set"
            );
        }

        [Test]
        public void AuthoredContinuous_ComponentDisabled_AnnouncesOnlyWhenEnabled()
        {
            var controller = AuthorContinuousInactive(componentEnabled: false);
            int started = 0;
            int ended = 0;
            controller.OnTalkStarted.AddListener(() => started++);
            controller.OnTalkEnded.AddListener(() => ended++);

            _authoredGo.SetActive(true);

            Assert.AreEqual(
                0,
                started,
                "A disabled component starts no recognition, so it must announce nothing — "
                    + "activating the GameObject does not reach OnEnable"
            );

            controller.enabled = true;

            Assert.AreEqual(
                1,
                started,
                "Ticking the checkbox is where recognition actually begins, so that is where "
                    + "the announcement belongs — deferred, not lost"
            );
            Assert.AreEqual(0, ended);
        }

        [Test]
        public void AuthoredContinuous_ComponentDisabled_SwitchToPushToTalkIsSilent()
        {
            var controller = AuthorContinuousInactive(componentEnabled: false);
            int ended = 0;
            controller.OnTalkEnded.AddListener(() => ended++);

            _authoredGo.SetActive(true);
            controller.ListeningMode = VoxrListeningMode.PushToTalk;

            Assert.AreEqual(
                0,
                ended,
                "Switching away before the component was ever enabled must be silent: nothing "
                    + "had started, and nothing had announced a start for this to pair with"
            );
        }

        sealed class CountingSpeechRecogniser : VoxrSpeechRecogniser
        {
            internal int StartCalls { get; private set; }
            internal int StopCalls { get; private set; }

            internal override void StartRecognitionCore() => StartCalls++;

            internal override void StopRecognitionCore() => StopCalls++;

            // Keeps the base's private OnDestroy — and its ReleaseNativeResources() — off
            // this double, exactly as VoxrPushToTalkPauseTests does. Load-bearing: Unity runs
            // a base class's privately-declared messages on a subclass.
            void OnDestroy() { }
        }

        CountingSpeechRecogniser UseCountingRecogniser()
        {
            _countingGo = new GameObject("CountingRecogniser");
            var counting = _countingGo.AddComponent<CountingSpeechRecogniser>();
            _controller.SpeechRecogniser = counting;
            return counting;
        }

        static void AssumeDestroyedButAlive(VoxrSpeechRecogniser recogniser)
        {
            Assume.That(recogniser == null, Is.True, "Precondition: the recogniser reads as null");
            Assume.That(
                ReferenceEquals(recogniser, null),
                Is.False,
                "Precondition: the managed wrapper is still alive — reading as null while "
                    + "still referenced is the exact state `?.` mishandles, and a plain null "
                    + "would make this test a duplicate of the null case below"
            );
        }

        [Test]
        public void ListeningMode_SetToContinuous_RecogniserDestroyed_IsNoOp()
        {
            var recogniser = UseCountingRecogniser();
            Object.DestroyImmediate(recogniser);
            AssumeDestroyedButAlive(recogniser);

            _controller.ListeningMode = VoxrListeningMode.Continuous;

            Assert.AreEqual(
                0,
                recogniser.StartCalls,
                "Switching to Continuous must skip a destroyed recogniser, not dispatch into it"
            );
            Assert.AreEqual(
                0,
                _startedCount,
                "Nothing started, so nothing may announce a start — the same pairing PressTalk "
                    + "and the authored-Continuous enable already keep"
            );
        }

        [Test]
        public void ListeningMode_SetToPushToTalk_RecogniserDestroyed_IsNoOp()
        {
            var recogniser = UseCountingRecogniser();

            _controller.ListeningMode = VoxrListeningMode.Continuous;
            Assume.That(recogniser.StartCalls, Is.EqualTo(1), "Precondition: the live start ran");
            Assume.That(_startedCount, Is.EqualTo(1), "Precondition: the live start announced");

            Object.DestroyImmediate(recogniser);
            AssumeDestroyedButAlive(recogniser);

            _controller.ListeningMode = VoxrListeningMode.PushToTalk;

            Assert.AreEqual(
                0,
                recogniser.StopCalls,
                "Switching back must skip a destroyed recogniser, not dispatch into it"
            );
            Assert.AreEqual(
                0,
                _endedCount,
                "Nothing stopped, so nothing may announce a stop — ReleaseTalk is already "
                    + "silent on a missing recogniser for the same reason"
            );
        }

        [Test]
        public void ListeningMode_SetToContinuous_RecogniserNull_IsNoOp()
        {
            _controller.SpeechRecogniser = null;

            Assert.DoesNotThrow(() => _controller.ListeningMode = VoxrListeningMode.Continuous);
            Assert.AreEqual(0, _startedCount);
        }

        [UnityTest]
        public IEnumerator OnDestroy_Cleanup_DoesNotThrow()
        {
            _controller.PressTalk();
            Object.DestroyImmediate(_go);
            _go = null;
            yield return null;
        }

        [Test]
        public void ReleaseTalk_SiblingTieDeferredByTheEagerGate_StillFires()
        {
            // Issue #74 DR-5 has the eager gate refuse on a sibling tie, so a buffer that used
            // to commit early now waits. Releasing the trigger is the one path where "waits"
            // could have meant "is discarded" — OnTalkEnded calls CancelPendingCommand as well
            // as FlushPendingBuffer, and only the order between them keeps the command alive.
            //
            // The discriminator is MEDIAL: a trailing one is refused by the issue #70 tail
            // condition instead, so it would not exercise the sibling refusal at all.
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("differ only at element 3")
            );

            _command.Configure(
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
            _command.BufferWindow = 1.5f;
            _command.CommandCooldown = 0f;
            _command.EagerFlushOnCompleteMatch = true;

            VoxrCommand? received = null;
            _command.OnCommandRecognised += cmd => received = cmd;

            _controller.PressTalk();
            _command.InjectText("set alpha on");
            Assert.IsFalse(received.HasValue, "the tie must defer rather than commit early");

            _controller.ReleaseTalk();

            Assert.IsTrue(
                received.HasValue,
                "releasing must flush the deferred command, not discard it"
            );
            Assert.AreEqual("set_mode", received.Value.Intent);
        }

        [Test]
        public void Hint_NoController_Continuous()
        {
            var go = new GameObject("NoControllerRecogniser");
            try
            {
                var speech = go.AddComponent<VoxrSpeechRecogniser>();

                Assert.AreEqual(
                    VoxrListeningMode.Continuous,
                    speech.ListeningModeHint,
                    "A recogniser no controller drives loads as Continuous"
                );
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Hint_Awake_HandsOff()
        {
            var go = new GameObject("AwakeHandOffPTT");
            go.SetActive(false);
            try
            {
                var speech = go.AddComponent<VoxrSpeechRecogniser>();
                var controller = go.AddComponent<VoxrPushToTalkController>();
                controller.SpeechRecogniser = speech;
                controller.InitialiseOnStart = false;

                speech.ListeningModeHint = VoxrListeningMode.Continuous;
                go.SetActive(true);

                Assert.AreEqual(
                    VoxrListeningMode.PushToTalk,
                    speech.ListeningModeHint,
                    "Awake must hand the mode over before any OnEnable can begin a load"
                );
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Hint_ModeSetter_HandsOff()
        {
            Assume.That(_speech.ListeningModeHint, Is.EqualTo(VoxrListeningMode.PushToTalk));

            _controller.ListeningMode = VoxrListeningMode.Continuous;
            Assert.AreEqual(VoxrListeningMode.Continuous, _speech.ListeningModeHint);

            _controller.ListeningMode = VoxrListeningMode.PushToTalk;
            Assert.AreEqual(
                VoxrListeningMode.PushToTalk,
                _speech.ListeningModeHint,
                "A mode change must reach the next load"
            );
        }

        [Test]
        public void Hint_InitialMode_HandsOff()
        {
            Assume.That(_speech.ListeningModeHint, Is.EqualTo(VoxrListeningMode.PushToTalk));

            _controller.InitialMode = VoxrListeningMode.Continuous;

            Assert.AreEqual(VoxrListeningMode.Continuous, _speech.ListeningModeHint);
        }

        [Test]
        public void Hint_Swap_ResetsOld()
        {
            Assume.That(_speech.ListeningModeHint, Is.EqualTo(VoxrListeningMode.PushToTalk));

            var counting = UseCountingRecogniser();

            Assert.AreEqual(
                VoxrListeningMode.Continuous,
                _speech.ListeningModeHint,
                "A recogniser the controller let go of loads as Continuous again"
            );
            Assert.AreEqual(VoxrListeningMode.PushToTalk, counting.ListeningModeHint);
        }

        [Test]
        public void Hint_OnDestroy_Continuous()
        {
            Assume.That(_speech.ListeningModeHint, Is.EqualTo(VoxrListeningMode.PushToTalk));

            Object.DestroyImmediate(_controller);

            Assert.AreEqual(
                VoxrListeningMode.Continuous,
                _speech.ListeningModeHint,
                "A recogniser whose controller is gone loads as Continuous"
            );
        }

        [Test]
        public void Hint_PressTalk_HandsOff()
        {
            _speech.ListeningModeHint = VoxrListeningMode.Continuous;

            _controller.PressTalk();

            Assert.AreEqual(
                VoxrListeningMode.PushToTalk,
                _speech.ListeningModeHint,
                "A press must hand the mode over before the start it begins"
            );
        }

        [UnityTest]
        public IEnumerator Hint_Start_HandsOff()
        {
            var go = new GameObject("StartHandOffPTT");
            go.SetActive(false);
            try
            {
                var speech = go.AddComponent<VoxrSpeechRecogniser>();
                var controller = go.AddComponent<VoxrPushToTalkController>();
                controller.SpeechRecogniser = speech;
                controller.InitialiseOnStart = true;
                go.SetActive(true);

                speech.ListeningModeHint = VoxrListeningMode.Continuous;
                yield return null;

                Assert.AreEqual(
                    VoxrListeningMode.PushToTalk,
                    speech.ListeningModeHint,
                    "Start must hand the mode over before the load it begins"
                );
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Hint_OnEnable_HandsOff()
        {
            var controller = AuthorContinuousInactive(componentEnabled: false);
            var speech = _authoredGo.GetComponent<VoxrSpeechRecogniser>();
            _authoredGo.SetActive(true);

            speech.ListeningModeHint = VoxrListeningMode.PushToTalk;
            controller.enabled = true;

            Assert.AreEqual(
                VoxrListeningMode.Continuous,
                speech.ListeningModeHint,
                "An authored-Continuous enable must hand the mode over before its start"
            );
        }

        [Test]
        public void Hint_OnEnableResume_HandsOff()
        {
            _controller.PressTalk();
            _controller.enabled = false;

            _speech.ListeningModeHint = VoxrListeningMode.Continuous;
            _controller.enabled = true;

            Assert.AreEqual(
                VoxrListeningMode.PushToTalk,
                _speech.ListeningModeHint,
                "A resumed enable must hand the mode over before its start"
            );
        }

        [Test]
        public void Hint_PauseResume_HandsOff()
        {
            var counting = UseCountingRecogniser();
            _controller.PressTalk();
            _go.SendMessage("OnApplicationPause", true);

            counting.ListeningModeHint = VoxrListeningMode.Continuous;
            _go.SendMessage("OnApplicationPause", false);

            Assert.AreEqual(
                VoxrListeningMode.PushToTalk,
                counting.ListeningModeHint,
                "A pause resume must hand the mode over before its start"
            );
        }
    }
}
