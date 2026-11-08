---
type: requirements
feature: bench-paced-mode
topic: audio-bench
status: draft
updated: 2026-11-09
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0001-two-clocks-split.md
  - Planning~/design-docs/audio-bench/decisions/adr-0002-bridge-and-rig.md
  - Planning~/design-docs/audio-bench/decisions/adr-0003-fast-and-paced-modes.md
  - Planning~/features/bench-fast-replay/requirements.md
---

# feat-bench-paced-mode — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on `bench-fast-replay` (merged as PR #198, `63434cb`, accepted at G2 2026-11-09). Path `development`.

## 1. User story

- **S1** — As the maintainer measuring a Phase D change, I want a paced bench mode that replays the TTS fixtures into the real native bridge in 20 ms chunks at real speed, stamps each stage on wall time beside audio time, splits each stage's latency into the audio-time part and a desktop-only compute part, gives the decoder's real-time factor, and carries the command event's wall stamp through the joined rig, so that I can see what a change costs in decode and queue lag — the part fast mode is blind to — and trust that split because a matched-chunk self-check guards it.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-paced-mode` | Paced 20 ms replay with wall stamps and the real-time factor (adr-0001, adr-0003); the matched-chunk split self-check; the command event's paced-arrival wall stamp (adr-0002). Path: `development`. **Full.** | `bench-fast-replay`

**Agreed at G0 on 2026-11-09** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; the lab writes only under `.scratch/lab-next-level-probes/` and merges nothing).

**In**

1. Paced mode: the TTS fixtures pushed into the real native bridge in 20 ms chunks at real speed (adr-0002, adr-0003); the 20 ms chunk is provisional → the tuning pass.
2. Wall stamps — a monotonic clock read at the bench's push and at its pop of each result — for first partial and final = endpoint, beside the audio-time stamps; each stage's latency split into an audio-time part and a compute part (wall − audio), the compute part labelled desktop-only (adr-0001).
3. The real-time factor: decode time ÷ audio duration, for paced runs only (adr-0003).
4. The command event's paced-arrival wall stamp: the joined rig delivers each result at its recorded paced wall-arrival time, on its frame clock; the command stage's compute part is its paced time − its fast time (adr-0002).
5. The matched-chunk split self-check: paced timing at fast mode's 16384-sample pushes must match fast mode's audio-time stamps to within one decoder read (4096 samples), or the split is wrong and the bench stops, quoting no numbers (adr-0001, adr-0003).
6. Paced-vs-fast transcript differences at 20 ms pacing reported as a finding, not a self-check failure (adr-0003).
7. Reports extending `bench-fast-replay`'s: they name the mode and carry the paced stamps, the split and the real-time factor.

**Out**

1. The seeded condition pipeline, the stand-in negative set and per-condition curves — `bench-conditions`; Kokoro-82M voices — `bench-voices`; repeated and paired A/B runs, p50/p90/p99 and McNemar — `bench-ab-report`.
2. Measuring the C# recogniser's frame cost live (the rig's frame clock models it, adr-0002); Quest's absolute timing and how the on-device stamp is taken; comparing wall numbers across machines.
3. Developer usability; ranking two decoders or decoder configurations on TTS-only audio (C12) — the design's non-goals.
4. Left untouched: the bridge interface (a change found needed is a scope stop, design §4); Command 3 and its pinned transcripts ("re-pin, never edit"); the committed fixture WAVs.

## 3. Why — the value it delivers

Fast mode (`bench-fast-replay`) gives the audio-time half of each stage's latency, the deterministic part that transfers to Quest unchanged, and the accuracy axis; it is blind to the compute half (adr-0001, adr-0003). This feature adds that half: decode and queue lag on wall time, and the decoder's real-time factor — what Quest's largest unmeasured budget row needs, the decoder catch-up that "adds ~0.5–1.0 s" (`I3-latency-ux-redesign.md`, budget row 7; design §0 In 1) — and it shows whether device-like 20 ms chunking moves vosk's endpoints against the harness's large fast pushes (adr-0003). The thing protected when a tradeoff forces a cut: the split into audio-time and compute parts is trustworthy, guarded by the matched-chunk self-check, or the bench gives no numbers (adr-0001).

## 4. Observable behaviour

1. The maintainer runs paced mode naming a configuration, as fast mode takes it: model, recogniser settings, endpointer profile, gain.
2. The bench runs fast mode first, in the same run, with its checks unchanged — the truth check and the pinned-transcript self-check; a failure there stops the run as fast mode does.
3. The matched-chunk pass: all 52 cases pushed into the real native bridge at real speed in fast mode's 16384-sample pushes. Each case's finals text must equal fast mode's, and its first-partial and each final's audio-time stamp must lie within one decoder read (4096 samples) of fast mode's; a missing or extra final fails. On any failure the bench names the differing cases, gives no numbers, and exits non-zero.
4. The 20 ms pass: all 52 cases pushed into the real native bridge at real speed in 20 ms chunks (960 samples at the fixtures' 48 kHz). Each result is stamped on audio time and wall time, and the pass's timeline is fed to the real C# recogniser through the joined rig at each result's paced wall-arrival time, so the command event is stamped on both clocks.
5. The report: fast mode's report, unchanged, plus a paced section. Per case and per stage — first partial, each final, command event — the audio-time offset from end of speech, the wall-time offset, and the compute part (wall − audio); the command stage's compute part, its paced time − its fast time; the real-time factor per case and over the corpus; the 20 ms finding — the cases whose finals differ from fast mode's, both shown, and the cases whose command class differs from fast mode's, both shown, or `none`; and per case the largest push lateness behind the real-time schedule. The paced section is labelled desktop-only, a single run with its run-to-run spread not measured, and never comparable across machines.
6. A paced run takes about twice the corpus's audio in real time beyond fast mode — 193 s of audio over 52 clips, so about 6.5 minutes.

## 5. Functional requirements

**Ruled on 2026-11-09** by the maintainer, in session, at SETTLE: S1 confirmed as G0 recorded it; R1, a single paced run, the compute part labelled "single run, spread not measured", repeats and run-to-run spread left to `bench-ab-report` (F8); R2, the matched-chunk self-check compares finals text and the first-partial and each final's audio-time stamp, a missing or extra final failing (F6); R3, the 20 ms pass scores the command class only, shown beside fast mode's and flagged where it differs, WER and ECE/NCE staying fast mode's (F7); each took the recommended option; the rest settled as drafted.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | Paced replay of all 52 cases into the real native bridge at real speed in 20 ms chunks (960 samples at 48 kHz), the chunk size provisional → the tuning pass. | S1 | Must | A green paced run over all 52 cases. | adr-0002; adr-0003; design Status |
| F2 | Wall stamps on a monotonic clock read at the bench's push and at its pop of each result; each stage's wall time given as a signed offset from end of speech. | S1 | Must | A known-value toy of the stamp arithmetic in the plan's own checks gives its stated offsets; the green run's report gives each case's wall offsets. | adr-0001; design §2 |
| F3 | Per case and per stage — first partial, each final, command event — the latency split into an audio-time part and a compute part (wall − audio), the compute part labelled desktop-only. | S1 | Must | The F2 toy gives its stated parts; the green run's report gives both parts per case and stage, with the label. | adr-0001 |
| F4 | The real-time factor, decode time ÷ audio duration, per case and over the corpus, for paced runs only. | S1 | Must | A known-value toy gives its stated factor; the green run's report gives the factor per case and over the corpus. | adr-0001; adr-0003; design §2 |
| F5 | The command event's paced wall stamp: the joined rig delivers each result at its recorded paced wall-arrival time on its frame clock; the command stage's compute part is its paced time − its fast time. | S1 | Must | The green run's report gives each case's command event on both clocks and its command-stage compute part, or `none` where no command fires. | adr-0002 |
| F6 | The matched-chunk split self-check: at real speed in 16384-sample pushes, each case's finals text equals fast mode's and its first-partial and each final's audio-time stamp lies within one decoder read (4096 samples) of fast mode's; a missing or extra final fails; on any failure no number is given, the differing cases are named and the run exits non-zero. | S1 | Must | The green run's self-check passes; a mutation shifting one case's stamp by more than 4096 samples makes the bench refuse numbers and name the case. | adr-0001; adr-0003; design §3; R2 |
| F7 | The 20 ms finding: the cases whose finals differ from fast mode's, and the cases whose command class differs from fast mode's, listed with both sides, reported as a finding and never a self-check failure. | S1 | Must | The green run's report lists the differing cases with both sides, or says `none`. | adr-0003; R3 |
| F8 | The report extends fast mode's: it names the mode, keeps fast mode's sections, adds the paced section, and labels the paced numbers desktop-only, single run with spread not measured, and never comparable across machines. | S1 | Must | The green run's report has fast mode's sections, the paced section and every label. | adr-0001; adr-0008; R1 |
| F9 | Per case, the largest lateness of a push behind its real-time schedule. | S1 | Should | The green run's report gives each case's largest push lateness. | design §3 (machine noise) |
| F10 | Untouched: Command 3 and its pins, the committed WAVs, the bridge interface and `Runtime/`; a bridge interface change found needed is a scope stop; a fast-only run's report is unchanged. | S1 | Must | Command 3 green; `git diff main` empty for the committed WAVs, `expectations.json`, the bridge's public interface and `Runtime/`; a fast-only run's report is byte-identical to `main`'s on the same inputs. | design §4; adr-0001; adr-0002; adr-0008 |

## 6. Non-functional requirements

- **Performance:** a paced run takes about twice the corpus's audio in real time beyond fast mode (193 s of audio, about 6.5 minutes); the design rules no other budget.
- **Robustness at the input boundary:** fast mode's named stops hold; a push the bridge refuses stops the run naming the case; an undefined value prints `undefined` with the reason, never a made-up number.
- **Degraded dependencies:** a loaded machine shows as push lateness (F9) and in the compute part, never as a stop; wall numbers are never compared across machines.

## 7. Non-goals / deferred

- The G0 Out list (§2).
- Repeated runs, run-to-run spread, and percentiles across runs or cases — `bench-ab-report` (R1).
- WER and confidence calibration for the 20 ms pass; they stay fast mode's (R3).
- Being a verification gate: the bench never is one (adr-0008).
- A latency target: none is set (design Status).

## 8. Dependencies & assumptions

- Depends on `bench-fast-replay`, merged as PR #198 (`63434cb`); uses the merged joined rig and scorer (PRs #177, #178) and the Windows native harness build (`verification` Command 3).
- Assumed: the maintainer runs it on the Windows desktop, on one machine per comparison.
- Assumed: the desktop gives a monotonic clock fine enough to resolve a 20 ms push schedule.

## 9. Acceptance criteria (G2)

- [ ] A green paced run over all 52 cases, with the matched-chunk self-check passing.
- [ ] Mutation: one case's stamp shifted by more than 4096 samples makes the bench refuse numbers and name the case.
- [ ] Known-value toys for the stamp arithmetic, the split and the real-time factor give their stated values.
- [ ] The green run's report has fast mode's sections, the paced section with every F3, F4, F5, F7 and F9 field, and every F8 label.
- [ ] A fast-only run's report is byte-identical to `main`'s on the same inputs.
- [ ] Command 3 is green.
- [ ] `git diff main` is empty for the committed WAVs, `expectations.json`, the bridge's public interface and `Runtime/`.
- [ ] Human-only verification: the maintainer reads one case's paced numbers in the green run's report and judges them plausible.

## 10. Open questions

1. How the paced push reaches the bridge — a new program linking it, or a mode of the harness — with Command 3's pinned run unchanged either way — the architecture (adr-0002).
2. How decode time is measured for the real-time factor without a bridge interface change; if it cannot be, a scope stop — the architecture (adr-0001).
3. How the per-case flush padding is pushed in the paced passes — the architecture.
4. The wall-time reference for end of speech, from which wall offsets count — the architecture (adr-0001).
5. How the stamps are taken and recorded — the architecture (adr-0001).
