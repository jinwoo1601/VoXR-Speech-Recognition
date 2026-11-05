# Phase 5 — persisted plan (2026-11-05)

Scope: Phase 5 as the row reads it — from a PM-made detached worktree of `feat-host-repoint` at the final head, `vendor/` provisioned by Phase 2's copy, the PM runs Commands 1-2 by the script with the stale-XML and pointed-elsewhere mutations, Command 3 and Command 4; then `compile-check` runs Commands 1-4 in the one checkout; then the PM removes the worktree; the human's §9 Editor check last — taking on the recipe's arm64 block (:55, :56, :63) made tree-relative and its check-in before the worktree is cut, dropping nothing.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Planning~/verification-recipe.md` | modify (`doc-writer`, S1): :55, :56, :63, :65 only; 234 lines, CRLF | F7 from the worktree |
| `Planning~/features/host-repoint/plan-5.md` | create | process-docs |
| `Planning~/features/host-repoint/architecture.md` | `## Build plan` link line | process-docs |
| WT, with its `vendor/`, `build*/`, `.scratch/`, SCRIPT (:361 mutated, S7, byte-restored) and SAMPLE (canary, S9, not restored) | created (PM, S3-S9); removed at S12 | §9; F3-F7 |
| P; HL `lock`, `owner`, `rest/` | created (PM) / rewritten by every run (the script); never committed | evidence; register *Host lock* |
| hub RES, `NativeBridge~/build*/`, `vendor/overlaid/` (ignored) | rewritten by S11 | F8 |
| TG; the hub's script, binding, README; `.gitattributes`; requirements, design doc, ADRs, other plans; `Runtime/`, `Samples~/`, `Tests~/`; `.claude/**` | none | the brief |

## Contracts and interfaces

**Names.** HUB = `D:/Workspace/VoXR-Speech-Recognition`; WT = `D:/Workspace/voxr-host-repoint-p5`; TG = `D:/Workspace/VoXR TestGround`; PF = TG's `Packages/manifest.json` and `Packages/packages-lock.json`; HL = HUB `.scratch/host-lock`; P = `D:/Workspace/VoXR-Speech-Recognition/.scratch/host-repoint-p5`; RES = a tree's `.scratch/test-results`; RUN = `py "Planning~/host-run/host_run.py"`; SCRIPT and SAMPLE = WT's `Planning~/host-run/host_run.py` and `Samples~/CommandRecognition/TacticalSceneController.cs`. Git Bash; paths per the binding's Path form; CR counts by a `py` byte count, never `grep -c $'\r$'`.

**Run Rn from tree T, platforms A**, PM: `mkdir -p "P/Rn"`; TG `Temp/UnityLockfile` absent; in the background `cd "<T>" && (s=$(date +%s); RUN A; echo "exit=$?"; echo "bracket=$(( $(date +%s) - s ))s") > "P/Rn/run.txt" 2>&1`, waited on to its `bracket=` line; `cp` the RES files present to `P/Rn/`; `cmp` each PF with `P/pre`; TG `git status --short` `diff` empty with `P/t0`. Green: `exit=0`, `lock wait: 0s`, each platform line `failed` 0 `inconclusive` 0, `resolved: file:<T> (packages-lock.json)` after each, `host: at rest`, `host status: unchanged`, `result: green`.

**Recipe lines as they will read**, every other byte unchanged:
- :55 `"$CMAKE" -B "NativeBridge~/build" \`
- :56 `         -S "NativeBridge~" \` (indent as now)
- :63 `"$CMAKE" --build "NativeBridge~/build" --config Release -j 4`
- :65, the first of the two blank lines after the fence (:66 stays blank), becomes this sentence, words and code spans exact: Run the block from the root of the tree under test in Git Bash: its `-S` and `-B` paths are relative to it, so it reads that tree's `NativeBridge~/` and writes only that tree's `NativeBridge~/build/`.

**Steps:**
- S0 PM, after `phase-pickup`'s RECORD & COMMIT (plan-5 and the link line checked in): `mkdir -p P/pre`; `git status --short -- ':!memory'` > `P/g0`; TG `git status --short` > `P/t0` (the 4 human lines); `cp` PF to `P/pre/`; TG `Temp/UnityLockfile` absent; `git worktree list` the hub alone; hub RES's four files by `stat -c '%n %s %Y'` and `sha256sum` > `P/r0`; `find NativeBridge~/build NativeBridge~/build-windows NativeBridge~/build-confgen NativeBridge~/vendor/overlaid -printf '%p %s %T@\n' | sort` > `P/n0`.
- S1 `doc-writer`: the four recipe lines; nothing else.
- S2 PM: `git diff` of the recipe touches those four lines only; CRLF count equals line count, 234; check-in per `.claude/bindings/vc-git.md` `## vc` — `git add -- "Planning~/verification-recipe.md"`, `git commit -F <message file> -- "Planning~/verification-recipe.md"`, `git push`; `git status --short -- ':!memory'` equals `P/g0`. That commit is the final head.
- S3 PM, from HUB: `git worktree add --detach "D:/Workspace/voxr-host-repoint-p5" feat-host-repoint`; `git -C "<WT>" rev-parse HEAD` is S2's sha; SCRIPT's CRLF recorded; WT `Tests~` present, `Tests`, `Tests.meta` and RES absent.
- S4 PM, from WT: README :35's line run as plan-2 S3 runs it (CR-stripped, `bash -c`) prints `provisioned`; `ls -A NativeBridge~/vendor` lists the two pinned directories only.
- S5 PM: R1 from WT, `EditMode PlayMode`, green; WT `Tests~` present, `Tests`, `Tests.meta` absent, `git -C "<WT>" status --short` empty (F3); `P/R1` holds both XMLs and logs from WT's RES (F5).
- S6 PM, stale XML: `cp P/R1/editmode.xml` into WT's RES; `py -c "import os,stat,sys; os.chmod(sys.argv[1], stat.S_IREAD)"` on it; R2 from WT, `EditMode`: `exit=1`; `EditMode: -/-/-/- exit=<n>`; `EditMode: not green — no results XML written by this run` (`exit <n>` in its place where Unity exits non-zero, :267-270); `resolved: file:D:/Workspace/voxr-host-repoint-p5 (packages-lock.json)`; `host: at rest`; `result: not green`; the plant `cmp`-equal to `P/R1/editmode.xml`; then `stat.S_IWRITE` the same way, `rm` it, `test ! -e`.
- S7 PM, pointed elsewhere: (a) SCRIPT :361 reads `    new = f'"{PACKAGE}": "file:{TREE}"'`; `cp` SCRIPT to `P/pre/`; (b) per OQ1, a one-occurrence byte edit by `py -c`: it asserts that line occurs exactly once in SCRIPT, rewrites its `{TREE}` to `{HUB}`, CRLF kept; (c) R3 from WT, `EditMode`: `exit=1`; one `EditMode: not green —` line, any reason; `resolved: file:D:/Workspace/VoXR-Speech-Recognition (packages-lock.json)`; `host: at rest`; `result: not green` — those three lines and exit 1 the F4 evidence; (d) `cp P/pre/host_run.py` over SCRIPT, `cmp` 0.
- S8 PM, Command 3 from WT, in the background into `P/c3.txt`: `sed -n '52,63p' "Planning~/verification-recipe.md" | tr -d '\r' > P/arm64.sh` (the edited block), `bash P/arm64.sh`, then binding :8's command verbatim. Green: no build error, harness exit 0, 37/37; WT `NativeBridge~/build/libvosk-bridge.so` and `vendor/overlaid/continuous-default` present (absent at S4); S0's `find` from HUB `diff` empty with `P/n0` (F7).
- S9 PM, Command 4 from WT: writes the csproj by plan-1 S2's `sed` from WT's recipe; plan-1 S4a's `-getProperty:PkgRoot` prints WT; binding :9's command green; recipe :138-139 with `SP` set prints 7; per OQ1, a one-occurrence byte edit by `py -c`: it asserts the anchor :69-71 (`void OnEnable()`, `{`, `if (commandRecogniser == null) return;`) occurs exactly once in SAMPLE and inserts `        MutationCanaryMethodThatDoesNotExist();` before :71 as new line 71, CRLF kept; Command 4 red, `error CS0103` at SAMPLE under WT, `(71,9)`. No restore: the canary is removed with WT at S12.
- S10 PM: hub RES by S0's reads equals `P/r0` (F5); TG status and PF as the run form.
- S11 `compile-check`, one dispatch, Commands 1-4 in the one checkout in binding order: Commands 1-2 exit 0, both platforms `failed` 0 `inconclusive` 0, `resolved: file:D:/Workspace/VoXR-Speech-Recognition (packages-lock.json)` after each, `host: at rest`, `result: green`; Command 3, the arm64 half by the edited block, no build error, 37/37; Command 4 green.
- S12 PM, from HUB, no exit 3 or killed run outstanding: `git worktree remove --force "D:/Workspace/voxr-host-repoint-p5"` (in `permissions.ask`: the human approves); `git worktree list` the hub alone.
- S13 the human, asked by the PM through AskUserQuestion, TG `Temp/UnityLockfile` absent first: opens TG's Editor, reads VoXR's source in Package Manager as the local path `D:\Workspace\VoXR-Speech-Recognition` (the hub in any slash form), closes the Editor; the answer recorded verbatim.
- S14 PM: TG `Temp/UnityLockfile` absent; TG status `diff` with `P/t0`, PF `cmp` with `P/pre`, a difference reported, never repaired; `git status --short -- ':!memory'` equals `P/g0`; recipe CRLF 234/234. P kept to G2, never committed.

**Stops**, never worked around: an S0-S4 read off, `NOT provisioned`, or S2's diff beyond the four lines — the human. A run not as its step reads, a `cmp` or TG status miss, or S8-S10 off — stop with the evidence, no later run; the PM first clears S6's plant and restores SCRIPT from `P/pre/`. S8's harness exit 1 — the human's ruling, never a re-pin. An exit 3 naming `rest/`, or a killed run — the recipe's step 5, the human's; WT kept until then. An S7b or S9 `py -c` assertion failing — stop, the file unwritten. S11 red — stop with S2's recipe commit already pushed: it stands, and what follows, a fix or a revert, each a new commit, is the human's ruling.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Relative `-B`/`-S` | `R="$(pwd -W)"`, `$R/NativeBridge~/…` | binding :8's harness half is tree-relative already; no variable, no added line; both hang on the directory the block runs from, which the sentence names |
| The sentence on blank :65 | a new line before the fence | 234 lines kept: no later recipe cite moves, and S8's `52,63` reads the block |
| R2 and R3 `EditMode` alone, no filter | both platforms; `--filter` | an EditMode run deletes and reads one XML; a filter saves seconds and adds a `total 0` path |
| R2's evidence: `-/-/-/-` and the plant's `cmp` | the reason line alone | :300 reads counts only after a successful delete; the reason may read `exit <n>` |
| R3: any `not green —` reason, the `resolved:` line the evidence | `resolved file:…` required | :266-280 prints one reason, the first that applies — `exit <n>`, no results XML, or `total 0`, the hub's `Tests~` unrenamed exposing no tests — so none is required |
| Hub dirs by a `find` listing file | its hash | a `diff` names what moved |
| One `compile-check` dispatch | one per Command | binding order, one report; Commands 1-2 run first, so S13 follows them |
| Hub Command 4 without a canary | a hub canary | plan-3 S11; S9's red in WT is the same verbatim listing, so binding :9's mutation check is met by S9 |

## Acceptance criteria

1. [ ] Recipe :55, :56, :63, :65 only, CRLF 234/234, committed and pushed before S3; WT's HEAD that commit (S1-S3) (F7; the brief).
2. [ ] R1 green from WT; PF `cmp`-equal; WT `Tests~` back, no leftovers, WT status empty (S5) (F3).
3. [ ] R1's XMLs and logs in WT's RES; hub RES equal to `P/r0` (S5, S10) (F5).
4. [ ] R2 not green, the plant unread and unchanged; R3 not green, `resolved:` the hub; SCRIPT `cmp` 0 (S6, S7) (F4).
5. [ ] Command 3 37/37 from WT, `continuous-default` written in WT, hub listing equal to `P/n0` (S8) (F7).
6. [ ] Command 4 green from WT, `PkgRoot` WT, 7 samples, canary red at SAMPLE (71,9) (S9) (F6).
7. [ ] Commands 1-4 green in the one checkout by `compile-check` (S11) (F8; §9).
8. [ ] The human reads VoXR's source as the hub (S13) (§9 human-only; H7).
9. [ ] TG status equal to `P/t0` and PF `cmp`-equal to `P/pre` after each run and at S14; no TG hand write (§6).
10. [ ] WT created and removed by the PM; no agent git write; hub status equal to `P/g0` (S12, S14) (§9; CONSTITUTION).

## Open questions that block implementation

OQ1 (who makes S7b's and S9's WT edits) ruled by the human, 2026-11-05 (AskUserQuestion, "PM, this phase (Recommended)"): for Phase 5 only, the PM makes S7b's edit (SCRIPT :361 `{TREE}` → `{HUB}`) and S9's edit (the canary line inserted before SAMPLE :71) in WT as one-occurrence byte edits by `py -c`, each asserting exactly one occurrence and keeping CRLF; S7's SCRIPT restored by `cp` from `P/pre/` and `cmp`; S9's canary removed with WT; nothing committed.

## Blast radius

- TG `Library/` and `Temp/` re-imported to WT, the hub (R3) and back; the human's Editor open at S13.
- HL `lock`, `owner`, `rest/` rewritten by every WT run, by design (`host_run.py` :29); not an F5 check.
- HUB: a `.git/worktrees/` entry S3-S12; S2's commit pushed; S11 rewrites hub RES, `NativeBridge~/build*/`, `vendor/overlaid/`.
- Every later arm64 build reads the edited block: run elsewhere than a tree root it finds no `NativeBridge~`, never the hub's.
- architecture.md :37's Owns line and the row :88 do not name the recipe's *Building NativeBridge* block, so the as-built reconcile at G2 carries that ownership; no architecture edit now.

## Runtime claims

- a (S3, S5): `git worktree add --detach` succeeds with the branch checked out in HUB; the CRLF SCRIPT runs as in Phase 4.
- b (S4): README :35's line provisions WT as it did plan-2's stand-in.
- c (S6): a read-only file fails `os.remove` (:288) and Unity's XML write, keeping its bytes; `S_IWRITE` clears it.
- d (S7): with :361 mutated, :374 leaves the manifest on the hub, Unity resolves the hub, the restore and at-rest read pass.
- e (S8, S11): CMake 3.22 resolves relative `-S`/`-B` against its working directory; in WT it configures fresh, reads WT's `Runtime/Plugins/Android/arm64-v8a/` (`CMakeLists.txt` :60-62), writes only WT's `NativeBridge~/build/`; in HUB it accepts the existing cache, the resolved source being the cached one.
- f (S8): `sed -n '52,63p'` of the CRLF recipe, CR-stripped, is the block, run by `bash` as the recipe reads.
- g (S9): `-getProperty` prints WT; CS0103 names SAMPLE under WT.
- h (S12): `git worktree remove --force` removes WT holding ignored `vendor/`, `build*/`, `.scratch/`.
- i (S1): Edit keeps the recipe CRLF.

Hazards: from S0 to S14 no other Commands 1-2 or Unity run and no TG Editor open but S13's — one voids `lock wait: 0s`, the PF and status compares and the §9 reading; an exit-3 or killed WT run leaves TG naming WT, so WT is kept until the human's restore; from S6's plant to its removal and S7b to S7d, WT's RES or SCRIPT is mutated, so no other WT run between; HUB changes only by S1-S2, or S14's `P/g0` compare misreads.
