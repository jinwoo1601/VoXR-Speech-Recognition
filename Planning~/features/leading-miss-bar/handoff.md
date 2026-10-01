You're picking up: **implement `feat-leading-miss-bar`** — backlog item 1 of the G1-locked #124 design. The plan is already approved and persisted; read the files below, then start at Next steps.

## Objective

Implement the leading-required-miss bar per the **approved plan persisted in `Planning~/features/leading-miss-bar/architecture.md` §10**. Five phases (Step 0 already done), ending at a PR the human rules on at G2. **Do not implement backlog items 2–4** (product docs, construction warning, release).

## Context

- **Repo:** `/mnt/d/Game Development/VoXR-Speech-Recognition` — bare Unity UPM package, **PUBLIC**. Branch `feat-leading-miss-bar`, off `main` at `b83b62f` (v1.5.0), **zero commits so far, tree clean**.
- **Issue #124:** `time to target track one two four four` fires `query_time_to_target` (1.0000) *and* `intercept_target` (0.6667) — "intercept" never spoken. A read-only query executed a maneuver order. The human is both maintainer and reporter.
- **Lane:** design & feature lane. G1 is done (2026-08-27, ten DRs). **G2 is the remaining hard stop.**
- **The rule (DR-1):** a candidate whose **first required element** matched nothing may compete and consume, but **may not fire**. Refuse-to-FIRE, not refuse-to-compete (DR-4) — the barred candidate still wins its round and consumes its span, which is what keeps leading debris off the next command.
- **All measurement is DONE.** Do not re-measure. 699 rows, 5 arms: 48 rows change, 17 fire nothing, **0 clean 1.0000 commands destroyed**.

## Current state

**Done this session:**
- Feature branch created; **working tree repaired and clean** (an interrupted baseline run had renamed `Tests~`→`Tests` and left 60 generated `.meta` orphans — all reverted, `git status --porcelain` = 0).
- `requirements.md` (329 lines) and `architecture.md` (543 lines) written, then corrected against `code-mapper` recon and a `doc-extractor` canon cross-check.
- **Plan approved and persisted** into `architecture.md` §10, after `plan-validator` review. Every finding folded in before approval.
- **EditMode baseline: 133/133, `failed="0"` at `b83b62f`.** **PlayMode has no baseline** — never captured.

**Not started:** all code. No `.cs` file has been touched.

## Key files & locations

- **`Planning~/features/leading-miss-bar/architecture.md` §10** — **the approved plan. Start here.** §1–§9 carry the seams, the data contract, the test plan and the risks.
- `Planning~/features/leading-miss-bar/requirements.md` — F1–F18 with acceptance checks; §4.14 is the seven acceptance cases.
- `Planning~/design-docs/leading-miss-bar.md` — **LOCKED, immutable.** DR-1/2/3/5/6/8 scope this feature.
- `Runtime/Commands/VoxrCommandParser.cs` — all three code seams. Line refs verified on `b83b62f` and listed in the plan.
- `Tests~/Runtime/VoxrCommandParserTests.cs`, `Tests~/Runtime/VoxrEagerCommitTests.cs` — T1–T9.
- `.claude/verification-bindings.md` — what `compile-check` runs. Binding 1 only; `NativeBridge~/` is untouched.
- `Planning~/features/coverage-in-selection/ab-rig/` — `stage.sh`, `--bench` in `Program.cs`.

## Constraints & decisions

**Locked — do not relitigate.** Refuse-to-FIRE; uniform over element type; default-on; no public switch, no constructor param, **no test seam in shipped code**; 2.0.0 (item 4's). Fork C is **deferred to its own design topic** — do not implement it.

**Three rulings taken this session, all recorded in the docs:**
1. **The latch is ONE local and TWO sites**, not §5.1's two-locals-four-sites. Approved deliberately: verified that exactly four increment sites exist and no optional branch touches either counter, so `matchedRequired == 0 && missedRequired == 0` *is* "no required element reached yet" — making design §9's third trap structurally impossible. Rationale in architecture §1.1/§2.2.
2. **`CHANGELOG.md` is written on this branch**, over-riding canon §9's assignment to item 2 — ruled a scheduling call, since no DR pins it. Item 2 extends rather than originates.
3. **Canon §6.2 carries a factual error; DR-2 stands.** Its claim that the slot fork "only bites … five or more" is false — `VoxrCommandRecogniser.cs:928` rejects any winner with an unfilled required slot regardless of score. Recorded as an erratum; **no design branch**. T7 is reframed accordingly.

**Three things this must not get wrong** (each a near-miss in the design's own analysis): the flush suppression goes **above** the allocations and the `_tiedSiblingBuf` write; the eager condition is **not** redundant (nothing on the flush path can detect its absence); the latch must skip optionals.

**Environment traps:** CSharpier reformats every `.cs` edit and reverts manual de-indents — restructure, don't re-indent. `grep` honours `.gitignore`, so `Planning~/` is invisible to repo-wide greps. `Parse` can never be pinned at zero allocation and `GC.GetAllocatedBytesForCurrentThread` is inert here — use `Is.Not.AllocatingGCMemory()`, and always mutation-verify a green alloc test. `rm -rf` is blocked; `find … -delete` works.

**Verification:** delegate to `compile-check`, **both** EditMode and PlayMode (most parser tests are PlayMode). Never run builds on the main thread. On-device Quest verification is human-only — report as deferred, never claim it.

## Next steps

1. Read `architecture.md` §10 — the approved plan — and §1–§6 for the seams and test plan. **Step 0 is already done; start at Phase 1.**
2. Implement Phases 1–5 in order. Phases 1–3 are the three code seams; Phase 4 is nine tests plus two existing ones that must change; Phase 5 is the CHANGELOG.
3. Verify: `compile-check` (both suites), then the rig `--bench` A/B (`stage.sh b83b62f` vs `stage.sh WORKTREE`) for G-5. Mutation-verify T2 and T3 — T3's mutation must fail **T3 alone**.
4. Run **`review-cycle`** on the branch.
5. Commit, push, `gh pr create`, then **`review-pr`** at full profile — standing grant, no permission ask.
6. Present for **G2**: the human's explicit in-conversation ruling on the open PR. Put the §6.2 erratum in the PR body. **Never merge; never tag.**

## Open questions

- **Editor diagnostic goes quiet on a barred round** (requirements §8.4). DR-3's placement is above the diagnostic block, which itself allocates three times per round — so the two cannot both be had. Leaning accept; the cheap remedy (an allocation-free barred-round counter) belongs to item 3. Raise at G2.
- **Whether T7's slot-leading fixture should join the corpus** (architecture §9.1). Leaning no — it would invalidate the byte-identical baselines every prior arm rests on. Raise at G2.
- **`scoring.md` §1's published `cease fire` / "fire" → 0.50 row** loses its mechanism (the bar now refuses it by position before score applies). The number stays pinned by a new companion test; the *doc row* is unowned — canon DR-9 scopes item 2 to §7 D only. Flag it for item 2 rather than fixing it here.
