// ============================================================================
// Purpose:  MonoBehaviour facade: speech-to-command pipeline with buffer, debounce, pending, grammar
// Layer:    Runtime.Commands
// Owns:     VoxrCommandRecogniser (public MonoBehaviour)
// Depends:  VoxrSpeechRecogniser, VoxrCommandParser, VoxrCommand, VoxrCommandDefinition, VoxrSlotDefinition, VoxrPendingCommand, VoxrPendingAmbiguity, VoxrMatchDiagnostics
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace VoXR.Commands
{
    [AddComponentMenu("VoXR/Command Recogniser")]
    public class VoxrCommandRecogniser : MonoBehaviour
    {
        [SerializeField] VoxrSpeechRecogniser speechRecogniser;

        [Tooltip("Bypasses grammar constraints so VOSK recognises freely (like pre-v2.0). " +
                 "Useful for on-device testing to see what VOSK actually hears before " +
                 "grammar constrains it. The parser still runs against the output so you " +
                 "can see what matches and what doesn't. Disable for release builds.")]
        [SerializeField] bool freeSpeechMode = false;

        [Tooltip("Reject commands where the minimum word confidence is below this threshold. " +
                 "Prevents phantom commands from background noise.")]
        [SerializeField] float minConfidence = 0.4f;

        [Tooltip("Reject matches where the pattern score is below this threshold. " +
                 "Prevents partial or garbled matches.")]
        [SerializeField] float minScore = 0.6f;

        [Tooltip(
            "How much each in-grammar word a match leaves unexplained counts against "
                + "the score — both the words the parser skipped to reach the match and the "
                + "ones left over after it. The parser slides its start point through the "
                + "utterance, so without this a short pattern found anywhere inside a longer "
                + "sentence scores a full 1.0 and fires, discarding the rest. At 1.0 the "
                + "score is close to the fraction of the utterance the pattern covers, so a "
                + "one-word command needs to be most of what was said. Unrecognised ([unk]) "
                + "filler is never charged, and a leftover word that could begin another "
                + "command or a confirm/cancel reply usually is not either, so chained "
                + "commands still work — the exception is a word the pattern itself tried "
                + "and failed to match, which is always charged. Set to 0 to disable — note "
                + "that this also restores the behaviour where a spoken argument can be "
                + "silently dropped in favour of a shorter pattern."
        )]
        // DR-4: carries the value across the rename for this field's OWN serialized data, so
        // a scene or asset that set the old name deserializes onto the new one. The field is
        // private, so there is no public property or constant needing a separate forwarder.
        //
        // Scope, deliberately narrow: [FormerlySerializedAs] governs field deserialization.
        // A prefab-INSTANCE override is stored as a literal propertyPath string in the
        // instance's m_Modifications and applied against the already-loaded prefab, and the
        // same is true of .preset assets and of any editor script calling FindProperty with
        // the old name. Whether Unity remaps those paths through this attribute is not
        // established here and no automated instrument in this package can settle it — it
        // needs a real prefab-override round-trip in a host project. Nothing shipped in this
        // package is affected: no prefab, no preset, no CustomEditor, and no sample scene
        // serializes this field at all.
        [FormerlySerializedAs("skippedWordPenalty")]
        [SerializeField]
        float coverageWeight = VoxrCommandParser.DefaultCoverageWeight;

        [Tooltip("Time in seconds to wait for additional speech before parsing. " +
                 "Longer values recover split commands but add latency. " +
                 "Default 0.5s matches typical PC latency; use 2.0s on Quest 3, " +
                 "where VOSK adds ~0.5–1.0s to inter-result gaps and the measured " +
                 "gap between results runs ~1.9–2.1s. Past ~2.5s unrelated " +
                 "utterances start merging. " +
                 "Set to 0 to disable buffering (v2.2 behaviour).")]
        [SerializeField] float bufferWindow = 0.5f;

        [Tooltip("When the buffered speech already forms a complete command that cannot be " +
                 "extended or completed by more words, flush and fire immediately instead " +
                 "of waiting out bufferWindow. Zero latency for unambiguous commands; " +
                 "commands that are a prefix of a longer one still wait the full window. " +
                 "Opt-in — off preserves the time-only buffering behaviour.")]
        [SerializeField] bool eagerFlushOnCompleteMatch = false;

        [Tooltip("How long a complete command that more speech could still extend — one " +
                 "that is a prefix of a longer command, or ends in a slot whose value could " +
                 "grow — waits for that continuation before firing. Only the continuation is " +
                 "being waited for, and a speaker who is continuing does so almost " +
                 "immediately, so this can be much shorter than bufferWindow (0.5–0.8s is " +
                 "usually enough; scale up with bufferWindow on Quest 3). Requires " +
                 "eagerFlushOnCompleteMatch. Never lengthens the wait: values above " +
                 "bufferWindow are ignored. Set to 0 to keep waiting the full bufferWindow.")]
        [SerializeField] float prefixHoldSeconds = 0f;

        [Tooltip("Minimum seconds between firing the same intent. " +
                 "Prevents duplicate commands from rapid VOSK results. " +
                 "Set to 0 to disable debounce.")]
        [SerializeField] float commandCooldown = 0.3f;

        [Header("Inspector Authoring (optional — ignored if Configure() is called from code)")]
        [SerializeField] VoxrSlotAsset[] slotAssets;
        [SerializeField] VoxrCommandSetAsset[] commandSetAssets;

        [Tooltip("Command sets to activate on startup when using Inspector authoring.")]
        [SerializeField] string[] initialActiveSetNames;

        [Header("Follow-Up / Pending Commands")]
        [Tooltip("Maximum seconds a pending command waits for follow-up speech before timing out.")]
        [SerializeField] float pendingTimeout = 5.0f;

        [Tooltip("What happens when a pending command times out.")]
        [SerializeField] VoxrPendingTimeoutBehavior pendingTimeoutBehavior = VoxrPendingTimeoutBehavior.Cancel;

        [Tooltip("Phrases that confirm a pending command. " +
                 "Leave empty to use defaults (confirm, affirmative, yes, go ahead, do it).")]
        [SerializeField] string[] confirmVocabulary;

        [Tooltip("Phrases that cancel a pending command. " +
                 "Leave empty to use defaults (cancel, abort, negative, belay that, never mind).")]
        [SerializeField] string[] cancelVocabulary;

        [Tooltip(
            "When the recogniser cannot tell two commands apart — they differ only by one "
                + "word and the recogniser dropped it — ask instead of guessing. The pending "
                + "command is raised through OnCommandPending with PendingAmbiguity set; the "
                + "speaker answers with the distinguishing word. Off by default: with no "
                + "OnCommandPending subscriber an ambiguous utterance would fire nothing at all."
        )]
        [SerializeField]
        bool disambiguateSiblingTies = false;

        public event Action<VoxrCommand> OnCommandRecognised;
        public event Action<VoxrCommand[]> OnCommandsRecognised;
        public event Action<string> OnUnrecognisedSpeech;

        public event Action<VoxrCommand> OnCommandPending;

        public event Action<VoxrCommand> OnCommandConfirmed;

        public event Action<VoxrCommand> OnCommandCancelled;

#if UNITY_EDITOR
        // Raised every time diagnostics are published, so editor-side collectors can capture
        // every utterance losslessly instead of polling LastMatchDiagnostics (which drops
        // entries whenever two utterances land between polls).
        internal static event Action<VoxrCommandRecogniser, VoxrMatchDiagnostics> DiagnosticsPublished;

        VoxrMatchDiagnostics _lastMatchDiagnostics;

        internal VoxrMatchDiagnostics LastMatchDiagnostics
        {
            get => _lastMatchDiagnostics;
            private set
            {
                _lastMatchDiagnostics = value;
                DiagnosticsPublished?.Invoke(this, value);
            }
        }

        internal string LastPartialResult { get; private set; }
#endif

        VoxrCommandParser _parser;
        readonly GrammarManager _grammar = new GrammarManager();

        // Utterance buffer
        readonly UtteranceBuffer _buffer = new UtteranceBuffer();

        // Set when the buffer holds a complete command that more speech could still extend,
        // so Update waits only prefixHoldSeconds for that continuation (issue #32).
        // Re-derived on every result and cleared whenever the buffer empties, so it never
        // outlives the buffer contents it was derived from.
        bool _eagerHoldArmed;

        // Per-intent debounce
        readonly CommandDebouncer _debouncer = new CommandDebouncer();

        // Command set and slot state
        VoxrSlotDefinition[] _slots;
        VoxrCommandDefinition[] _activeCommands;
        readonly CommandSetManager _setManager = new CommandSetManager();
        readonly DynamicSlotManager _slotManager = new DynamicSlotManager();

        // Pending command state
        readonly PendingCommandHandler _pending = new PendingCommandHandler();

        // Pre-allocated buffer for accepted commands (avoids per-utterance List allocation)
        VoxrCommand[] _acceptedBuf;

        // Slot-resolution state. All four are per-utterance scratch owned by the recogniser and
        // reused rather than reallocated: an utterance where nothing resolves — which is every
        // utterance in a game that registered no resolver, and most of them in one that did —
        // must not allocate.
        //
        // One answer per slot per utterance. Cleared at the top of Step 3b, which is also the
        // only thing that keeps a resolution from outliving the utterance it was given for.
        Dictionary<string, VoxrSlotResolution> _resolutionCache;

        // A single candidate's resolution attempt, in progress. Nothing is materialised into a
        // VoxrCommand until every unfilled required slot has resolved, so these hold the answers
        // that would be thrown away if the next slot comes back empty.
        List<VoxrSlotMatch> _resolveSlotScratch;
        List<VoxrResolvedSlot> _resolveRecordScratch;

        // The resolved form of each result-buffer entry. Null means no resolver is registered at
        // all, and the read sites fall back to the parser's own command — one branch and no copy,
        // which is what makes the unused feature cost nothing.
        VoxrCommand[] _effectiveBuf;

        public string[] ActiveSetNames => _setManager.ActiveSetNames;

        public bool HasPendingCommand => _pending.HasPending;

        public VoxrCommand? PendingCommand => _pending.PendingCommand;

        /// <summary>
        /// The ambiguity a pending command is waiting on, or <c>null</c> when there is no pending
        /// command or it is waiting on something else (a confirmation, or a missing argument).
        /// </summary>
        /// <remarks>
        /// <c>HasValue</c> is the reason signal. <c>OnCommandPending</c> carries only the command,
        /// so an integrator subscribed for <c>requiresConfirmation</c> would otherwise treat a
        /// "which did you mean?" as a "are you sure?" and prompt yes/no — which under a
        /// disambiguation does nothing, leaving the pending to time out and fire nothing.
        ///
        /// Only ever non-null with <c>disambiguateSiblingTies</c> enabled. Read it while the
        /// pending is live. The arrays are allocated once at entry and are safe to retain, but
        /// they are the live pending's own arrays rather than copies — do not write to them, as
        /// that would change which word resolves the question and what fires when it does.
        /// </remarks>
        public VoxrPendingAmbiguity? PendingAmbiguity
        {
            get
            {
                var pending = _pending.Current;
                if (
                    !pending.HasValue
                    || pending.Value.Reason != VoxrPendingReason.AwaitingDisambiguation
                    // Guards ChoiceValues, the same field TryHandleConfirmCancel checks, so the
                    // two readers of this record cannot disagree about what "present" means and
                    // hand out an ambiguity whose DiscriminatingValues is null.
                    || pending.Value.ChoiceValues == null
                )
                    return null;

                return new VoxrPendingAmbiguity(
                    pending.Value.Choices,
                    pending.Value.ChoiceValues,
                    pending.Value.ChoicesTruncated
                );
            }
        }

        public void Configure(VoxrSlotDefinition[] slots, VoxrCommandDefinition[] commands)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (commands == null) throw new ArgumentNullException(nameof(commands));

            InterpretResolution(_pending.Cancel());

            _debouncer.Clear();
            _slots = slots;
            _setManager.Reset();
            _activeCommands = commands;
            _setManager.BuildLookup(commands);
            EnsureAcceptedBuffer(commands.Length);

            // Same word list to both: the decoder can return follow-up vocabulary as real
            // tokens, so the parser's coverage rule has to know those words are legitimate
            // rather than charge them as unexplained (issue #65 §5.2).
            _parser = new VoxrCommandParser(_slotManager.BuildEffectiveSlots(_slots), commands,
                coverageWeight,
                GetFollowUpGrammarWords(),
                cancelVocabulary,
                disambiguateSiblingTies,
                minScore
            );
            _grammar.Rebuild(_slots, commands, GetFollowUpGrammarWords());

            if (!freeSpeechMode && speechRecogniser != null && speechRecogniser.IsModelReady)
            {
                speechRecogniser.SetGrammar(_grammar.CurrentJson);
                _grammar.IsApplied = true;
            }
        }

        public void Configure(VoxrSlotDefinition[] slots, VoxrCommandSet[] sets)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (sets == null) throw new ArgumentNullException(nameof(sets));

            InterpretResolution(_pending.Cancel());

            _debouncer.Clear();
            _slots = slots;
            _setManager.Configure(sets);

            _parser = null;
            _grammar.Reset();
            _activeCommands = null;
        }

        public void SetActiveSets(params string[] setNames)
        {
            if (!_setManager.HasSets)
                throw new InvalidOperationException(
                    "Configure(slots, sets) must be called before SetActiveSets().");

            InterpretResolution(_pending.Cancel());

            var commands = _setManager.Activate(setNames);
            _activeCommands = commands;
            EnsureAcceptedBuffer(commands.Length);
            _debouncer.Clear();
            RebuildParserAndGrammar();
        }

        public void SetActiveSet(string setName)
        {
            SetActiveSets(setName);
        }

        public void InjectText(string text, VoxrWord[] words = null)
        {
            Debug.Assert(System.Threading.Thread.CurrentThread.ManagedThreadId == 1,
                "InjectText must be called from the Unity main thread.");

            if (string.IsNullOrWhiteSpace(text))
                return;

            if (_parser == null)
            {
                Debug.LogWarning("[VoxrCommandRecogniser] InjectText called before parser is ready. " +
                    "Call Configure(slots, commands) or Configure(slots, sets) followed by SetActiveSets(...) first.");
                return;
            }

            HandleResult(new VoxrResult(text, words ?? Array.Empty<VoxrWord>()));
        }

        public void FlushPendingBuffer()
        {
            Debug.Assert(System.Threading.Thread.CurrentThread.ManagedThreadId == 1,
                "FlushPendingBuffer must be called from the Unity main thread.");

            if (_buffer.IsActive)
                FlushBuffer();
        }

        public void CancelPendingCommand() => InterpretResolution(_pending.Cancel());

        // -------- Dynamic slot value providers --------

        public void RegisterSlotValueProvider(string slotName, Func<string[]> valueProvider)
        {
            _slotManager.Register(slotName, valueProvider);
        }

        public bool UnregisterSlotValueProvider(string slotName)
        {
            return _slotManager.Unregister(slotName);
        }

        // -------- Dynamic slot resolvers --------

        /// <summary>
        /// Registers the delegate asked to fill slot <paramref name="slotName"/> when the speaker
        /// omitted it. A second registration for the same slot replaces the first silently.
        /// </summary>
        /// <remarks>
        /// Unlike a value provider this changes no grammar, so it needs neither a parser rebuild
        /// nor a <see cref="NotifySlotChanged"/> call -- it takes effect on the next utterance.
        /// The resolver is consulted only when a parsed command is otherwise complete and its only
        /// defect is one or more unfilled required slots, and it can never supply a pattern's
        /// first required element: a command whose leading required element went unspoken is
        /// refused before any command is built, so there is nothing to offer a resolver. The
        /// delegate runs synchronously on the main thread inside the recognition callback, so it
        /// must be cheap; see <see cref="VoxrSlotResolution"/> for the rest of the contract.
        /// </remarks>
        public void RegisterSlotResolver(string slotName, Func<VoxrSlotResolution> resolver)
        {
            _slotManager.RegisterResolver(slotName, resolver);
        }

        /// <summary>
        /// Removes the resolver for <paramref name="slotName"/> and returns whether one was
        /// removed. No parser rebuild or <see cref="NotifySlotChanged"/> call is needed; from the
        /// next utterance the slot resolves no longer.
        /// </summary>
        public bool UnregisterSlotResolver(string slotName)
        {
            return _slotManager.UnregisterResolver(slotName);
        }

        public void NotifySlotChanged()
        {
            if (_activeCommands == null)
                return;

            RebuildParser();
        }

        public void RebuildParser()
        {
            if (_activeCommands == null)
                throw new InvalidOperationException(
                    "Configure must be called before RebuildParser().");

            _parser = new VoxrCommandParser(_slotManager.BuildEffectiveSlots(_slots), _activeCommands,
                coverageWeight,
                GetFollowUpGrammarWords(),
                cancelVocabulary,
                disambiguateSiblingTies,
                minScore
            );
        }

        public void RebuildGrammar()
        {
            if (_activeCommands == null)
                throw new InvalidOperationException(
                    "Configure must be called before RebuildGrammar().");

            if (_pending.HasPending)
            {
                _grammar.GrammarRebuildDeferred = true;
                return;
            }

            RebuildGrammarInternal();
        }

        void RebuildGrammarInternal()
        {
            _buffer.Reset();
            _eagerHoldArmed = false;
            _grammar.Rebuild(_slots, _activeCommands, GetFollowUpGrammarWords());
            _grammar.ForceApply(speechRecogniser, freeSpeechMode);
        }

        void DrainDeferredGrammarRebuild()
        {
            if (!_grammar.GrammarRebuildDeferred)
                return;

            _grammar.GrammarRebuildDeferred = false;
            RebuildGrammarInternal();
        }

        // Test-only setters. Production callers configure via the Inspector.
        internal float BufferWindow { set => bufferWindow = value; }
        internal bool EagerFlushOnCompleteMatch { set => eagerFlushOnCompleteMatch = value; }
        internal float PrefixHoldSeconds { set => prefixHoldSeconds = value; }
        internal float CommandCooldown { set => commandCooldown = value; }
        internal VoxrSpeechRecogniser SpeechRecogniser
        {
            set
            {
                // Unsubscribe from the old recogniser if any.
                if (speechRecogniser != null)
                {
                    speechRecogniser.OnModelReady -= HandleModelReady;
                    speechRecogniser.OnResult -= HandleResult;
#if UNITY_EDITOR
                    speechRecogniser.OnPartialResult -= HandlePartialResult;
#endif
                }

                speechRecogniser = value;

                // Subscribe immediately when the component is already active
                // (Edit Mode tests may not re-trigger OnEnable after SetActive).
                if (value != null && isActiveAndEnabled)
                {
                    value.OnModelReady += HandleModelReady;
                    value.OnResult += HandleResult;
#if UNITY_EDITOR
                    value.OnPartialResult += HandlePartialResult;
#endif
                }
            }
        }

        void EnsureAcceptedBuffer(int commandCount)
        {
            if (_acceptedBuf == null || _acceptedBuf.Length < commandCount)
                _acceptedBuf = new VoxrCommand[Math.Max(commandCount, 1)];
        }

        void RebuildParserAndGrammar()
        {
            RebuildParser();
            RebuildGrammarInternal();
        }

        void Awake()
        {
            // If user code already called Configure(), _slots is non-null and inspector assets are ignored.
            if (_slots != null)
                return;

            // Slot assets are optional — an all-literal grammar declares no slots — so an absent
            // or empty array converts with zero of them. Command sets are what carry the
            // commands, so an empty array leaves nothing to convert; warn rather than return in
            // silence when slot assets were assigned, since silence there is indistinguishable
            // from a recogniser that never hears anything.
            if (commandSetAssets == null || commandSetAssets.Length == 0)
            {
                if (slotAssets != null && slotAssets.Length > 0)
                {
                    Debug.LogWarning(
                        "[VoxrCommandRecogniser] Slot assets are assigned but "
                            + "Command Set Assets is empty — skipping Inspector conversion, so no "
                            + "command will be recognised."
                    );
                }
                return;
            }

            int slotCount = slotAssets?.Length ?? 0;
            var slotList = new List<VoxrSlotDefinition>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                if (slotAssets[i] == null)
                {
                    Debug.LogWarning($"[VoxrCommandRecogniser] slotAssets[{i}] is null — skipping.");
                    continue;
                }
                slotList.Add(slotAssets[i].ToDefinition());
            }

            var setList = new List<VoxrCommandSet>(commandSetAssets.Length);
            for (int i = 0; i < commandSetAssets.Length; i++)
            {
                if (commandSetAssets[i] == null)
                {
                    Debug.LogWarning(
                        $"[VoxrCommandRecogniser] commandSetAssets[{i}] is null — skipping.");
                    continue;
                }
                setList.Add(commandSetAssets[i].ToSet());
            }

            Configure(slotList.ToArray(), setList.ToArray());

            if (initialActiveSetNames != null && initialActiveSetNames.Length > 0)
                SetActiveSets(initialActiveSetNames);
        }

        void OnEnable()
        {
            if (speechRecogniser == null)
                return;

            speechRecogniser.OnModelReady += HandleModelReady;
            speechRecogniser.OnResult += HandleResult;
#if UNITY_EDITOR
            speechRecogniser.OnPartialResult += HandlePartialResult;
#endif

            if (!Debug.isDebugBuild && freeSpeechMode)
            {
                Debug.LogWarning("[VoxrCommandRecogniser] Free-speech mode is active in a " +
                    "release build — grammar constraints are disabled.");
            }
        }

        void OnDisable()
        {
            if (speechRecogniser == null)
                return;

            speechRecogniser.OnModelReady -= HandleModelReady;
            speechRecogniser.OnResult -= HandleResult;
#if UNITY_EDITOR
            speechRecogniser.OnPartialResult -= HandlePartialResult;
#endif

            // Suppress deferred grammar rebuild — grammar will be re-evaluated on next enable/configure.
            // CancelPendingIfActive would drain the rebuild, which is unsafe during disable.
            _grammar.GrammarRebuildDeferred = false;
            InterpretResolution(_pending.Cancel());

            // Flush any pending buffer on disable
            if (_buffer.IsActive)
                FlushBuffer();
        }

        void Update()
        {
            if (_buffer.IsActive && _buffer.ShouldFlush(Time.time, EffectiveBufferWindow))
                FlushBuffer();

            if (_pending.HasPending &&
                Time.time - _pending.Current.Value.CreatedTime >= pendingTimeout)
            {
                var resolution = _pending.HandleTimeout(pendingTimeoutBehavior);
                InterpretResolution(resolution);
#if UNITY_EDITOR
                bool timedOutConfirmed = resolution.Outcome == PendingOutcome.Confirmed;
                string timeoutLabel = timedOutConfirmed
                    ? "timeout — fired as-is" : "timeout — cancelled";
                LastMatchDiagnostics = new VoxrMatchDiagnostics(
                    resolution.Command.RawText ?? "", Array.Empty<VoxrWord>(),
                    new[] { new VoxrMatchAttempt(
                        resolution.Command.Intent, null,
                        resolution.Command.Score, minScore,
                        resolution.Command.Confidence, minConfidence,
                        null, timedOutConfirmed ? null : timeoutLabel,
                        timedOutConfirmed) },
                    Time.frameCount);
#endif
            }
        }

        void HandleModelReady()
        {
            _grammar.ApplyIfReady(speechRecogniser, freeSpeechMode);
        }

        void HandleResult(VoxrResult result)
        {
            if (string.IsNullOrWhiteSpace(result.Text))
                return;

            if (_parser == null)
                return;

            if (bufferWindow <= 0f)
            {
                ProcessParsedResultsCore(result.Text, result.Words, null);
                return;
            }

            // Append to buffer and reset timer
            _buffer.Append(result.Text, result.Words, Time.time);

            // The verdict belongs to the buffer as it stands, so re-derive it from scratch
            // for every result rather than carrying the previous one forward.
            _eagerHoldArmed = false;

            // Eager flush: if the buffered speech already forms a complete command that
            // cannot be extended or completed by more words, flush now instead of waiting
            // out bufferWindow. If it can still be extended, arm the shorter prefix hold
            // instead. Skipped while a command is pending so confirm/follow-up stays on the
            // timer path.
            if (eagerFlushOnCompleteMatch && !_pending.HasPending)
            {
                var verdict = ProbeEagerCommit();
                if (verdict == EagerCommitVerdict.Commit)
                    FlushBuffer();
                else
                    _eagerHoldArmed = verdict == EagerCommitVerdict.HoldExtendable;
            }
        }

        // How long the buffer waits for more speech. A complete-but-extendable match is
        // only waiting on a continuation the speaker would begin almost immediately, so
        // prefixHoldSeconds may cut that wait short (issue #32) — never lengthen it.
        float EffectiveBufferWindow =>
            _eagerHoldArmed && prefixHoldSeconds > 0f && prefixHoldSeconds < bufferWindow
                ? prefixHoldSeconds
                : bufferWindow;

        void FlushBuffer()
        {
            _eagerHoldArmed = false;
            string text = _buffer.Flush();
            if (text.Length == 0)
            {
                _buffer.ClearWords();
                return;
            }

            var words = _buffer.GetWordsSpan();
            ProcessParsedResultsCore(text, words, _buffer.ConfidenceBuffer);
            _buffer.ClearWords();
        }

        // Speculative check used by HandleResult: peek (don't consume) the buffer and ask
        // the parser whether it already forms one complete, confident command, and whether
        // more speech could still extend it.
        EagerCommitVerdict ProbeEagerCommit()
        {
            string text = _buffer.PeekText();
            if (text.Length == 0)
                return EagerCommitVerdict.None;

            string[] tokens = text.Split(VoxrCommandParser.SplitSeparator,
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return EagerCommitVerdict.None;

            // The buffer's array is aligned to the token positions of PeekText(), which is
            // exactly the text just split here — and the peek does not consume it, so the
            // alignment still holds.
            return _parser.TryEagerCommit(
                tokens, _buffer.ConfidenceBuffer, minScore, minConfidence);
        }

        // wordConfidence is supplied by the buffered path, which builds it per result as the
        // results arrive (alignment is a per-result property — see UtteranceBuffer). null means
        // "no buffer segmented this utterance": build it here from the single result's own words.
        void ProcessParsedResultsCore(string text, ReadOnlySpan<VoxrWord> words,
            float[] wordConfidence)
        {
            // Split once — shared by pending handlers, diagnostics, and the parser.
            string[] tokens = text.Split(VoxrCommandParser.SplitSeparator,
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return;

            // Built only when unbuffered: the per-token confidence array is aligned to these
            // tokens, so it cannot be built before the split.
            wordConfidence ??= _parser.InstanceBuildWordConfidence(tokens, words);

#if UNITY_EDITOR
            // Editor diagnostics need VoxrWord[] — copy once for the editor path only.
            VoxrWord[] diagWords = words.ToArray();
#endif

            // ---- Step 1: Confirm/cancel check (before parsing) ----
            if (_pending.HasPending)
            {
                var ccResolution = _pending.TryHandleConfirmCancel(
                    tokens,
                    confirmVocabulary,
                    cancelVocabulary,
                    Time.time
                );
                if (ccResolution.Outcome != PendingOutcome.None)
                {
                    InterpretResolution(ccResolution);
#if UNITY_EDITOR
                    // Three-way since the choice arm landed. Answering a disambiguation whose
                    // chosen intent requires confirmation resolves as ReEnteredPending, and a
                    // two-way "confirmed or cancelled" label reported that as "cancelled via
                    // vocabulary" with accepted=false — in LastMatchDiagnostics, the debug window
                    // and the exported session log — while the runtime had in fact advanced to
                    // asking "are you sure?".
                    bool wasConfirmed = ccResolution.Outcome == PendingOutcome.Confirmed;
                    string ccReject;
                    switch (ccResolution.Outcome)
                    {
                        case PendingOutcome.Confirmed:
                            ccReject = null;
                            break;
                        case PendingOutcome.ReEnteredPending:
                            ccReject = "chosen via vocabulary, now awaiting confirmation";
                            break;
                        default:
                            ccReject = "cancelled via vocabulary";
                            break;
                    }
                    LastMatchDiagnostics = new VoxrMatchDiagnostics(
                        text, diagWords,
                        new[] { new VoxrMatchAttempt(
                            ccResolution.Command.Intent, null,
                            ccResolution.Command.Score, minScore,
                            ccResolution.Command.Confidence, minConfidence,
                                null,
                                ccReject,
                            wasConfirmed) },
                        Time.frameCount);
#endif
                    return;
                }
            }

            // ---- Step 2: Follow-up slot-fill attempt (before parsing) ----
            VoxrCommand? followUpResult = null;
            if (_pending.HasPending)
                followUpResult = _pending.TryFollowUpSlotFill(
                    text, tokens, wordConfidence, _parser);

            // ---- Step 3: Normal parse (internal path — no duplicate split/dict) ----
            int resultCount = _parser.ParseInternal(tokens, text, wordConfidence);

#if UNITY_EDITOR
            var parseDiag = _parser.LastParseDiagnostics;
            // Snapshotted here beside parseDiag, and for the same reason the parser itself is
            // snapshotted below: Step 7 raises public events between iterations and a subscriber
            // may answer one by re-parsing, which would replace this array mid-walk with another
            // utterance's barred rounds.
            var barredRounds =
                _parser.LastBarredRounds ?? Array.Empty<VoxrCommandParser.BarredRoundEntry>();
            // wordConfidence is already built above — reuse for diagnostics.
            float[] diagWordConf = wordConfidence;
#endif

            // ---- Step 4: Determine if any normal result passes standard thresholds ----
            //
            // The completeness term (issue #73) has to be here as well as in Step 7, and this
            // is the read that is easy to miss. This flag decides two things below: whether a
            // follow-up slot-fill is preempted by the new utterance (Step 5), and whether a
            // live pending command is cancelled outright (Step 6). A command missing a required
            // argument no longer fires in Step 7, so letting it set this flag would cancel the
            // user's pending command in favour of one that then goes nowhere — losing the
            // half-finished command to an utterance that produces nothing.
            bool hasCompleteNewCommand = false;
            // A complete, above-minScore result held back ONLY by the confidence gate. Step 7
            // drops those silently — "the user said a valid command, just not confidently" — so
            // the refusal branch below must not turn around and report the same utterance as
            // unrecognised (issue #113). Step 7's own `anyThresholdFiltered` cannot serve: the
            // refusal returns from Step 5 and never reaches it. The debounce and sibling-tie
            // arms need no counterpart — both sit behind the predicate that sets
            // hasCompleteNewCommand, which skips this whole arm.
            bool anyConfidenceFilteredNewCommand = false;
            // Snapshot the parser itself alongside its buffer. Both are read across iterations
            // of the Step 7 loop, which raises public events a subscriber may answer by calling
            // Configure (setting _parser to null) or SetActiveSets (installing a parser with
            // differently-sized buffers). One instance for the whole walk, or the two halves of
            // the ResultBuffer/TiedSiblingBuffer parallel-array contract can come from different
            // parsers mid-loop.
            var parser = _parser;
            var resultBuf = parser.ResultBuffer;

            // ---- Step 3b: Fill omitted required slots from the game's registered resolvers ----
            //
            // Resolution happens once, here, and every site that consults the completeness ruling
            // below — Step 4's flag, Step 5's follow-up split, Step 7's own gate — reads the
            // result rather than asking again. That is the whole of the uniformity guarantee:
            // those sites cannot disagree about whether a command is complete because there is
            // nothing left for them to disagree about.
            //
            // BEHIND the parser snapshot above, deliberately. This is the first and only game code
            // this method calls between the parse and the Step 7 loop, and a resolver is as free
            // to answer by calling Configure — which sets _parser to null — as any event
            // subscriber is. In front of the snapshot that is a live NullReferenceException on the
            // `parser.ResultBuffer` read; behind it, it inherits the protection the snapshot
            // exists to give and the caveat below covers it unchanged.
            //
            // The cache is cleared BEFORE the early-out, not after, and that ordering is
            // load-bearing: Step 5 is reachable with resultCount == 0 — a follow-up fill on an
            // utterance the parser produced nothing for — and it resolves through this same cache.
            // Clearing after the early-out would leave the previous utterance's answers live on
            // exactly that path, which is the cross-utterance memory this feature is defined by
            // not having. A resolution that can go stale can aim a weapon at a dead target.
            _resolutionCache?.Clear();
            // The effective buffer is cleared here rather than at the end of the utterance,
            // because there is no end that covers it: it is filled in this block, ahead of three
            // early returns — the follow-up branch, the no-results branch, and the acceptedCount
            // == 0 one, which is the ordinary outcome whenever every candidate went pending or was
            // rejected. A tail clear would reach one path in four and leave the staleness
            // path-dependent, which is harder to reason about than never clearing at all.
            // Whole buffer, not the first resultCount entries: a longer previous utterance leaves
            // a tail of commands this one never overwrites, and those are exactly the entries that
            // would otherwise keep a VoxrCommand and its slot arrays reachable indefinitely. Ahead
            // of the HasResolvers gate for the same reason the cache clear is, so unregistering
            // every resolver mid-session drops what the last one filled.
            if (_effectiveBuf != null)
                Array.Clear(_effectiveBuf, 0, _effectiveBuf.Length);
            VoxrCommand[] effective = null;
            if (_slotManager.HasResolvers && resultCount > 0)
            {
                if (_effectiveBuf == null || _effectiveBuf.Length < resultCount)
                    _effectiveBuf = new VoxrCommand[resultCount];
                effective = _effectiveBuf;

                for (int i = 0; i < resultCount; i++)
                {
                    var candidate = resultBuf[i].Command;
                    effective[i] = candidate;

                    // Gated on minScore for correctness, not economy. A below-minScore incomplete
                    // command on a definition with AllowPartialMatch enters pending today;
                    // resolving it empties ComputeUnfilledSlots, the pending branch is skipped,
                    // and the candidate falls through to the reject instead. Resolution is allowed
                    // to change the completeness answer and nothing else — never which side of
                    // another gate a command lands on.
                    if (candidate.Score < minScore)
                        continue;

                    if (
                        _setManager.TryLookupCommand(candidate.Intent, out var candidateDef)
                        && TryResolveMissingSlots(candidate, candidateDef, out var resolvedCandidate)
                    )
                    {
                        effective[i] = resolvedCandidate;
                    }
                }
            }

            // A resolver may have cancelled the pending this fill was built against — CancelPendingCommand
            // and Configure both clear it, and Step 3b above is the first game code this method has ever run
            // between Step 2's fill and Step 5's use of it. The fill merged INTO that pending, so without it
            // there is nothing for Step 5 to complete or re-arm, and every read in that branch assumes it is
            // still there: both the resolution gate's definition read and the shipped Complete(...) one.
            // Discarded at the premise rather than guarded at each read, so the two cannot disagree.
            if (followUpResult.HasValue && !_pending.HasPending)
                followUpResult = null;

            // Snapshotting the parser does not make this loop re-entrant. These are the
            // parser's pooled arrays, not copies, so a subscriber that answers one of the events
            // below by calling InjectText synchronously re-enters ParseInternal on this same
            // instance and overwrites them mid-walk. That is pre-existing and unsupported;
            // handlers should queue rather than inject.
            for (int i = 0; i < resultCount; i++)
            {
                // The resolved form when a resolver filled this candidate's missing slots, the
                // parser's own command otherwise. Step 7 reads it the same way, from the same
                // array, so the two completeness answers are the same answer.
                var cmd = effective != null ? effective[i] : resultBuf[i].Command;
                if (cmd.Score >= minScore && !IsIncomplete(cmd))
                {
                    if (cmd.Confidence < 0f || cmd.Confidence >= minConfidence)
                    {
                        hasCompleteNewCommand = true;
                        break;
                    }

                    anyConfidenceFilteredNewCommand = true;
                }
            }

            // ---- Step 5: Arbitrate follow-up vs new command ----
            if (followUpResult.HasValue && !hasCompleteNewCommand)
            {
                // A follow-up result is not necessarily a complete command (issue #77). The
                // slot-fill walks the unfilled slots in order, stops at the first one it cannot
                // fill, and returns as soon as ONE new slot is filled — so a pending with two or
                // more unfilled required slots yields a command still missing an argument. Firing
                // it here would fire exactly the shape #73 refuses on the flush path, on the very
                // path #73 routes those commands to, and the re-score does not stand in for the
                // test: ScoreFollowUp re-scores against the matched pattern, so a partly filled
                // command can score alongside a complete one.
                //
                // Keeping the pending alive rather than discarding the fill is what makes this a
                // refusal to fire rather than a refusal to progress: each utterance fills what it
                // can and the command waits for the rest.
                var followUp = followUpResult.Value;
                bool followUpIncomplete = IsIncomplete(followUp);

                // The same resolution Step 3b offers a fresh parse, on the one site that would
                // otherwise disagree with it. Without it the SAME utterance completes or re-arms
                // depending only on whether a pending happened to be live, and nothing in the
                // suite, the session log or the debug window would show the split — the first
                // report arrives as "sometimes it fills the target, sometimes it asks".
                //
                // `Score > 0f` is not tidiness. This path has no minScore gate by design (#77,
                // #113); its fire-floor is the `Score <= 0` refusal below, and that refusal sits
                // BELOW the completeness split precisely so a partial fill re-arms instead of
                // stalling. Resolving a non-positive fill moves it across that split and turns
                // today's "keep the progress and ask again" into "refuse and report
                // unrecognised" — the exact stall the placement exists to prevent. The gate here
                // is exactly complementary to the floor below.
                //
                // Resolved against the PENDING's definition, because that is the one Complete
                // fires under. Taken only if the definition IsIncomplete reads agrees the result
                // is now complete: the comment below documents one intent resolving to two
                // different definitions as reachable, and a resolution satisfying the firing one
                // while the other still charges the command for unfilled required slots would
                // fire a command one of them calls incomplete. All-or-nothing, extended from
                // slots to definitions.
                if (
                    followUpIncomplete
                    && followUp.Score > 0f
                    && TryResolveMissingSlots(
                        followUp,
                        _pending.Current.Value.Definition,
                        out var followUpResolved
                    )
                    && !IsIncomplete(followUpResolved)
                )
                {
                    followUp = followUpResolved;
                    followUpIncomplete = false;
                }

                // The `Score <= 0` floor both flush paths carry (CompareCandidate's first test
                // and ParseInternal's bestScore check), on the one fire path that never had it
                // (issue #113). scoring.md §1 states the rule without qualification — a
                // candidate scoring zero or less is discarded and never competes — and a merged
                // command reaches a subscriber without ever passing either of those tests, so
                // the rule has to be restated here or it is not the rule.
                //
                // Reachable because ScoreFollowUp and IsIncomplete resolve an intent to
                // different definitions when two are registered under one intent: the former
                // scans the parser's command array and breaks on the FIRST match, the latter
                // reads CommandSetManager's dictionary, which BuildLookup fills last-write-wins.
                // The short definition then calls the command complete while the long one
                // charges it for required slots the matched pattern never had. Floored rather
                // than reconciled deliberately: the floor holds whatever the two disagree
                // about, and a non-positive score is not fireable for any reason.
                //
                // BELOW the completeness split, and that placement is the whole of it. Above it
                // this refused partial fills too, which is not a floor but a stall: #77's
                // re-arm is how a multi-slot exchange advances, and discarding the fill left
                // every later answer to re-derive the same non-positive score and be refused
                // again, so the command could never be completed by any speech at all. What
                // this refuses is strictly a command about to FIRE; an incomplete fill goes on
                // to AdvanceSlotFill, which keeps the progress and floors the stored score
                // itself.
                //
                // Refusing rather than re-arming, here where the command IS complete by slots:
                // AdvanceSlotFill would install a pending with nothing left to fill — one
                // TryFollowUpSlotFill declines forever and FireAsIs would eventually fire
                // carrying this same score. Leaving the pending untouched keeps the command it
                // would fire the one that legitimately scored on the first utterance. The
                // refusal neither resolves nor advances the pending, so it stays subject to the
                // ordinary endings — confirm, cancel, preemption, CancelPendingCommand(),
                // replacement, timeout. What it can no longer do is progress by further
                // follow-up speech, since the same fill re-scores non-positive every time.
                if (!followUpIncomplete && followUp.Score <= 0f)
                {
#if UNITY_EDITOR
                    LastMatchDiagnostics = new VoxrMatchDiagnostics(
                        text, diagWords,
                        new[] { new VoxrMatchAttempt(
                            followUp.Intent, null,
                            followUp.Score, minScore,
                            followUp.Confidence, minConfidence,
                            null,
                            FormattableString.Invariant(
                                $"follow-up re-score {followUp.Score:F2} <= 0"
                            ),
                            false) },
                        Time.frameCount);
#endif
                    // Suppressed for a result the ordinary path would have swallowed, so the
                    // two paths agree on when the integrator is told the speech was not
                    // understood.
                    if (!anyConfidenceFilteredNewCommand)
                        OnUnrecognisedSpeech?.Invoke(text);
                    return;
                }

                var followUpRes = followUpIncomplete
                    ? _pending.AdvanceSlotFill(followUp, Time.time)
                    // The pending's own definition: this path fills a slot on the command that
                    // is already pending, so the winner IS the resolved command. Only the
                    // disambiguation path resolves to a different one.
                    : _pending.Complete(
                        followUp,
                        _pending.Current.Value.Definition,
                        Time.time
                    );
#if UNITY_EDITOR
                // Read the re-armed pending BEFORE the resolution is interpreted. Interpreting it
                // invokes OnCommandPending, whose subscribers may cancel, reconfigure, or disable
                // the recogniser — any of which clears the pending and would make this read throw
                // on a Nullable with no value. The rest of the method's diagnostics capture their
                // locals ahead of the events for the same reason.
                string followUpReason = followUpIncomplete
                    ? "still pending (partial: unfilled "
                        + $"[{string.Join(", ", _pending.Current.Value.UnfilledSlots)}])"
                    : null;
#endif
                InterpretResolution(followUpRes);
#if UNITY_EDITOR
                LastMatchDiagnostics = new VoxrMatchDiagnostics(
                    text, diagWords,
                    new[] { new VoxrMatchAttempt(
                        followUp.Intent, null, followUp.Score,
                        minScore, followUp.Confidence, minConfidence,
                            null,
                            followUpReason,
                            !followUpIncomplete
                        ),
                    },
                    Time.frameCount);
#endif
                return;
            }

            // If new complete command preempts a pending, cancel the pending
            if (_pending.HasPending && hasCompleteNewCommand)
                InterpretResolution(_pending.Cancel());

            // ---- Step 6: No parse results ----
            if (resultCount == 0)
            {
#if UNITY_EDITOR
                // An utterance whose every round was barred produced no result, but it is not
                // the same thing as nothing having matched — and the synthetic "no match" below
                // says exactly that, hiding the one shape the bar exists to catch. Publish what
                // was refused instead. Only the diagnostic changes: OnUnrecognisedSpeech still
                // fires for a fully-barred utterance, which is what issue #124 settled.
                VoxrMatchAttempt[] noResultAttempts;
                if (barredRounds.Length > 0)
                {
                    noResultAttempts = new VoxrMatchAttempt[barredRounds.Length];
                    for (int b = 0; b < barredRounds.Length; b++)
                    {
                        noResultAttempts[b] = BuildBarredAttempt(
                            barredRounds[b],
                            tokens,
                            diagWordConf
                        );
                    }
                }
                else
                {
                    noResultAttempts = new[]
                    {
                        new VoxrMatchAttempt(
                            null,
                            null,
                            0f,
                            minScore,
                            0f,
                            minConfidence,
                            null,
                            "no match",
                            false
                        ),
                    };
                }

                LastMatchDiagnostics = new VoxrMatchDiagnostics(
                    text,
                    diagWords,
                    noResultAttempts,
                    Time.frameCount
                );
#endif
                OnUnrecognisedSpeech?.Invoke(text);
                return;
            }

            // ---- Step 7: Process results with pending-aware logic ----
            float now = Time.time;
            int acceptedCount = 0;
            bool anyThresholdFiltered = false;
#if UNITY_EDITOR
            var attempts = new List<VoxrMatchAttempt>(resultCount);
#endif

            for (int i = 0; i < resultCount; i++)
            {
#if UNITY_EDITOR
                // Barred rounds carry the number of results emitted before them, so draining
                // them here puts every attempt in the order its round actually ran rather than
                // appending them all at the end. Several can share an index: consecutive barred
                // rounds all fire before the next result is emitted.
                for (int b = 0; b < barredRounds.Length; b++)
                {
                    if (barredRounds[b].ResultsBefore == i)
                        attempts.Add(BuildBarredAttempt(barredRounds[b], tokens, diagWordConf));
                }
#endif

                // Step 3b's result, as Step 4 read it. Same array, same index, same answer.
                var cmd = effective != null ? effective[i] : resultBuf[i].Command;

                // Below score threshold, OR missing a required argument — either way this is
                // not a command to fire. Check AllowPartialMatch before rejecting.
                //
                // The completeness half is issue #73 and is deliberately independent of
                // minScore: a missing argument is a missing argument at any score. Until now
                // only the arithmetic held these down, and only by coincidence — a five-element
                // pattern with one missed required slot lands on exactly 0.60 and cleared the
                // default gate, firing a command whose argument the handler never receives.
                // TryEagerCommit has refused this shape since #66; this is the same rule on the
                // path that is actually on by default.
                //
                // Routing rather than refusing outright is what makes the two halves one branch:
                // a command opted into AllowPartialMatch now reaches the pending/slot-fill path
                // it was always meant to reach, instead of being fired incomplete for the sole
                // reason that it scored well. With the flag off (the default) it falls through
                // to the reject below, and the utterance is reported unrecognised.
                bool incomplete = IsIncomplete(cmd);
                if (cmd.Score < minScore || incomplete)
                {
                    if (cmd.Score > 0f &&
                        _setManager.TryLookupCommand(cmd.Intent, out var partialDef) &&
                        partialDef.AllowPartialMatch)
                    {
                        var unfilled = _pending.ComputeUnfilledSlots(cmd, partialDef);
                        if (unfilled.Length > 0)
                        {
                            // Handled, not rejected (issue #133) — same reason the
                            // disambiguation branch below sets it. Without this the utterance
                            // reaches acceptedCount == 0 with the flag clear and raises
                            // OnUnrecognisedSpeech in the same frame OnCommandPending asked the
                            // integrator to prompt for the missing slot. Inside the
                            // unfilled.Length check rather than above it: a candidate that
                            // opens no pending falls through to the reject below and must
                            // still be reported.
                            anyThresholdFiltered = true;

                            var enterRes = _pending.EnterPending(cmd, partialDef, unfilled,
                                VoxrPendingReason.PartialMatch, Time.time,
                                out var cancelRes);
                            InterpretResolution(cancelRes);
                            InterpretResolution(enterRes);
#if UNITY_EDITOR
                            attempts.Add(BuildAttempt(cmd, parseDiag, i, tokens, diagWordConf,
                                $"entered pending (partial: unfilled [{string.Join(", ", unfilled)}])",
                                false));
#endif
                            continue;
                        }
                    }

#if UNITY_EDITOR
                    // Report the condition that actually rejected it. An incomplete command can
                    // sit well above minScore, so reusing the score wording here would print a
                    // comparison that is plainly false in the session log and the debug window.
                    attempts.Add(BuildAttempt(cmd, parseDiag, i, tokens, diagWordConf,
                            cmd.Score < minScore
                                ? FormattableString.Invariant(
                                    $"score {cmd.Score:F2} < minScore {minScore:F2}"
                                )
                                : "required slot unfilled",
                            false
                        )
                    );
#endif
                    continue;
                }

                // Reject if below confidence threshold (skip when word data unavailable, i.e. -1)
                if (cmd.Confidence >= 0f && cmd.Confidence < minConfidence)
                {
                    anyThresholdFiltered = true;
#if UNITY_EDITOR
                    attempts.Add(BuildAttempt(cmd, parseDiag, i, tokens, diagWordConf,
                        FormattableString.Invariant(
                            $"confidence {cmd.Confidence:F2} < minConfidence {minConfidence:F2}"
                        ), false));
#endif
                    continue;
                }

                // Per-intent debounce
                if (commandCooldown > 0f &&
                    _debouncer.IsOnCooldown(cmd.Intent, now, commandCooldown))
                {
                    anyThresholdFiltered = true;
#if UNITY_EDITOR
                    attempts.Add(BuildAttempt(cmd, parseDiag, i, tokens, diagWordConf,
                        FormattableString.Invariant($"debounced ({commandCooldown:F1}s cooldown)"),
                        false));
#endif
                    continue;
                }

                // Sibling tie — ask which intent was meant instead of firing the first-registered
                // one (issue #74 item 3). AFTER the debounce check, because a command on cooldown
                // should not raise a question the speaker then answers into a cooldown; BEFORE
                // the confirmation check, because "which?" precedes "are you sure?" and Complete
                // sequences the two for free once the choice resolves.
                if (
                    disambiguateSiblingTies
                    && TryBuildAmbiguity(
                        parser,
                        i,
                        cmd,
                        tokens,
                        wordConfidence,
                        out var choices,
                        out var choiceValues,
                        out var choiceDefs,
                        out bool choicesTruncated
                    )
                )
                {
                    // Without this the utterance reaches acceptedCount == 0 with the flag clear
                    // and raises OnUnrecognisedSpeech — telling the integrator the speech was not
                    // understood in the same frame it was asked to prompt about it.
                    anyThresholdFiltered = true;

                    var enterAmbRes = _pending.EnterPending(
                        cmd,
                        choiceDefs[0],
                        Array.Empty<string>(),
                        VoxrPendingReason.AwaitingDisambiguation,
                        Time.time,
                        out var cancelAmbRes,
                        choices,
                        choiceValues,
                        choiceDefs,
                        choicesTruncated
                    );
                    InterpretResolution(cancelAmbRes);
                    InterpretResolution(enterAmbRes);
#if UNITY_EDITOR
                    attempts.Add(
                        BuildAttempt(
                            cmd,
                            parseDiag,
                            i,
                            tokens,
                            diagWordConf,
                            $"entered pending (awaiting disambiguation, {choices.Length} choices)",
                            false
                        )
                    );
#endif
                    continue;
                }

                // Check RequiresConfirmation — enter pending instead of firing
                if (_setManager.TryLookupCommand(cmd.Intent, out var confirmDef) &&
                    confirmDef.RequiresConfirmation)
                {
                    // Handled, not rejected (issue #133) — see the partial-match branch above.
                    anyThresholdFiltered = true;

                    var enterConfRes = _pending.EnterPending(cmd, confirmDef,
                        Array.Empty<string>(), VoxrPendingReason.AwaitingConfirmation, Time.time,
                        out var cancelConfRes);
                    InterpretResolution(cancelConfRes);
                    InterpretResolution(enterConfRes);
#if UNITY_EDITOR
                    attempts.Add(BuildAttempt(cmd, parseDiag, i, tokens, diagWordConf,
                        "entered pending (awaiting confirmation)", false));
#endif
                    continue;
                }

                _debouncer.RecordFire(cmd.Intent, now);
                _acceptedBuf[acceptedCount++] = cmd;
#if UNITY_EDITOR
                attempts.Add(BuildAttempt(cmd, parseDiag, i, tokens, diagWordConf, null, true));
#endif
            }

#if UNITY_EDITOR
            // The rounds barred after the last emitting one. The loop above cannot reach them:
            // it stops at resultCount, and theirs is exactly resultCount. Tested with >= rather
            // than ==, which is equivalent today — the parser stops the round loop when the
            // result buffer fills, so ResultsBefore can never exceed the count it returns — but
            // == would turn any future drift there into attempts silently vanishing from the
            // log, which is the failure mode issue #144 exists to close.
            for (int b = 0; b < barredRounds.Length; b++)
            {
                if (barredRounds[b].ResultsBefore >= resultCount)
                    attempts.Add(BuildBarredAttempt(barredRounds[b], tokens, diagWordConf));
            }

            LastMatchDiagnostics = new VoxrMatchDiagnostics(
                text, diagWords, attempts.ToArray(), Time.frameCount);
#endif

            if (acceptedCount == 0)
            {
                // Only fire OnUnrecognisedSpeech when the speech genuinely didn't match
                // any command. Threshold-filtered results (confidence, debounce) are
                // silently dropped — the user said a valid command, just not confidently
                // or too soon after the last one.
                if (!anyThresholdFiltered)
                    OnUnrecognisedSpeech?.Invoke(text);
                return;
            }

            // Fire per-command events in order
            for (int i = 0; i < acceptedCount; i++)
                OnCommandRecognised?.Invoke(_acceptedBuf[i]);

            // Fire batch event
            if (OnCommandsRecognised != null)
            {
                var batch = new VoxrCommand[acceptedCount];
                Array.Copy(_acceptedBuf, batch, acceptedCount);
                OnCommandsRecognised.Invoke(batch);
            }

            // Clear stale references
            Array.Clear(_acceptedBuf, 0, acceptedCount);
        }

        // Builds the choice list for result i, or answers false and asks nothing.
        //
        // `parser` is passed in rather than read from the field, and that is load-bearing: the
        // Step 7 loop raises public events between iterations, and a subscriber is allowed to
        // call Configure — which sets _parser to null — or SetActiveSets, which installs a new
        // parser whose buffers are sized to the new command count. The loop already snapshots
        // ResultBuffer for exactly this reason; reading _parser live here reintroduced the
        // hazard that snapshot exists to remove.
        //
        // Allocates — three small arrays and one VoxrCommand per alternative — and that is
        // deliberate: this runs once per AMBIGUITY, never per candidate, and everything it
        // produces crosses into a public event where a subscriber can retain it. The parse path
        // itself stays allocation-free (the parser records rivals into preallocated buffers);
        // this is the boundary where that stops being true, following the same rule
        // PendingCommandHandler already applies to anything reaching a subscriber.
        //
        // `winner` is passed in rather than re-read from parser.ResultBuffer[i], and that is
        // load-bearing too, for a different reason: a resolver may have filled a required slot the
        // speaker omitted, and the pooled buffer still holds the command as the parser produced
        // it. PendingCommandHandler fires the CHOICE the speaker picked, not the pending's own
        // command, so a re-read here would make choices[0] the unresolved form and answering the
        // question would fire a command missing its argument — the shape issue #73 refuses, and a
        // direct contradiction of the comment there recording that the winner was proved complete.
        bool TryBuildAmbiguity(
            VoxrCommandParser parser,
            int i,
            VoxrCommand winner,
            string[] tokens,
            float[] wordConfidence,
            out VoxrCommand[] choices,
            out string[] choiceValues,
            out VoxrCommandDefinition[] choiceDefinitions,
            out bool truncated
        )
        {
            choices = null;
            choiceValues = null;
            choiceDefinitions = null;

            var record = parser.TiedSiblingBuffer[i];
            truncated = record.Truncated;
            if (record.RivalCount == 0)
                return false;

            // The intent is what the lookup needs, and resolution does not change it, so the
            // caller's command and the buffer's would answer this identically.
            if (!_setManager.TryLookupCommand(winner.Intent, out var winnerDef))
                return false;

            // Index 0 is always the candidate that would have fired with the flag off, so the
            // order an integrator renders is the order registration would have produced.
            // Locals, not pooled fields. This runs once per ambiguity and allocates three
            // arrays at the end regardless, so pooling bought nothing measurable while keeping
            // the winner's and rivals' commands alive after the question resolved — the exact
            // staleness the Step 7 tail clears out of _acceptedBuf.
            var choiceBuf = new List<VoxrCommand>(1 + record.RivalCount);
            var valueBuf = new List<string>(1 + record.RivalCount);
            var defBuf = new List<VoxrCommandDefinition>(1 + record.RivalCount);
            choiceBuf.Add(winner);
            valueBuf.Add(record.WinnerValue);
            defBuf.Add(winnerDef);

            for (int n = 0; n < record.RivalCount; n++)
            {
                // A chosen alternative is NOT re-tested against the debounce, and that is a
                // decision rather than an omission. A review found that the Step 7 cooldown
                // check tests only the winner, and gating each rival here was tried and
                // reverted: on a two-way set it drops the only rival, the choice list falls
                // below two, and the winner fires — silently degrading to the coin flip this
                // feature exists to remove, with the truncation signal discarded on the way out.
                //
                // It also could not have been doing its job. An answer always waits out
                // bufferWindow (the eager path is skipped while a pending is live), so the
                // earliest a choice can fire is now + bufferWindow, while exclusion requires
                // now - lastFire < commandCooldown. At the shipped 0.5s and 0.3s the cooldown
                // has always expired before the answer could fire. And the bias runs backwards:
                // the intent on cooldown is the one the speaker just used.
                //
                // The pre-existing confirmation path already settles the principle — it enters
                // pending after the debounce check and fires on confirm without re-checking. A
                // deliberate answer to a question the recogniser asked is not the duplicate
                // VOSK result CommandDebouncer exists to suppress.
                //
                // Confidence needs no test either: a rival's span differs from the winner's only
                // by trailing [unk], which ComputeConfidence skips, so their confidences are
                // equal by construction and the winner already cleared the floor.
                //
                // Unreachable today, and kept anyway — the same treatment AdvanceSlotFill's
                // array carry gets. Every construction site builds the parser and the set
                // manager's lookup from ONE command array (Configure passes the same array to
                // both; SetActiveSets takes what Activate returns, and Activate calls
                // BuildLookup on exactly that), so an intent the parser can report is always
                // resolvable. A test asserting this branch was written and then removed: it
                // registered the rival in an inactive set, which does not reach the shape —
                // the parser is rebuilt from the ACTIVE commands, so it stops seeing the tie
                // at all. The guard is what keeps that argument true if either end changes.
                if (
                    !_setManager.TryLookupCommand(parser.SiblingRivalIntent(i, n), out var rivalDef)
                )
                {
                    // Nameable in a prompt but not fireable, so not offered — and reported,
                    // because an answer the speaker could have given is going unoffered.
                    truncated = true;
                    continue;
                }

                var rival = parser.BuildSiblingRivalCommand(i, n, tokens, wordConfidence);
                // Offered the same all-or-nothing resolution the winner got, against its own
                // definition. A rival is a different pattern that can miss the same slot, and
                // since the handler fires the chosen CHOICE, an unresolved rival offered beside a
                // resolved winner is a command that fires with its required slot still unfilled
                // the moment the speaker picks it. The cache is already warm, so this costs a
                // dictionary hit; a rival that does not resolve is offered exactly as before.
                if (TryResolveMissingSlots(rival, rivalDef, out var resolvedRival))
                    rival = resolvedRival;

                choiceBuf.Add(rival);
                valueBuf.Add(parser.TiedSiblingRivalAt(i, n).Value);
                defBuf.Add(rivalDef);
            }

            // Fewer than two survivors is not a question. Fall through and fire the winner, as
            // the flag-off path would.
            if (choiceBuf.Count < 2)
                return false;

            choices = choiceBuf.ToArray();
            choiceValues = valueBuf.ToArray();
            choiceDefinitions = defBuf.ToArray();
            return true;
        }

        // Whether a command is missing one of its own required arguments (issue #73). The flush
        // path's completeness condition, and the counterpart to the two COMPLETENESS conditions
        // TryEagerCommit enforces (#66, #70) — that gate also refuses on an ambiguous sibling
        // tie (#74), which is not a completeness question and has no flush-side counterpart
        // here. Issue #77 added the third caller: the follow-up slot-fill exit,
        // whose input is not a parse result at all but a pending command merged with a fill.
        //
        // An intent with no definition in the active sets is treated as complete: we cannot read
        // a pattern we do not have, and inventing a refusal there would silence commands for a
        // reason unrelated to their arguments. In practice the lookup only fails if the active
        // set changed between the parse and this loop.
        bool IsIncomplete(VoxrCommand cmd)
        {
            return _setManager.TryLookupCommand(cmd.Intent, out var def)
                && VoxrCommandParser.HasUnfilledRequiredSlot(cmd, def);
        }

        // -------- Slot resolution --------

        // The game is asked about a slot at most once per utterance, however many candidates that
        // utterance produced and however many of them miss the same slot. Memoising is safe
        // because slot names are global: the parser's slot table is built once from the flat
        // VoxrSlotDefinition registry, so `{track}` in any pattern of any command binds to one
        // entry, and two candidates missing `track` are missing the SAME slot.
        //
        // A resolver's exception propagates uncaught, which is the treatment DynamicSlotManager
        // already gives a value provider. Catching it would turn a bug in the game into a
        // resolution that merely returned nothing, and the command would quietly go pending — a
        // working-looking feature with the real failure hidden behind it.
        VoxrSlotResolution ResolveOnce(string slotName)
        {
            if (_resolutionCache == null)
                _resolutionCache = new Dictionary<string, VoxrSlotResolution>(StringComparer.Ordinal);
            else if (_resolutionCache.TryGetValue(slotName, out var cached))
                return cached;

            VoxrSlotResolution resolution;
            if (_slotManager.TryGetResolver(slotName, out var resolver))
                resolution = resolver();
            else
                resolution = VoxrSlotResolution.None;

            _resolutionCache[slotName] = resolution;
            return resolution;
        }

        // Offers every unfilled REQUIRED slot of the command's matched pattern to its registered
        // resolver, and answers with the filled command only if every one of them resolved.
        //
        // The walk mirrors HasUnfilledRequiredSlot element for element, through the same two
        // helpers and the same three guards, so the set of slots a resolver is offered is exactly
        // the set that makes IsIncomplete say true. Any divergence there is a command that
        // resolves and still does not fire, or fires still missing an argument.
        //
        // The definition is a parameter rather than a lookup, and that is what the follow-up path
        // needs: it must resolve against the definition the command will actually fire under,
        // which is not always the one IsIncomplete reads.
        //
        // All-or-nothing. The first required slot that does not resolve ends the attempt with
        // nothing materialised and nothing allocated, and the caller keeps exactly the command
        // the parser produced.
        bool TryResolveMissingSlots(
            in VoxrCommand cmd,
            VoxrCommandDefinition def,
            out VoxrCommand resolved
        )
        {
            resolved = default;

            if (!_slotManager.HasResolvers)
                return false;

            // All three of HasUnfilledRequiredSlot's guards, not two. MatchedPatternIndex == -1 is
            // the public constructor's default and reaches here through the pending machinery, and
            // without the Patterns test the length comparison dereferences the null array a failed
            // lookup's default(VoxrCommandDefinition) carries.
            if (
                def.Patterns == null
                || cmd.MatchedPatternIndex < 0
                || cmd.MatchedPatternIndex >= def.Patterns.Length
            )
                return false;

            if (_resolveSlotScratch == null)
            {
                _resolveSlotScratch = new List<VoxrSlotMatch>();
                _resolveRecordScratch = new List<VoxrResolvedSlot>();
            }
            _resolveSlotScratch.Clear();
            _resolveRecordScratch.Clear();

            var pattern = def.Patterns[cmd.MatchedPatternIndex];
            for (int p = 0; p < pattern.Length; p++)
            {
                string slotName = VoxrCommandParser.ExtractSlotName(pattern[p]);
                if (
                    slotName == null
                    || VoxrCommandParser.IsOptionalSlot(pattern[p])
                    || cmd.HasSlot(slotName)
                )
                    continue;

                var resolution = ResolveOnce(slotName);
                if (!resolution.HasValue)
                    return false;

                _resolveSlotScratch.Add(new VoxrSlotMatch(slotName, resolution.Value));
                // A null reason is normalised to the empty string, because that is what lets
                // GetSlotResolutionReason answer "which slots" and "why" in one call: non-null
                // there means resolver-filled, always. Leave the null through and the single
                // accessor becomes a lie for a resolver that stated no reason.
                _resolveRecordScratch.Add(
                    new VoxrResolvedSlot(slotName, resolution.Reason ?? string.Empty)
                );
            }

            // Nothing to resolve: the command was already complete, so there is no resolution to
            // report and the caller should keep the original.
            if (_resolveRecordScratch.Count == 0)
                return false;

            // Appended AFTER the parser's matched slots, never interleaved. The Editor
            // diagnostics index-match Slots[s] against the parser's per-slot word spans, so an
            // interleaved resolved slot silently mislabels every diagnostic slot after it — and
            // recovering the matched count as Slots.Length - ResolvedSlots.Length depends on it.
            var slots = new VoxrSlotMatch[cmd.Slots.Length + _resolveSlotScratch.Count];
            Array.Copy(cmd.Slots, slots, cmd.Slots.Length);
            for (int s = 0; s < _resolveSlotScratch.Count; s++)
                slots[cmd.Slots.Length + s] = _resolveSlotScratch[s];

            resolved = cmd.WithResolvedSlots(slots, _resolveRecordScratch.ToArray());
            return true;
        }

        // -------- Pending resolution interpreter --------

        void InterpretResolution(PendingResolution resolution)
        {
            switch (resolution.Outcome)
            {
                case PendingOutcome.None:
                    return;

                case PendingOutcome.Confirmed:
                    _debouncer.RecordFire(resolution.Command.Intent, Time.time);
                    OnCommandConfirmed?.Invoke(resolution.Command);
                    OnCommandRecognised?.Invoke(resolution.Command);
                    if (OnCommandsRecognised != null)
                        OnCommandsRecognised.Invoke(new[] { resolution.Command });
                    DrainDeferredGrammarRebuild();
                    break;

                case PendingOutcome.Cancelled:
                    OnCommandCancelled?.Invoke(resolution.Command);
                    DrainDeferredGrammarRebuild();
                    break;

                case PendingOutcome.Entered:
                case PendingOutcome.ReEnteredPending:
                    OnCommandPending?.Invoke(resolution.Command);
                    break;
            }
        }

        string[] GetFollowUpGrammarWords()
        {
            var words = new HashSet<string>(StringComparer.Ordinal);

            VoxrFollowUpVocabulary.AddPhraseWords(words, VoxrFollowUpVocabulary.DefaultConfirm);
            VoxrFollowUpVocabulary.AddPhraseWords(words, VoxrFollowUpVocabulary.DefaultCancel);

            if (confirmVocabulary != null)
                VoxrFollowUpVocabulary.AddPhraseWords(words, confirmVocabulary);
            if (cancelVocabulary != null)
                VoxrFollowUpVocabulary.AddPhraseWords(words, cancelVocabulary);

            var result = new string[words.Count];
            words.CopyTo(result);
            return result;
        }

        // Test-only setters/getters
        internal float PendingTimeout { set => pendingTimeout = value; }
        internal VoxrPendingTimeoutBehavior PendingTimeoutBehavior
        {
            set => pendingTimeoutBehavior = value;
        }
        internal string[] ConfirmVocabulary { set => confirmVocabulary = value; }
        internal string[] CancelVocabulary { set => cancelVocabulary = value; }

        // Like cancelVocabulary, this is frozen into the parser at Configure/RebuildParser time,
        // so a test must set it BEFORE Configure. Getting that wrong is invisible here — the
        // parser records ties whenever the flag is set OR UNITY_EDITOR is defined, and every
        // Unity test runs in the Editor — so it shows up only as the flush not routing.
        internal bool DisambiguateSiblingTies
        {
            set => disambiguateSiblingTies = value;
        }
        internal string TestGrammarJson => _grammar.CurrentJson;
        internal bool TestGrammarRebuildDeferred => _grammar.GrammarRebuildDeferred;
        internal float TestEffectiveBufferWindow => EffectiveBufferWindow;

        // Read-only, and read by the completeness tests (issue #76) rather than copied as a
        // literal. Those tests assert that a candidate CLEARS this gate, so that the refusal
        // they then observe can only have come from the completeness term. Against a hard-coded
        // 0.60 that assertion cannot fail: raising the serialized default would drop the
        // candidates below the gate, reject them on score, and leave the tests passing without
        // ever reaching the branch they exist to pin.
        internal float MinScore => minScore;

        internal void TestForceTimeoutNow()
        {
            if (!_pending.HasPending) return;
            var current = _pending.Current.Value;
            current.CreatedTime = -1000f;
            _pending.ForceSetForTest(current);
            SendMessage("Update", SendMessageOptions.DontRequireReceiver);
        }

#if UNITY_EDITOR
        internal VoxrPendingCommand? EditorPendingCommand => _pending.Current;

        void HandlePartialResult(string text)
        {
            LastPartialResult = text;
        }

        VoxrMatchAttempt BuildAttempt(VoxrCommand cmd,
            VoxrCommandParser.ParseDiagnosticEntry[] parseDiag, int index,
            string[] tokens, float[] wordConf,
            string rejectReason, bool isAccepted)
        {
            string pattern = null;
            string tiedRival = null;
            bool tiedRivalIsSibling = false;
            // Defaulted to "no runner-up" rather than left unassigned: an attempt built with no
            // parse entry behind it (the synthetic paths) has no second-ranked candidate to
            // name, and reporting one would be a claim about a selection that never ran.
            string runnerUpIntent = null;
            float runnerUpScore = -1f;
            VoxrDiagnosticSlotMatch[] diagSlots = Array.Empty<VoxrDiagnosticSlotMatch>();

            if (parseDiag != null && index < parseDiag.Length)
            {
                pattern = parseDiag[index].PatternString;
                tiedRival = parseDiag[index].DescribeTiedRival();
                tiedRivalIsSibling = parseDiag[index].TiedRivalIsSibling;
                runnerUpIntent = parseDiag[index].RunnerUpIntent;
                runnerUpScore = parseDiag[index].RunnerUpScore;

                if (cmd.Slots.Length > 0 && parseDiag[index].SlotStartWords != null)
                {
                    int slotCount = Math.Min(cmd.Slots.Length, parseDiag[index].SlotStartWords.Length);
                    diagSlots = new VoxrDiagnosticSlotMatch[slotCount];
                    for (int s = 0; s < slotCount; s++)
                    {
                        int sw = parseDiag[index].SlotStartWords[s];
                        int ew = parseDiag[index].SlotEndWords[s];
                        float slotConf = VoxrCommandParser.ComputeConfidence(tokens, sw, ew, wordConf);
                        diagSlots[s] = new VoxrDiagnosticSlotMatch(
                            cmd.Slots[s].Name, cmd.Slots[s].Value, sw, ew, slotConf);
                    }
                }
            }

            return new VoxrMatchAttempt(
                cmd.Intent, pattern, cmd.Score, minScore,
                cmd.Confidence, minConfidence, diagSlots,
                rejectReason,
                isAccepted,
                tiedRival,
                tiedRivalIsSibling,
                barred: false,
                runnerUpIntent: runnerUpIntent,
                runnerUpScore: runnerUpScore
            );
        }

        // A barred round produced no VoxrCommand, so it cannot go through BuildAttempt: the
        // intent, pattern and score come off the parser's barred record instead, and the span's
        // confidence is computed here exactly as BuildAttempt computes a slot's.
        //
        // Slots are left empty deliberately — the parser returns above the point where a barred
        // winner's slot array would be built, and building one purely for the log would put
        // allocation back on the path the bar exists to keep cheap.
        VoxrMatchAttempt BuildBarredAttempt(
            in VoxrCommandParser.BarredRoundEntry barred,
            string[] tokens,
            float[] wordConf
        )
        {
            float confidence = VoxrCommandParser.ComputeConfidence(
                tokens,
                barred.StartIdx,
                barred.EndIdx,
                wordConf
            );

            return new VoxrMatchAttempt(
                barred.Intent,
                barred.PatternString,
                barred.Score,
                minScore,
                confidence,
                minConfidence,
                null,
                "barred",
                false,
                tiedRival: null,
                tiedRivalIsSibling: false,
                barred: true,
                runnerUpIntent: barred.RunnerUpIntent,
                runnerUpScore: barred.RunnerUpScore
            );
        }
#endif
    }
}
