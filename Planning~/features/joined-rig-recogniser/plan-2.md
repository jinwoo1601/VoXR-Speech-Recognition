# Phase 2 — persisted plan (2026-10-15)

**Phase 2 - bridge timing and dump schema 2**, serial: F1, F11 (schema), §6, §10 item 2; bound by §2 Out 1. NB = `NativeBridge~/`, AB = `Planning~/features/coverage-in-selection/ab-rig/`; lines at `90c6c16`.

## Files to change

| Path | Change | Why |
|---|---|---|
| NB `src/result_queue.h`, `src/vosk_bridge.cpp` | modify | *Bridge timing*; F1 |
| NB `src/vosk_bridge.h` | modify | *ABI route*; §10 item 2 |
| NB `harness/main.cpp` | modify | *Schema 2*; F1 |
| AB `JoinedInput.cs`, `Joined.cs` | modify | *Schema 2*, *Checks as commands*; F1, F11 |
| AB `Rig.csproj` | modify, comment only | names `timing` |
| `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` | regenerate | *ABI route*; §6 |
| `plan-2.md`; `architecture.md` | create; link line | this plan; committed at this pickup |

## Contracts and interfaces

**Dispatch.** A, verification agent: Step 0. B, `code-writer`: Steps 1-6, atomic. C, verification agent: criterion 2. B2, `code-writer`: Step 7. C: criteria 3-11. No agent git write; text reports, PM-filed in `.scratch/`; scripts as `env -u MSYS_NO_PATHCONV bash AB/<script>`.

**Step 0 - baseline.** In NB `build` (its cache names this tree), Command 3 verbatim: 16/16, exit 0, stdout to `.scratch/joined/jrr-p2-base.stdout`; the committed `.so`'s `vosk_bridge_*` dynamic symbols (NDK `llvm-readelf --dyn-syms` or `llvm-nm -D`); DocCheck, `warning` lines counted; `git status --short`. `.scratch/joined/p4.jsonl` (schema 1) absent: remake it now, per `Planning~/features/joined-rig-parser/plan-4.md` criterion 3; after Step 1 it cannot be.

**Step 1 - `result_queue.h`.** `QueuedResult` (:8-11) gains `int64_t consumed, origin`; `Push` (:19-22) stores them after `is_final`; `Pop` (:24-31) moves them out; under the lock (:45).

**Step 2 - `vosk_bridge.cpp`.** Globals (:20-42), `static`: `std::atomic<int64_t> g_consumed{0}`, `int64_t g_decoder_fed`, `int64_t g_session_origin`. `recognition_loop`: after a non-zero `Read` (:91), before the downsample (:116), `g_consumed += read_count`; in the `ds_count > 0` accept (:124-126), `g_decoder_fed += ds_count`. Every `Push` (:84, :132, :139, :146, :160) passes `g_consumed`, `g_session_origin`. `start_internal`, after `reset_pipeline()` (:178), before the spawn (:193): `g_consumed = 0; g_session_origin = g_decoder_fed;`. `g_decoder_fed = 0` at each recognizer creation (:225, :363, :365), not in `vosk_bridge_reset` (:307). After `vosk_bridge_get_result` (:383-391): `void vosk_bridge_get_result_position(int64_t* out_consumed, int64_t* out_origin)` fills each non-null out-param from `g_current_result` (:42; zeros before any result); `int64_t vosk_bridge_get_consumed(void)` returns `g_consumed`, held after `vosk_bridge_stop` until the next `start_internal`.

**Step 3 - `vosk_bridge.h`.** Both `VOSK_BRIDGE_EXPORT`, under `// Results` after :65; `stdint.h` (:4); no export list (hidden-visibility preset).

**Step 4 - `main.cpp`**, only under `--dump`. `constexpr int kDecoderRate = 16000`: to `vosk_bridge_init` (:263) as `float`; header `decoderRate` (integer) and `schema` 2 (:250), phase 1's fields kept. `FixtureOutcome` (:31-40) gains `int64_t consumed = 0`. `DrainResults` (:78-117) reads `vosk_bridge_get_result_position` into each non-null result's `consumed`, `origin`. After `PushAll(padding)` (:307), before the drain and stop (:309): loop `DrainResults` until `vosk_bridge_get_consumed() == static_cast<int64_t>(r.pushed)`, sleeping 5 ms; past 2000 sleeps in a row without `consumed` progress (about 10 s; PushAll's rule, :136-139), a `drain stalled` error in `r.errors`, then stop. After the post-stop drain, `r.consumed = vosk_bridge_get_consumed()`, written by `DumpCaseEnd` (:65-73). `expectations.json` untouched.

**Step 5 - `JoinedInput.cs`.** Header (:159-161): `schema` 1 or 2, else exit 2; schema 2 needs integer `decoderRate` > 0; `Dump` (:61-69) gains `Schema`, `DecoderRate`. Schema 2, else a named exit 2 (file, line): `result` integer `consumed`, `origin` ≥ 0; `case-end` integer `consumed` ≥ the last result's; in a case `consumed` non-decreasing and ≤ `pushed`, `origin` constant. Only the reader enforces these (F1's never-decreases evidence). Schema 1 needs neither; extra keys ignored. `DumpCase` (:46-59) gains case-end `Pushed`, `Consumed`, and `Results` in dump order: `Seq`, `Final`, `Consumed`, `Origin` (null in schema 1), the payload's raw JSON text or `Raw`.

**Step 6 - `Joined.cs`.** Verb `timing` (:80-95): `--joined timing --dump D [--dump-b D2]`, own parse (each once, else `unknown argument`, exit 2); `ParseOptions` (:371-423) unchanged. Schema 1: exit 2, `<dump>: schema 1, no positions`. `FAIL <case>: <what>` only for: per dump, case-end `consumed` ≠ `pushed`; with two, unequal case sets or ordered non-empty final texts, or finals paired by order in a case unequal in word count or any word's per-case `start`, `end` (printed − `origin` ÷ `decoderRate`, `decimal` from the number's text, never `double`; exact). Then `timing: largest consumed difference <d> input samples`, over those pairs, never results by `seq`; `timing: <p>/<n> cases passed`; exit 0 all pass, 1 a FAIL, 2 a stop. Named in the header verb list (:12-14), exit codes (:16-20), usage (:52-55), unknown-verb stop (:95), and `Rig.csproj`'s entry-points comment (:13-14).

**Step 7 - the `.so`** (B2, after criterion 2): `cp NativeBridge~/build/libvosk-bridge.so Runtime/Plugins/Android/arm64-v8a/`.

**Stops (PM):** a Command 3 mismatch (exit 1) or a `--dump` stdout off Command 3's: the human's, never a re-pin. `timing` red on two dumps (a consumed difference > 4096 included): a false claim, no tolerance. An unlisted file in a build error; a `Runtime/` change beside the `.so`, `.meta` included; criterion 7 off, `report.md` pair included. Never a relaxed compare. *Staging* trap: no drive through `InjectResult`, `InjectPartialResult`, `InjectText`, `FlushPendingBuffer`. `D:\Workspace\VoXR TestGround\Temp\UnityLockfile` present before criterion 9.

**Commit set (PM, `vc` binding):** the seven edited files (six plus `Rig.csproj`) and the `.so`; `plan-2.md` and the link line go at this pickup.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `void` getter, nullable out-params | `int` status | nothing can fail |
| Input and decoder counters apart | one derived | decimation by 3 carries phase |
| Drain stall: case error, then stop | exit 2 | PushAll's form |
| `timing` parses its own options | `--dump-b` in `ParseOptions` | `score`, `report` unchanged |
| `timing` exit 0/1/2 | 0/2 | `drive-check`'s |

## Acceptance criteria

The gate: 2-11, in order. The Quest check: human-only, at G2, never claimed.

1. [ ] Step 0 recorded. (F1; §6)
2. [ ] Command 3 verbatim: arm64 0 errors; 16/16, exit 0, stdout `cmp`-equal to Step 0's, as `.scratch/joined/jrr-p2-cmd3.stdout`; `git status --short -- NativeBridge~/harness/expectations.json` empty. (F1; §6)
3. [ ] From NB, the harness with Command 3's arguments plus `--dump ../.scratch/joined/jrr-p2-a.jsonl`, stdout to `../.scratch/joined/jrr-p2-a.stdout`; again as `jrr-p2-b`: each exit 0, stdout `cmp`-equal to criterion 2's. (F1)
4. [ ] `stage.sh WORKTREE .scratch/jrr-p2-stage recogniser`; there `dotnet build -v q --nologo`: 0 errors. (F11)
5. [ ] There `dotnet run --no-build -- --joined timing --dump ../joined/jrr-p2-a.jsonl`, then `-b`: each exit 0, `<n>/<n>`; both (`--dump-b`): exit 0, `<n>/<n>`, difference ≤ 4096; `../joined/p4.jsonl`: exit 2, `no positions`. `python3 -I` on `jrr-p2-a.jsonl`'s last case with a non-empty final, in dump order: first word's per-case start in [0, `samples` ÷ `sampleRate`). (F1)
6. [ ] There `--joined drive-check`: exit 0, `5/5`. (F11)
7. [ ] `joined.sh --vary parser --dump .scratch/joined/jrr-p2-a.jsonl --ref-a main --ref-b main --out .scratch/joined/jrr-p2-f11`: exit 0, `Flips: 0`, plan-1 criterion 6's counts; the same on `.scratch/joined/p4.jsonl`, `--out .scratch/joined/jrr-p2-f11-s1`; the two `report.md` `cmp`-equal. (F11)
8. [ ] DocCheck verbatim: exit 0, no `FAIL`, `<n>/<n>`; warnings as Step 0. (F11)
9. [ ] Commands 1-2, host Editor closed: green. (§6)
10. [ ] The `.so`'s dynamic symbols: both new names and every Step 0 name; `git status --short -- Runtime/` only the `.so`. (§10 item 2)
11. [ ] Canaries, each a `.scratch/joined/` copy of `jrr-p2-a.jsonl` edited only by a `python3 -I` one-off; `timing` as in 5: (i) a final's word `end` changed, `--dump` `jrr-p2-a`, `--dump-b` copy: exit 1, the case named; (ii) a result's `consumed` below its predecessor's in the case, `--dump` copy: exit 2, file and line named; (iii) a case-end `consumed` set to its `pushed` + 1, `--dump` copy: exit 1, the case named. Each red, then the unedited dumps green. (F1)

## Open questions that block implementation

None.

## Blast radius

- `JoinedInput.cs`, `Joined.cs`: every stage (`stage.sh` :73-76), DocCheck, `joined.sh`.
- The `.so`: every device build; `BridgeNative.cs` binds nothing new.
- Hot path: two adds per chunk, two 64-bit copies per result.
- Owed after G2: `Documentation~/native-bridge.md` ABI table, `harness/README.md`.
- Phase 3 binds `DumpCase.Results`, `Schema`, `DecoderRate`.

## Runtime claims

- a (5): word times count from recognizer creation, so per-case times put a first word near its onset.
- b (3): the drain moves no final; verdict, stdout unchanged.
- c (5): two `--dump` runs give equal origins and per-case word times.
- d (5): chunk boundaries match across runs within one read.
- e (3): the drain reaches `consumed` = `pushed` within the stall bound.
- f (5): `vosk_bridge_get_consumed` keeps the case's count after `vosk_bridge_stop`.
- g (5): equal decoder times re-serialize to equal number text.
- h (10): both exports are arm64 `.so` dynamic symbols under hidden visibility.
- i (2): Command 3's stdout is `cmp`-equal across baseline and changed builds.
- j (1, 10): the Unity 6000.3.7f1 NDK ships `llvm-readelf` or `llvm-nm`, listing the `--strip-all` `.so`'s `.dynsym`.
- k (9, 10): after Commands 1-2, `git status --short -- Runtime/` shows only the `.so` (a rewritten `.meta`: PM stop).
- l (5): nlohmann's `start`/`end` text parses as `decimal`, exponent forms included.
