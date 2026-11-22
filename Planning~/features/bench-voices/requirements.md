---
type: requirements
feature: bench-voices
topic: audio-bench
status: draft
updated: 2026-11-22
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0005-kokoro-voices.md
  - Planning~/features/bench-fast-replay/requirements.md
---

# feat-bench-voices — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on `bench-fast-replay` (merged as PR #198, `63434cb`). Path `development`.

## 1. User story

- **S1** — As the maintainer measuring a Phase D change on the desktop bench, I want the bench to run its cases in several Kokoro-82M voices beside the baseline Piper voice, so that a change that breaks on other speakers shows up before a Quest session, while the baseline's pinned transcripts never move.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-voices` | Kokoro-82M voices beside the baseline Piper voice (adr-0005). Path: `development`. **Full.** | `bench-fast-replay`

**Agreed at G0 on 2026-11-22** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; the lab writes only under `.scratch/lab-next-level-probes/` and merges nothing) and with `release` (held, no branch; the only overlap `CHANGELOG.md`, a release file).

**In**

1. Kokoro-82M (Apache-2.0) as a second voice source in the bench: each case is a clip × condition × voice (adr-0005; design §2 "State, per run").
2. The baseline Piper voice `en_US-lessac-medium` kept as the baseline voice; its pinned transcripts never move (adr-0005; `05-rulings.md` :98, "re-pin, never edit").
3. Every report names the voice it ran with (adr-0005; design §3, hidden config differences).
4. Until the maintainer has downloaded the Kokoro model, the bench runs the baseline voice only (adr-0005 Consequences).
5. The voice count is provisional → the tuning pass (~12 per R6) (adr-0005; design Status, "All numbers provisional").

**Out**

1. The Kokoro model download itself — a human step, the maintainer's (adr-0005).
2. Re-pinning or editing the existing Piper fixtures and their pins.
3. Ranking two decoders or decoder configurations on TTS-only audio, any voice set included (C12; adr-0005 Consequences), and gating verification (adr-0008); developer usability and Quest's absolute timing — the design's non-goals.
4. Paced mode — `bench-paced-mode`; paired A/B runs — `bench-ab-report`.

## 3. Why — the value it delivers

Every bench number today comes from one synthetic speaker, `en_US-lessac-medium` (`R6-evaluation-methodology.md` :259: "Any speaker-dependent regression is invisible by construction"). This feature adds Kokoro-82M voices so a change that breaks on other speakers shows before a Quest session (adr-0005), serving the maintainer's accuracy axis (`00-brief.md` :40-41). More voices are still TTS: they show where a change breaks, never rank decoders or prove a gain (C12; adr-0005 Consequences; adr-0008; `R6-evaluation-methodology.md` :372). The thing protected when a tradeoff forces a cut: the baseline voice still ties exactly to the pins, and voice results are never pooled into one pass rate.

## 4. Observable behaviour

1. Once, after downloading the Kokoro model, the maintainer renders the voiced set in the 12 voices into a local cache outside the repository; the render refuses, by name, any phrase it cannot speak whole.
2. The maintainer runs fast mode as `bench-fast-replay` takes it; with no voice named the bench behaves exactly as today.
3. Naming voices — all, or a named subset — the bench first runs the clean reference over all 52 baseline cases and checks it against the pins; on any mismatch it names the differing cases, gives no numbers and exits non-zero.
4. It then replays each voiced clip clean in each named voice; naming the condition grid as well crosses them, each voiced clip under each condition from the run's seed.
5. The report: a header naming the full config plus, per voice, its id, its licence, the Kokoro package version and the model digest; per case and voice its command class, WER and signed audio-time latency offsets, with no aggregate across voices; per voice the negative set's false fires apart from the positive set. It is labelled TTS-only and provisional.
6. Where the cache is absent the run gives the baseline voice only and says `voices: baseline only — Kokoro not provisioned`; where some voices are missing it names them and runs the rest.

## 5. Functional requirements

**Ruled on 2026-11-22** by the maintainer, in session, at SETTLE: S1 confirmed as G0 recorded it; R1, Kokoro renders live in an untracked local cache and nothing Kokoro is committed (F1); R2, the voiced set is the 15 spoken non-pause phrases and the 16 negative phrases (F1); R3, voices run clean by default and cross the condition grid on request (F5, F11); R4, the 12 voices (F1); R5, the rest as drafted; each took the recommended option.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The voiced set — the 15 spoken non-pause phrases (the 16 conditioned cases less silence) and the 16 negative phrases — rendered by the maintainer in 12 Kokoro-82M English voices: af_heart, af_bella, af_nicole, af_sarah, bf_emma, bf_isabella, am_michael, am_fenrir, am_puck, bm_george, bm_fable, bm_lewis (6 female, 6 male; 7 American, 5 British; count provisional → the tuning pass), into an untracked local cache outside the repository; nothing Kokoro is committed. | S1 | Must | After a render the cache holds 372 clips (31 × 12); `git status --short` shows no new path outside `.scratch/`. | adr-0005; R1, R2, R4; `R6-evaluation-methodology.md` :319 |
| F2 | Each voiced clip has the generator's layout — 48 kHz mono 16-bit, 0.3 s lead, 1.0 s tail, peak in [0.04, 0.4] — and its speech truth by the same −40 dBFS scan between lead and tail as the committed fixtures. | S1 | Must | The bench's truth check passes on every cached clip; a known-value toy clip's truth is exact. | adr-0006; `bench-fast-replay` truth mode |
| F3 | The baseline untouched: with no voice named the report is byte-identical to `main`'s on the same inputs; the committed Piper fixtures, the negative renders and `expectations.json` are unchanged. | S1 | Must | `cmp` of a no-voice report with `main`'s gives 0; `git diff main` is empty for the committed WAVs and `expectations.json`. | adr-0005; design §3 |
| F4 | The clean reference first: a voiced run first runs all 52 baseline cases and must pass the pinned-transcript self-check, or no number is given, the differing cases are named and the run exits non-zero. Voiced clips carry no pins. | S1 | Must | With one pin altered in a scratch copy, a voiced run refuses numbers and names the case. | adr-0003; adr-0005; design §3 |
| F5 | A voiced run replays each voiced clip clean in fast mode, in all 12 voices or a named subset, scored as `bench-fast-replay` scores — command class, WER, ECE/NCE, signed audio-time latency offsets — against each phrase's expected intent and text. | S1 | Must | A green voiced run's report lists each voiced clip once per named voice with those scores; a run naming two voices lists only those two. | adr-0005; `bench-fast-replay` F4-F9; R3 |
| F6 | Per-voice results, never pooled: every figure is per voice; each curve or rate point states its n in phrases; significance counted per phrase, with the voice as the resampling block; the negative set's false fires per voice, apart from the positive set and never netted. | S1 | Must | The green run's report has no figure pooled across voices and an n on every point. | design :132; `R6-evaluation-methodology.md` :198, :370; adr-0007 |
| F7 | Every report names, per voice, its id, the Kokoro package version and the model digest, beside the full config; labelled TTS-only and provisional, never ranking decoders. | S1 | Must | The header names every F7 field and carries the labels. | adr-0005 Consequences; design §3; adr-0008 |
| F8 | Not provisioned: with the cache absent the run gives the baseline voice only and says `voices: baseline only — Kokoro not provisioned`, exit 0; with some voices missing it names them and runs the rest. | S1 | Must | A run with the cache moved aside prints that line; a run with one voice's clips removed names that voice. | adr-0005 Consequences |
| F9 | The render refuses, naming the phrase and the word, any phrase with a word Kokoro's English lexicon cannot pronounce without the optional GPL espeak-ng fallback; no GPL component is required to render. | S1 | Must | A toy phrase with an invented word is refused by name; the render's environment lists no espeak-ng. | `R6-evaluation-methodology.md` :18, :321 |
| F10 | Each voice's licence is recorded with its renders and named in the report. | S1 | Should | The cache's record and the report header name a licence per voice. | `R6-evaluation-methodology.md` :321 |
| F11 | On request, voices cross the condition grid: naming voices and `--condition grid` runs each voiced clip under each condition from the run's seed, curves per voice per condition; voices run clean by default. | S1 | Should | A crossed run's report gives a curve per voice per condition, each with its n; two runs with one seed give identical reports. | design :86; adr-0004; R3 |
| F12 | Paced mode stays baseline-voice only: a paced run naming voices is refused with a message. | S1 | Must | `bench.sh --paced` with voices named exits non-zero naming the reason. | adr-0003; scope Out |
| F13 | Untouched: Command 3, its pins, the committed fixture WAVs, `expectations.json` and the bridge interface; a bridge interface change found needed is a scope stop. | S1 | Must | Command 3 green; `git diff main` empty for those paths and the bridge's public interface. | design §4; adr-0005 |

## 6. Non-functional requirements

- **Performance:** the design rules no budget; none is set. A crossed run (F11) is about twelve times a grid run; the report states its clip count.
- **Robustness at the input boundary:** an unknown voice name is refused with the valid list; a cached clip with a missing or failing truth, or with the wrong rate or format, is named and stops the run; an unpronounceable word is refused at render (F9) — never a silent drop or a made-up number.
- **Degraded dependencies:** the Kokoro cache absent or partial is not an error (F8); the clean reference failing its self-check stops the run with no numbers (F4).

## 7. Non-goals / deferred

- The G0 Out list (§2).
- Committing Kokoro renders (R1); voices in paced mode (F12).
- Significance tests, McNemar, percentiles and summaries across runs — `bench-ab-report`.
- Tuning the voice count or the voice list — the tuning pass.
- Voicing the 36 pause cases or the silence case (R2).
- Being a verification gate: the bench never is one (adr-0008).

## 8. Dependencies & assumptions

- Depends on `bench-fast-replay` (merged as PR #198, `63434cb`); F11 on `bench-conditions` (merged as PR #201, `cae67e6`); F12 on `bench-paced-mode` (merged as PR #199, `8de2f69`).
- Human step: the maintainer installs the `kokoro` Python package (0.9.4, 2025-04-05; Python ≥3.10, <3.13 per PyPI; torch) and downloads the Kokoro-82M v1.0 model (about 330 MB) and the 12 voices once (adr-0005); Kokoro and its G2P `misaki` are Apache-2.0, espeak-ng (GPL-3.0) optional (read 2026-11-22: huggingface.co/hexgrad/Kokoro-82M, pypi.org/project/kokoro, github.com/hexgrad/misaki).
- Assumed: the maintainer runs it on the Windows desktop; the bench run itself needs no Python, as today.

## 9. Acceptance criteria (G2)

- [ ] A green voiced fast run in all 12 voices, the clean reference's self-check passing (F4, F5).
- [ ] A run naming no voice gives a report byte-identical to `main`'s (F3).
- [ ] Mutation: one pin altered in a scratch copy makes a voiced run refuse numbers and name the case (F4).
- [ ] The truth check passes on every cached clip, and the known-value toy is exact (F2).
- [ ] The report gives per-voice figures with n per point and none pooled (F6), and every F7 header field.
- [ ] With the cache moved aside the run says `voices: baseline only — Kokoro not provisioned` (F8); an invented word is refused by name at render (F9); a paced run naming voices is refused (F12).
- [ ] Command 3 is green; `git diff main` is empty for the committed WAVs, `expectations.json` and the bridge's public interface; `git status --short` shows no Kokoro file in the repository (F1, F13).
- [ ] Human-only verification: the maintainer provisions Kokoro (install, download, render) and listens to one clip per voice, approving the 12.
- [ ] The commands the project's `verification` binding names are green.

## 10. Open questions

1. Whether Kokoro renders the same bytes on every CPU run (no source states it; the model code never switches to eval mode) — the architecture, by probe; it decides whether the cache records a digest per clip.
2. How a per-clip voice is carried to the report, the corpus manifest's `voice` being one value per manifest — the architecture.
3. The 24 kHz → 48 kHz resample, and the cache's location and keying — the architecture.
4. Where the render step lives (`generate.py` beside Piper, or its own script) and how a voice subset is named on the command line — the architecture.
5. The crossed run's run time — measured at build; no budget is ruled.
6. The voice count and list are provisional → the tuning pass (design Status).
