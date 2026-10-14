# Phase 1 — persisted plan (2026-10-14)

**Phase 1 - the harness dump** (`architecture.md` `## Build plan`), unsplit: F1 and section 10 item 1, under the maintainer's `## Risks` "Readings". `main.cpp` cited by symbol and line (M:n); after a shift, the symbol governs.

## Files to change

| Path | Change | Why |
|---|---|---|
| `NativeBridge~/harness/main.cpp` | modify | F1; register *Audio time*, *Opt-in*, *Schema*; "Dump writer"; "Dump records" |

Sources are listed explicitly: the writer stays in `main.cpp`'s anonymous namespace, no new file, no CMake edit.

## Contracts and interfaces

**Conventions.** Existing statements keep their behaviour. Records: `nlohmann::ordered_json`, `kind` first, keys in "Dump records" order, each `dump(-1, ' ', false, json::error_handler_t::replace)` plus `\n` - one line, UTF-8, LF (binary stream). Nothing writes to stdout.

**Step 0 - baseline, before any edit.** Verification agent, before the code-writer; `.scratch/joined/` made. From `NativeBridge~`: Command 3's two `cmake.exe` calls on the unchanged tree, then `./build-windows/bin/vosk-bridge-harness.exe` alone with Command 3's three arguments, stdout (report only) to `../.scratch/joined/p0-before.stdout`: exit 0, 16/16.

**Step 1 - state.** Anonymous namespace: a `DumpWriter` (the `std::ofstream`, an `enabled` bit set only by `--dump`, `Write(const ordered_json&)`), one file-scope instance. Every write sits under `enabled`: without `--dump`, stdout, stderr, exit codes and `--write-baseline` behave as today, save the usage text (Step 2; the unknown-argument `Fail`), mandated by the register's *Opt-in* entry. `FixtureOutcome` (M:30-36) gains `pushed`, `prevPushed` (`size_t`) and `seq`, all 0; counters run always, printing nothing.

**Step 2 - CLI.** Flag loop (M:105-112): `--dump <path>`, value required as for `--manifest`, sets path and `enabled` (an empty value fails at open). Usage (M:111) gains ` [--dump <file>]` after `[--write-baseline]`.

**Step 3 - open, header.** After the corpus-drift guard (M:159-175), before `float gain` (M:177), when enabled: a non-empty `parent_path()` made by `create_directories` (error_code form), else `Fail("cannot create directory for dump: <path> (<message>)")`; `std::ofstream` `binary | trunc`, else `Fail("cannot open dump file: <path>")`; exit 2, `<path>` as given. Then `header`: `schema` 1; `manifest`, `fixtures`, `model` as given; `gain`, `libvosk`, `grammar` the parsed manifest's; `ringCapacity` = `RingBuffer<float>::kCapacity`. A later `Fail` (M:181, M:189, M:240) leaves no `end`; Phase 2's reader rejects that - accepted.

**Step 4 - counters.** `PushAll`: where `offset` advances (M:77), `r.pushed` advances by `written` (input-rate samples; both calls, M:218-219). Each `DrainResults` call ends with `r.prevPushed = r.pushed`: every result of one drain carries that drain's pair.

**Step 5 - `result`.** `DrainResults`: after `payload` (M:45), before its handling (M:46-59, unchanged), when enabled: `case` (`r.file`), `seq` (`r.seq++`: per case from 0, contiguous), `final` (`is_final != 0`), `pushed`, `prevPushed`, `payload` = `ordered_json::parse(payload, nullptr, false)`, or, if discarded, `raw` (the string).

**Step 6 - framing**, case loop (M:201-233):
- WAV load fails (M:210-211): `case` with `samples` 0, `sampleRate` 0, then `case-end`.
- Loaded: `case` (`wav.samples.size()`, `wav.sample_rate`) after M:209, before `vosk_bridge_start_push` (M:213). Start fails: `case-end` after its error (M:215). Else `case-end` after the post-stop drain (M:222), before `vosk_bridge_reset` (M:223).
- `case-end`: `case`, `results` = `r.seq`, `pushed`, `errors` = `r.errors`.
- `end` (`cases` = `outcomes.size()`, `passed`) after the tally (M:247-255), before M:256; the stream closes. A bad stream at close prints one stderr line naming the path; exit unchanged.

**Stops for the human:** harness exit 1 (never a re-pin); a non-empty symbol diff; a stdout difference in criterion 3; a download needed; a false runtime claim (PM-ruled; the text fixed before the phase closes).

**Commit set:** `main.cpp`; this plan and the link line; `memory/**/*.meta` if any. Nothing under `.scratch/joined/`. No CHANGELOG line (dev tooling).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| One file-scope writer | Passed through `PushAll`/`DrainResults` | `PushAll` drains at M:85; no signature changes |
| `ordered_json`, own parse, compact | `json` at M:46; raw splice | `json` sorts keys; M:46-59 stay as written; Vosk's multi-line JSON breaks lines |
| `json::error_handler_t::replace` | Strict | A non-UTF-8 `raw` would throw to exit 2 |
| WAV-fail frame `case` 0/0 | No frame | The reader sees every case |
| Bad stream: stderr, same exit | Exit 2 | The dump moves no verdict |
| Criterion 3 vs Step 0's capture | Tail of Command 3's stdout | Build output precedes the report; a pre-edit capture proves no change |

## Acceptance criteria

Verification agent, never the main thread, in order; Git Bash, project root; build output grepped for errors/warnings; byte compares by `python3 -I`, not `cmp`.

1. [ ] Commands 1-2 (binding `## verification`), green as it says; `Tests~` revert done; `memory/**/*.meta` committed, never deleted. (every phase)
2. [ ] Command 3 verbatim, both halves. Arm64: recipe `### Building NativeBridge`, `NativeBridge~/build/` deleted first; `LLVM_NM="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe"`, then `"$LLVM_NM" -D --defined-only NativeBridge~/build/libvosk-bridge.so | awk '{print $2, $3}' | sort > .scratch/joined-rig-parser-phase-1-symbols-post.txt`, its `diff` against `.scratch/windows-native-harness-phase-1-symbols-committed.txt` empty. The arm64 half does not compile `main.cpp` (`VOSK_BRIDGE_BUILD_HARNESS` is off outside the `windows-x64` preset, `NativeBridge~/CMakeLists.txt:114-116`), so this only confirms Command 3 verbatim stays green. If `git status --short` lists the committed `.so`, the PM restores it between dispatches; no recommit. Desktop: stdout to `.scratch/joined/p1-cmd3.stdout` (grepped, not compared), stderr beside it; exit 0, 16/16; no `error C`, `LNK` or `error:` line; `warning C` lines naming `main.cpp` reported. (F1)
3. [ ] Step 0's harness run on the new build, stdout to `../.scratch/joined/p1-nodump.stdout`; again plus `--dump ../.scratch/joined/p1.jsonl`, stdout to `p1-dump.stdout` there: each exit 0 and byte-identical to `p0-before.stdout`. (F1; decision 7)
4. [ ] `p1.jsonl` (`python3 -I`): every line a JSON object, LF, no CR; first one `header`, `schema` 1, `manifest`/`fixtures`/`model` = the arguments, `gain`/`libvosk`/`grammar` = `harness/expectations.json`'s, `ringCapacity` 65536 (`ring_buffer.h` :15); 16 `case`...`case-end` frames in manifest order, `sampleRate` 48000; per case `seq` contiguous from 0, count = `results`; `pushed`/`prevPushed` on every `result`, `prevPushed` <= `pushed`, both non-decreasing; `case-end` `pushed` = `samples` + M:196's padding length; a partial present; every non-final payload's only key `partial`; every final a string `text`, non-empty ones a `result` array of items with `word`, `conf`, `start`, `end`; per case the non-empty final `text`s = its `expectedFinals`; no `raw`, `error`, or non-empty `errors`; one `end`, last, `cases` 16, `passed` 16. (F1; section 10 item 1)
5. [ ] Criterion 3's `--dump` run again into `p1b.jsonl` (stdout `p1b-dump.stdout`, same compare). Gated: per case, final `text`s equal `p1.jsonl`'s. Recorded only (equal, or the differing cases): finals' `result` arrays (`word`, `conf`, `start`, `end`) and partial sequences. (`## Risks`)
6. [ ] Per case, the first non-empty final's first word `start` reported beside `case-end` `pushed` / `sampleRate`; recorded "restart" if every value is below its case's figure, else "run on". (`## Risks`)
7. [ ] `.scratch/joined/p1-notdir` made a regular file; criterion 3's run with `--dump ../.scratch/joined/p1-notdir/x.jsonl`: exit 2, stderr names that path, stdout empty. (decision 7)
8. [ ] After any PM restore: `git diff --stat main -- NativeBridge~/ Runtime/ Tests~/` lists `NativeBridge~/harness/main.cpp` only; its diff shows every dump write under `enabled` and the parse, error and keep branches of `DrainResults` unchanged. (opt-in by construction)

## Open questions that block implementation

None.

## Blast radius

- Every later Command 3 build compiles the writer, inert without `--dump`.
- `.scratch/joined/` fills; `NativeBridge~/build/` rebuilt fresh; the committed `.so` may be rewritten (PM restore).
- Phase 2's reader binds to these records; a schema change is a register amendment.
- `architecture.md`'s `main.cpp` line refs shift (re-pointed at reconcile); `harness/README.md` does not name `--dump` (the usage text, M:111, does).

## Runtime claims

- Criterion 7: MSVC's `create_directories` errors on a regular-file component, or the open fails - exit 2 naming the path either way.
- Criterion 4: nlohmann's re-emission preserves every value the rig reads - `text`, `result[]` `word`/`conf`/`start`/`end`, `partial` (text by the `expectedFinals` match; numbers by nlohmann's round-trip float output).
- Criterion 4: partials are `{"partial": "..."}`; finals carry `result[]` with `conf` and times.
- Criterion 2: Command 3 stays 16/16, exit 0, with the writer compiled in; the symbol list is unchanged.
- Criterion 3: the harness's stdout report is byte-identical across runs of one build with and without `--dump` - nothing timing-dependent is printed, and Git Bash's redirect introduces no CR/LF difference.
- Criterion 5: two dumps of one build hold the same partial sequence.
- Criterion 5: finals' `result` arrays are equal across two dumps of one build - recorded, not gated.
- Criterion 6: word times restart at zero after `vosk_recognizer_reset` (`vosk_bridge.cpp` :307), else Phase 2 offsets per case - recorded which.
