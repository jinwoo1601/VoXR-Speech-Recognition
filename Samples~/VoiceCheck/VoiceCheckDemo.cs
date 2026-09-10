using System;
using UnityEngine;
using UnityEngine.UI;
using VoXR;
using VoXR.Commands;

public class VoiceCheckDemo : MonoBehaviour
{
    [SerializeField]
    VoxrSpeechRecogniser recogniser;

    [SerializeField]
    VoxrCommandRecogniser commandRecogniser;

    [Header("UI")]
    [SerializeField]
    RectTransform levelBarTrack;

    [SerializeField]
    Image levelBarFill;

    [SerializeField]
    Text promptText;

    [SerializeField]
    Text statusText;

    [SerializeField]
    Text audioCheckText;

    [SerializeField]
    Text commandCheckText;

    [Header("Tuning")]
    [Tooltip(
        "Input level that counts as 'audio is reaching the recogniser'. Linear RMS. "
            + "0.02 sits above a quiet room's noise floor but well under even a soft "
            + "utterance -- speech rarely exceeds 0.3 RMS, so 0.02 is a floor, not a target."
    )]
    [SerializeField]
    float audioSeenThreshold = 0.02f;

    [Tooltip(
        "Meter scaling: level * this, clamped to 1, is the fraction of the track filled. "
            + "3 maps 0.0-0.5 RMS to a full bar, matching the Editor debug window's meter."
    )]
    [SerializeField]
    float meterFullScale = 3f;

    const string CheckIntent = "voice_check";

    // There is no public API to read the active grammar back, so the sample's own
    // definitions ARE the active grammar -- the prompt is rendered from Patterns[0].
    static readonly string[][] Patterns =
    {
        new[] { "voice", "check" },
        new[] { "microphone", "check" },
        new[] { "testing", "one", "two" },
    };

    bool _modelReady;
    bool _audioSeen;
    bool _commandHeard;
    bool _errorLatched;
    string _lastStatus;
    float _lastFillWidth = -1f;

    void OnEnable()
    {
        if (recogniser != null)
        {
            recogniser.OnModelReady += HandleModelReady;
            recogniser.OnError += HandleError;
        }

        if (commandRecogniser != null)
        {
            commandRecogniser.OnCommandRecognised += HandleCommand;
            commandRecogniser.OnUnrecognisedSpeech += HandleUnrecognised;
        }
    }

    void OnDisable()
    {
        if (recogniser != null)
        {
            recogniser.OnModelReady -= HandleModelReady;
            recogniser.OnError -= HandleError;
        }

        if (commandRecogniser != null)
        {
            commandRecogniser.OnCommandRecognised -= HandleCommand;
            commandRecogniser.OnUnrecognisedSpeech -= HandleUnrecognised;
        }
    }

    void Start()
    {
        if (promptText != null)
            promptText.text = string.Join(" ", Patterns[0]);

        if (commandRecogniser != null)
        {
            commandRecogniser.Configure(
                Array.Empty<VoxrSlotDefinition>(),
                new[] { new VoxrCommandDefinition(CheckIntent, Patterns) }
            );
        }

        if (recogniser != null)
        {
            _modelReady = recogniser.IsModelReady;
            recogniser.StartRecognition();
        }

        Retry();
    }

    void Update()
    {
        float level = recogniser != null ? recogniser.InputLevel : 0f;

        if (!_audioSeen && level >= audioSeenThreshold)
        {
            _audioSeen = true;
            SetCheck(audioCheckText, "1. Audio reaching the recogniser", true);
            Debug.Log($"[VoiceCheckDemo] Audio check passed at level {level:F4}.");
        }

        UpdateMeter(level);
        RefreshStatus();
    }

    // Clears both latches so the checks can be run again. Wired to the Retry button.
    public void Retry()
    {
        _audioSeen = false;
        _commandHeard = false;
        _errorLatched = false;
        // Forget the last wording too, or a cleared error would leave its text on
        // screen when RefreshStatus picks the same status string it picked before.
        _lastStatus = null;
        SetCheck(audioCheckText, "1. Audio reaching the recogniser", false);
        SetCheck(commandCheckText, "2. Command recognised", false);
        RefreshStatus();
    }

    void UpdateMeter(float level)
    {
        if (levelBarFill == null || levelBarTrack == null)
            return;

        // Same scaling as VoxrDebugWindow.DrawLevelMeter: 0.0-0.5 RMS maps to a full
        // bar, because speech rarely exceeds 0.3. Keeping the two in step means a
        // reading here means the same thing as a reading in the Editor window.
        float fill = Mathf.Clamp01(level * meterFullScale);
        float width = levelBarTrack.rect.width * fill;
        if (Mathf.Approximately(width, _lastFillWidth))
            return;
        _lastFillWidth = width;

        var size = levelBarFill.rectTransform.sizeDelta;
        size.x = width;
        levelBarFill.rectTransform.sizeDelta = size;
    }

    void RefreshStatus()
    {
        if (_errorLatched)
            return;

        string status;
        if (_audioSeen && _commandHeard)
            status = "Voice check passed -- both checks green.";
        else if (!_modelReady)
            status = "Loading the speech model...";
        else if (!_audioSeen)
            status = "Listening -- say the phrase above.";
        else
            status = "Audio is arriving. Now say the phrase exactly as written.";

        // Every branch is a literal, so reference equality is enough and Update()
        // touches the Text only when the wording actually changes.
        if (ReferenceEquals(status, _lastStatus))
            return;
        _lastStatus = status;
        if (statusText != null)
            statusText.text = status;
    }

    static void SetCheck(Text label, string title, bool passed)
    {
        if (label == null)
            return;
        label.text = passed ? $"{title}: PASSED" : $"{title}: waiting";
        label.color = passed ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.7f, 0.7f, 0.7f);
    }

    void HandleModelReady()
    {
        _modelReady = true;
        Debug.Log("[VoiceCheckDemo] Model ready.");
    }

    void HandleCommand(VoxrCommand command)
    {
        if (command.Intent != CheckIntent || _commandHeard)
            return;

        _commandHeard = true;
        SetCheck(commandCheckText, "2. Command recognised", true);
        Debug.Log(
            $"[VoiceCheckDemo] Command check passed: \"{command.RawText}\" score={command.Score:F2}"
        );
    }

    // The raw transcript is the answer to "the bar moves but nothing fires".
    // It shows what VOSK actually heard, which is what separates a gain problem
    // from a wording one -- so the README sends the reader here.
    void HandleUnrecognised(string text)
    {
        Debug.Log($"[VoiceCheckDemo] Heard, but matched no command: \"{text}\"");
    }

    void HandleError(VoxrBridgeErrorCode code, string message)
    {
        Debug.LogError($"[VoiceCheckDemo] VOSK [{code}] {code.ToDescription()}: {message}");

        _errorLatched = true;
        _lastStatus = null;
        if (statusText != null)
            statusText.text = $"[{code}] {code.ToDescription()}: {message}";
    }
}
