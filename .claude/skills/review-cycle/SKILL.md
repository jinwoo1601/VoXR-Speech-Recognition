---
name: review-cycle
description: "Run a multi-agent, adversarially verified review of a changeset range or feature branch. Use when any code review, review pass or simplify pass is due; it replaces /code-review and /simplify."
kind: orchestration
agents: [review-angle, finding-verifier, compile-check]
stops: [the ruling on a disputed finding, the ruling on a deferral, the ruling on a scope change, the ruling on a canon fork]
status: active
bindings: [vc, verification, review]
---

# review-cycle

## What this is

**One union review cycle: correctness and quality in the same fan-out.** Finder angles read a shared brief and hunt in parallel; adversarial verifiers kill the weak findings; CONFIRMED findings are fixed by default and listed at the next gate, and the human rules only on a disputed finding, a deferral, a scope change or a canon fork; each fix goes to its owner from a brief. Its depth follows the lane and the changeset's class, as `## Depth profiles` says.

> Finders find, verifiers kill, a writing agent fixes what is CONFIRMED by default, and the human rules on what is disputed. No finding reaches the human unverified; every fix is listed at the next gate.

Scope boundary: this cycle reviews a working tree or branch in the checkout. A vc pack may ship a sibling bound to the hosted review its system provides; a feature may run both, and they are not redundant.

## Bindings

- `vc` (required) — the base-revision reads the brief quotes under `## Scope` (the changed-file list, a file at a revision, a diff between two revisions), and the check-in procedure COMMIT follows. Absent → stop and say so before any brief is filed.
- `verification` (required) — the commands CHECK runs through `compile-check`. `none` → CHECK is recorded as `nothing to run`.
- `review` (optional) — the angles each profile runs and the hot paths, `core`'s `## review` section. Absent, empty or `TODO` → the defaults in `## Depth profiles`, and no hot paths declared, so EFF does not run; never a stop. A slot reading `default` means the table's default.
- Agents — `review-angle`, `finding-verifier`, or `compile-check` missing → stop before any dispatch and say so.

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
<!-- Interview: Which version-control system does this project use, and how are branches created, changes checked in, base revisions read, branches merged, the working tree recorded before an upgrade, and pull requests read, diffed and commented on? -->
- System: git; GitHub, remote `origin` `https://github.com/jinwoo1601/VoXR-Speech-Recognition.git`, public; `gh` on the Windows PATH (`C:\Program Files\GitHub CLI\gh.exe`), authenticated as `jinwoo1601`
- Main branch: `main`
- Auto-mode entries: included
- Branch procedure: a re-lock — a `design-<topic>` branch cut from the feature branch's head in the feature's own tree, declared on its STATUS line `re-lock of <feature-branch>`, merged back into that feature branch after G1 — is cut by `git switch -c design-<topic>` in the feature's tree and merged back by `git switch <feature-branch>`, then `git merge design-<topic>`, in that tree; branches are `design-<topic>`, `feat-<feature>`, `light-<topic>` (an issue's `light-<n>-<slug>`), `lab-<topic>`, which merges nothing, and the release prep `chore/release-X.Y.Z`; each is cut from an up-to-date `main` — `git fetch origin`, then on `main` `git merge --ff-only origin/main`, then `git switch -c <branch>` — and runs in the one checkout, since `trees` is none
- Check-in procedure: inline on the main thread — `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each one command, never a heredoc or a chain, never `git add -A` — a hub operation's `memory/` check-in by the base's `scripts/hub.py commit --message <file> --root <hub> <paths>`, which commits the named paths under `memory/` alone; then `git push -u origin <branch>` on a branch's first push and `git push` after; the commit sha is the receipt
- Base-revision reads: `git show <revision>:<repo-relative-path>`; `git diff --name-status <base>..<head>` for the changed-file list; `git diff <base>..<head> -- <path>`; `git log --oneline <base>..<head>`; `git status --short` for the working-tree read; `git branch --show-current` for the current branch; `git diff --numstat --no-renames <base>..<head>` for the per-file line-count read
- Merge procedure: a pull request against `main`, opened by `gh pr create --base main --title "<title>" --body-file <file>` after the push, merged by the human on GitHub behind the gate's stop; Claude never merges
- Snapshot procedure: `git stash create`, then, where it prints a sha, `git stash store -m "harness pre-upgrade <date>" <sha>` — the working tree and the index are left as they are; where it prints nothing, the snapshot is `git rev-parse HEAD`; confirmed by `git rev-parse --verify <sha>^{commit}`; restored by the human from that sha; `.claude/` and `CLAUDE.md` are tracked from `light-harness-adopt` on, so the record covers them — `.claude/settings.local.json` and `.scratch/` stay ignored and are not covered
- PR read: `gh pr view <n> --json number,title,body,url,state,isDraft,headRefName,headRefOid,baseRefName,files,labels` and `gh pr list --head "<branch>" --json number`; `gh` has no `baseRefOid` and no `closingIssuesReferences` field, and `--json` never goes without an explicit field list
- PR diff: `gh pr diff <n> > <file>`
- PR comment: `gh pr review <n> --comment --body-file <file>`, falling back to `gh pr comment <n> --body-file <file>`; never `--approve` or `--request-changes`, which GitHub refuses on one's own pull request; `gh pr edit` fails on the Projects-classic `projectCards` deprecation, so a PR body is patched by `gh api repos/jinwoo1601/VoXR-Speech-Recognition/pulls/<n> -X PATCH -F body=@<file>`
<!-- harness:binding:end vc -->

<!-- harness:binding:start verification .claude/bindings/unity.md -->
<!-- Interview: How is this Unity project compiled and tested from where the human works — which run, in which order, what does green look like for each, and how are only the checks that cover one change run — and may those commands also run in a writer tree, a parallel writer's own working tree, from that tree's root? Which shell does the Bash tool run commands in, which command prints the working directory, and in which form is a path written and two paths compared? One `Command N` line per command in run order; `Fallback` which route replaces which Commands when no Editor holds this tree open, which selector picks it and what its green is, or `none`; `Targeted` a command with `<target>` where the change's checks go, or `none`; `Tree-safe` answered `yes` or `no`, unstated read as `no`; a single `none` when nothing can be run. How a call names its tree, which route runs and how green is read: `.claude/references/unity/unity-cli-contract.md`. -->
- Run from: the project root `D:\Workspace\VoXR-Speech-Recognition`, the one checkout, which the host project `D:\Workspace\VoXR TestGround` (Unity 6000.4.7f1) consumes by `file:` path — this bare UPM package compiles and tests only through that host; the full procedure, the toolchain paths and the Samples~ csproj are in `Planning~/verification-recipe.md`
- Command 1: EditMode through the host, the Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent), `Tests~` renamed `Tests` and `"testables": ["com.jinwoo1601.voxr"]` in the host's `Packages/manifest.json`: `"/c/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe" -runTests -batchmode -projectPath "D:\Workspace\VoXR TestGround" -testPlatform EditMode -testResults "D:\Workspace\VoXR-Speech-Recognition\.scratch\test-results\editmode.xml" -logFile "D:\Workspace\VoXR-Speech-Recognition\.scratch\test-results\editmode.log"`, no `-quit` — expected green: the NUnit results XML this run wrote, `failed="0"` and `inconclusive="0"`
- Command 2: PlayMode, the same with `-testPlatform PlayMode` and `playmode.xml` and `playmode.log` (most parser and command tests are PlayMode, so it is never skipped); then the revert — `Tests` back to `Tests~`, the Unity-generated `.meta` orphans removed (root `Tests.meta` included), the host manifest restored — expected green: as Command 1, and `git status --short` showing no `Tests` leftovers
- Command 3: only when `NativeBridge~/` changed: the arm64 build with the Unity 6000.3.7f1 Android SDK's `cmake.exe` and `ninja.exe` per the recipe, then the desktop harness Windows-native in Git Bash — MSVC from Visual Studio 2022 Community through CMake's "Visual Studio 17 2022" generator, the same SDK's `cmake.exe` by absolute path, `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` — `cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/vosk-model-small-en-us-0.15 --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json`; on a bridge ABI change the rebuilt `.so` is committed at `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` — expected green: `libvosk-bridge.so` built with no errors, and the harness exits 0 with 16/16 transcripts matching; a transcript mismatch (exit 1) stops for the human's ruling, never a re-pin
- Command 4: only when `Runtime/` public API or any `Samples~/` source changed: the out-of-band Samples~ compile on native dotnet in Git Bash, the csproj generated per the recipe into `.scratch/samples-compile/` — `(cd .scratch/samples-compile && dotnet build SamplesCompile.csproj -v m -nologo)` — the only check that compiles `Samples~`; a missing Unity or host input fails the build naming its path — expected green: `Build succeeded` and `0 Error(s)`, the one CS0618 warning on `PermissionDeniedAndDontAskAgain` expected, and a mutation that breaks a sample turning it red
- Fallback: none — no Command goes through a live Editor: Commands 1 and 2 run batchmode with the host's Editor closed, Commands 3 and 4 run outside Unity
- Failure rule: a non-zero exit, a results XML missing or not written by this run, `failed` or `inconclusive` above 0, or a Samples~ green whose mutation check stayed green; on-device Quest checks — mic capture, `OnApplicationPause` delivery on doff or the system menu, any native-bridge change — are human-only, reported as deferred and never claimed
- Prerequisites: Unity 6000.4.7f1 with an active licence and the host imported at least once; for Command 3 the Unity 6000.3.7f1 Android NDK and SDK (its `cmake.exe` for both halves, `ninja.exe` for the arm64 half), Visual Studio 2022 Community with the x64 C++ tools, and `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` (the pinned `vosk-win64-0.3.45` and the small model); for Command 4 the .NET SDK 10 installed natively with `dotnet` on PATH, the Unity 6000.4.7f1 data folder and the host's `Library/ScriptAssemblies` (`UnityEngine.UI.dll`, `Unity.InputSystem.dll`)
- Targeted: Command 1 or 2 with `-testFilter "<target>"` added — a test name, fixture or namespace regex
- Tree-safe: no — the host consumes this checkout by path, and a batchmode run takes the host's Editor lock
- Shell: Git Bash (an MSYS2 bash on Windows), in which every Command runs directly, each Windows program reached on PATH (`dotnet`) or by absolute path; Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1` and a plain Git Bash does not, and every bound command behaves the same either way
- Working-dir read: `pwd -W`
- Path form: Windows form `D:\…`, compared case-insensitively with either separator; the MSYS forms `/d/…`, `/c/…` and `/tmp` appear only inside commands, as command words bash resolves, and are never written as a path; an argument a Windows program receives and a path inside a generated file are in drive-letter form (`C:/…` or `D:\…`); the working-dir read `pwd -W` already prints this form (`D:/…`)
<!-- harness:binding:end verification -->

<!-- harness:binding:start review .claude/bindings/core.md -->
<!-- Interview: Which review angles does each depth profile run in this project — slim, prose and full, `default` keeping the roster's own list — and which code paths are hot (per-frame, per-tick, per-event or otherwise cost-critical), where the efficiency angle EFF runs? `Hot paths` is repo-relative paths or globs, or a single `none`. -->
- Slim angles: default (LINE, GONE, XFILE)
- Prose angles: default (LINE, GONE, XFILE, CONV)
- Full angles: default (the composed roster set)
- Hot paths: `Runtime/VoxrSpeechRecogniser.cs`, `Runtime/Dsp/**`, `Runtime/Native/**`, `Runtime/Commands/**`, `NativeBridge~/src/**` — the per-frame poll, the per-chunk audio path, the bridge, and the per-utterance parser
<!-- harness:binding:end review -->

## Prerequisites

Confirm before proceeding:
- The build is complete and green (run `compile-check` first if unsure).
- The scope is expressible as a revision range or branch in the `vc` binding's terms.
- The feature's process docs are locatable (they supply the invariants).

## Depth profiles

Depth is chosen by lane and class, not by taste.

| Profile | When | Angles (default) | Materiality floor | CHECK |
|---|---|---|---|---|
| **full** | a design or feature branch — wherever G2 is at stake, feature-lite included — and any branch the lane read cannot place | the composed roster set | no | as written |
| **slim** | a light-lane changeset of class slim, or of no declared class | LINE, GONE, XFILE | yes | as written |
| **prose** | a light-lane changeset of class prose | LINE, GONE, XFILE, CONV | yes | once, at the head after the last fix; a fix round does not re-run it |
| **trivial** | a light-lane changeset of class trivial | none — no fan-out | — | as written |

- **The lane read.** Match the branch under review against the forms the `vc` binding's *Branch procedure* names (a `<…>` placeholder matches any text), as `review-pr` does: the light-lane form is the light lane; the design and feature forms select full; no match, or a slot naming no light-lane form, selects full, and the brief says which case applied. Never a stop.
- **The class read.** For a light-lane branch, the effective class the invoking skill's check named — its `bounds` verdict, handed in by `tackle-issue` REVIEW or `g2-lite` REVIEW — before the declared one; else the class the work declared at its start — in its launch or handoff brief, or its line in `memory/STATUS.md` — slim when none was declared.
- **The `review` binding** replaces a profile's default angle list where its slot names one. The profile's name, the angles it runs, and where they came from (default or binding) are recorded under the brief's `## Angles`.
- **EFF** is listed only where the `review` binding's *Hot paths* slot names paths; the brief quotes them. With none declared EFF is not dispatched, in any profile, and the brief says so.
- **Widening and narrowing.** The human may widen any profile (e.g. "review thoroughly" → full). Never narrow below the profile without the human saying so; a narrowing is recorded in the brief and said at RULE.

## Materiality floor — slim and prose

A finding becomes a **numbered finding** only if it asserts something in the merged result is *wrong*: code that misbehaves, a doc statement that is false, a claim the change does not deliver, or a removal that lost behaviour. That is the whole bar — severity is not the test.

Everything about what the change *could additionally* have done — stronger coverage, a tighter assertion, an adjacent improvement, style, naming — is a **Note**: one line, no evidence block, marked unverified; a Note is not verified and not ruled.

The floor does not apply to the full profile, where a coverage gap can be exactly what blocks G2.

## Workflow

1. **BRIEF** — build `.claude/references/core/review-brief.md` into `.scratch/review-<slug>-brief.md` before any fan-out: the revision range and the base-revision read idiom per the `vc` binding, stated literally (agents use exactly what the brief says); the changed-file list per the same binding; per-file intent from the persisted plan; under `## Load-bearing invariants`, first the entries of `.claude/references/core/prose-invariants.md`, copied verbatim and numbered under one lead-in line saying they are standing rules no check can read, so a finding against one is a breach of it in the change and never the observation that no check enforces it — whatever narrowing the human rules, since a narrowing chooses angles, not canon; a missing file is named there and again when the survivors are presented at RULE, and the cycle runs on — then the feature's own invariants; canon pointers from the feature docs; the project rules, as the template's Project rules says; under `## Angles`, the angles of the profile `## Depth profiles` chooses, recorded as it says; the full profile's composed roster set is `.claude/references/core/angle-roster.md`, plus, for each name in `.claude/harness.json`'s `packs`, `.claude/references/<pack>/angle-roster.md` where it exists (no manifest, or a composed pack with no angle file of its own, is core's roster alone). On the trivial profile, FAN OUT, DEDUP and VERIFY are skipped and RULE follows.
2. **FAN OUT** — one `review-angle` per angle listed under `## Angles`, each through the Agent tool without a `name`. Each call prompt = the brief's absolute path + that angle's roster entry pasted in full. Subagents inherit no context from this thread: every prompt must be self-contained. Read-only agents run in parallel — batch the dispatches in as few messages as possible.
3. **DEDUP** — main-thread reasoning, no agents: merge duplicate findings across angles, keep the strongest statement of each, drop anything the brief declared out of scope. On slim and prose, then apply `## Materiality floor — slim and prose`: each survivor is a numbered finding or a Note. The floor asks *wrong vs. could-be-better*, never *big vs. small*; never sort a real defect into Notes to shorten the review.
4. **VERIFY** — one `finding-verifier` per surviving numbered finding, its text passed verbatim plus the brief path; Notes are not verified. Batch ~5 at a time when findings exceed 10. No finding is presented to the human without a verifier verdict.
5. **RULE** — every CONFIRMED finding, a CONFIRMED-AS-CLARITY one included, goes to FIX without a ruling, and is listed, with its verdict and decisive evidence, at the next gate. The hard stop is only for a finding the PM disputes, one it proposes to defer, a fix that changes scope, or one a canon fork decides: present those ranked by severity then confidence, each with its verdict and decisive evidence, and each put to the human by `.claude/references/core/ruling-form.md`, its rule 3; the human rules fix / defer / reject per finding. Notes are listed after the findings, one line each, unverified, no ruling asked. On the trivial profile the human is shown the whole diff, by the `vc` binding's diff read, and reads it; anything the human raises is ruled as a finding. File the collation — the profile run, findings, verdicts, and per finding `fixed by default` or its ruling — beside the brief as `.scratch/review-<slug>-collation.md` (the delegation contract's filing rule: read-only agents' reports are not filed raw). Deferred items are recorded in `memory/STATUS.md`, or where the project's deferral convention says.
6. **FIX** — a fix goes to its owner, from a brief filed under `.scratch/`: `code-writer` through `implement` for code and pack files, `doc-writer` for prose docs and bindings, `architect` for the architecture doc; the reviewed code's shape is canon, and the review is not a license to refactor. Agents find and verify, and a writing agent edits from the finding, or from the ruling where one was asked.
7. **CHECK** — dispatch `compile-check` on the `verification` binding. On prose, it runs once, at the head after the last fix; a fix round does not re-run it.
8. **COMMIT** — per the `vc` binding's check-in procedure: inline on the main thread, or the agent the binding names; the findings summary is the message. This skill never commits by itself.

A handoff taken at a step boundary, as `handoff` `## When to hand off` says, after FAN OUT and before RULE files the findings the cycle holds, with any verdicts, as the collation `.scratch/review-<slug>-collation.md`, and its brief names that file and the review brief by path. After the last step there is no next step, and the objective is what follows — the invoking skill's next step where `tackle-issue` or `g2-lite` REVIEW invoked this cycle, otherwise the branch's gate skill — as `handoff` `## When to hand off` says of a skill's last step.
