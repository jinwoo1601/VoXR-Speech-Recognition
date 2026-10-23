---
name: review-pr
description: "Review an already-open pull request only, posting the verdict as a PR review; a changeset with no PR goes to review-cycle. Use on 'review PR 42' or 'is that PR safe to merge'."
kind: orchestration
agents: [review-angle, finding-verifier, compile-check]
stops: [the word before anything is posted on the PR, the ruling on the posted review before any feature-lane fix, the merge or approval or close of the PR]
status: active
bindings: [vc, issues, verification]
removal-date: null
---

# Review PR — Design & Build Workflow

## What this is

**Independent review of an already-open PR, written back onto the PR.** The PR's own title, body, and ticked validation boxes are treated as *claims to audit*, never as context to trust. Finder agents inherit nothing from the session that wrote the code — that is the whole source of the independence.

> The PR says what it did; the reviewer establishes what it did. Findings land on the PR, and only the human decides what happens to them.

This is the PR-bound sibling of `review-cycle`. Use `review-cycle` on a working tree or branch before a PR exists; use this once a PR is open. On a feature that ran both, the two are not redundant: `review-cycle` reviewed the tree mid-build, this one reviews the final diff the human will actually merge.

## Bindings

- `vc` (required) — the *PR read*, *PR diff* and *PR comment* slots are how a pull request is read, diffed and commented on; any of them `none`, `TODO` or missing — a project with no pull-request host, or one whose binding predates the slots — has no PR to review → stop and report, pointing at `review-cycle` instead. The *Base-revision reads* slot supplies the base-content idiom step 2 states literally in the brief. The *Branch procedure* slot names the project's branch forms, and step 1 reads the lane from it (see *Depth profiles*). Absent → stop and say so before any brief is filed.
- `issues` (optional) — the *Read* slot, by which the linked issue is read, and the *Link* slot, whose form the PR body is parsed for. Absent, empty, `TODO`, or its *Tracker* reading `none` → no linked issue is read, never a stop.
- `verification` (optional for this skill) — the commands CHECK runs through `compile-check` on the prose and trivial profiles. `none` → CHECK is recorded as `nothing to run`. Absent → CHECK is not run and the posted review says so, never a stop.
- Agents — `review-angle` or `finding-verifier` missing → stop before any dispatch and say so. `compile-check` missing → on the prose and trivial profiles, CHECK is not run and the posted review says so.

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

<!-- harness:binding:start issues .claude/bindings/light-lane.md -->
<!-- Interview: Where does this project track the issues light-lane work takes up — which tracker, how one issue and its thread are read and the open issues listed with their labels, how findings and questions are posted where an issue lives, and how a changeset names the issue it closes — or does the human name each task, with no tracker? -->
- Tracker: GitHub Issues on `jinwoo1601/VoXR-Speech-Recognition`; an issue labelled `blocked`, `wontfix`, `question`, `needs-input` or `duplicate`, or with a `light-<n>-*` branch already on `origin`, is not picked
- Read: `gh issue view <n> --json title,body,labels,assignees,comments`; `gh issue list --state open --limit 100 --json number,title,createdAt,labels,assignees,url`
- Comment: `gh issue comment <n> --body-file <file>`; an issue left waiting on the human also gets the `needs-input` label by `gh api repos/jinwoo1601/VoXR-Speech-Recognition/issues/<n>/labels -f "labels[]=needs-input"`
- Link: `Closes #<n>` in the pull request body
<!-- harness:binding:end issues -->

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

## When it applies — and prerequisites

**Trigger:** an open PR needs review — invoked directly on a PR number, or as the bound lane step below.

Confirm before proceeding:
- The PR exists and is open, and its head commit is pushed.
- `git fetch origin` has run, so base and head are resolvable locally.
- You are not about to review a PR whose branch has moved since you last looked — pin `headRefOid` in the brief and cite it in the posted review.

## Depth profiles

Depth is chosen by lane and class, not by taste. State which profile you ran in the posted review.

| Profile | Lane | Angles | CHECK |
|---|---|---|---|
| **slim** | Issue lane — a head branch in the form the `vc` binding's *Branch procedure* names for light-lane work, of class slim or of no declared class; gate-exempt | `CLAIM` + `LINE`, `GONE` | none — `g2-lite`'s preflight audits run evidence |
| **prose** | Issue lane, of class prose | `CLAIM` + `LINE`, `GONE`, `XFILE`, `CONV` | once, at the pinned head |
| **trivial** | Issue lane, of class trivial | none — no fan-out; the posted review names the profile and the human reads the PR diff | once, at the pinned head |
| **full** | Feature/design lane — a head branch in the design or feature form that slot names — any PR where G2 is at stake, and any head the lane read cannot place | `CLAIM` + the entire composed roster set | none — G2's preflight audits run evidence |

**The lane read.** Match the PR's `headRefName` against the forms the `vc` binding's *Branch procedure* names, a `<…>` placeholder matching any text. The light-lane form is the issue lane, its profile chosen by the class read; the design and feature forms select full. A head that matches no named form, or a slot that names no light-lane form, runs full, and the posted review's profile line says which case applied and why. This is never a stop.

**The class read.** For an issue-lane head, the effective class the invoking skill's check named — its `bounds` verdict, handed in by `tackle-issue` REVIEW or `g2-lite` REVIEW — before the declared one; else the class the work declared at its start — in its launch or handoff brief, or its line in `memory/STATUS.md` — slim when none was declared. The posted review's profile line names the class and where it was read.

`CLAIM` is defined in `references/pr-angles.md` — it is PR-specific and exists in no other cycle. The rest are pasted verbatim from the composed roster set — `.claude/references/core/angle-roster.md`, plus, for each name in `.claude/harness.json`'s `packs`, `.claude/references/<pack>/angle-roster.md` where it exists; with no manifest, it is core's roster alone. If core's roster is not locatable, stop and report rather than improvising angle definitions. A composed pack with no roster of its own is not a stop: the posted review names every such pack.

Depth may be widened on request (`review the PR thoroughly` → full, whatever the lane). Never narrow below the chosen profile without the human saying so, and if you do, say which angles you dropped in the posted review — a silently narrowed review reads as a clean one.

**Why slim is three angles.** On a typical issue diff — one or two files, one behavior — `XFILE` and `GAP` mostly re-find what `LINE` and `GONE` already have (measured on one issue-lane PR: a single finding was reported independently by four of the six angles then run), while `GAP` reliably converts a small fix into a long list of could-assert-more remarks. The issue lane is gate-exempt and its PRs are small; it buys its confidence from `CLAIM` auditing the author's own claims plus two correctness angles, not from breadth. Add `XFILE` back when the diff crosses a contract boundary — a call signature, a serialized field name, an event payload, an enum ordinal, or a string key read on both sides of the change — and name the addition in the posted review.

## Materiality floor — slim and prose

A finding becomes a **numbered finding** only if it asserts something in the merged result is *wrong*: code that misbehaves, a doc or API-reference statement that is false, a PR claim the diff does not deliver, or a removal that lost behavior. That is the whole bar — severity is not the test, so a false row in an API reference is numbered even though it is minor.

Everything about what the change *could additionally* have done — coverage that could be stronger, an assertion that could be tighter, an adjacent improvement, style, naming — is **not** a numbered finding. It goes in a flat `### Notes` list at the end of the posted review: one line each, no evidence block, explicitly marked unverified.

This floor is what keeps the issue lane proportionate. It does not apply to the full profile, where a coverage gap can be exactly what blocks G2.

## Workflow

1. **RESOLVE** — pin the PR by the `vc` binding's *PR read* slot — its number, title, body, url, state, draft flag, `headRefName`, `headRefOid`, `baseRefName`, files and labels — then pin the base:

   ```bash
   git fetch origin
   git merge-base "origin/<baseRefName>" "<headRefOid>"    # the base SHA agents compare against
   ```

   Compute the base with `git merge-base` as above (it is the merge-base anyway, which is what you want: immune to an advanced `main`), and derive the linked issue by parsing the PR body for the form the `issues` binding's *Link* slot names; `issues` absent, empty, `TODO`, or its *Tracker* reading `none` → no linked issue is read, never a stop.

   No number given → infer from the current branch, by the *PR read* slot's lookup of the PR for a branch, the branch per the `vc` binding's current-branch read; zero or multiple matches → ask, don't guess. Pick the depth profile by the lane read — `headRefName` against the forms the `vc` binding's *Branch procedure* names — and the class read, as *Depth profiles* says. A closed or merged PR is not reviewable here — its merge-base collapses onto its head and the diff comes back empty; stop and say so rather than posting a review of nothing.
2. **BRIEF** — build the brief from `assets/pr-review-brief-template.md` into the session scratchpad (absolute path — NOT the project tree, so the working tree stays clean). Save the full diff beside it, by the `vc` binding's *PR diff* slot, as `<scratchpad>/pr-<n>.diff`, and point the brief at it by absolute path. Base content idiom for agents: `git show <base-sha>:<repo-relative-path>`, using the merge-base SHA from step 1 — state the resolved idiom literally in the brief; agents use exactly what the brief says. Transcribe the PR's claims **verbatim** into the claims section, including every ticked validation box. The brief's `## Load-bearing invariants` opens, in both lanes and whatever the depth profile, with the entries of `.claude/references/core/prose-invariants.md`, copied verbatim and numbered under one lead-in line saying they are standing rules no check can read — so a finding against one is a breach of it in the change, never the observation that no check enforces it; a missing file is named in the brief and in the posted review, and the review runs on. The lane's own invariants follow the list: the feature's process docs supply them, with the canon pointers, where the lane has them; the issue lane puts the linked issue's reported problem after the list, read from the issue itself, by the `issues` binding's *Read* slot, rather than from the PR's summary of it — where no linked issue is read, the brief says so. On the trivial profile, FAN OUT, DEDUP and VERIFY are skipped and POST follows.
3. **FAN OUT** — one `review-angle` agent per angle in the chosen profile. Each call prompt = the brief's ABSOLUTE path + that angle's entry pasted in full. Subagents inherit no context from this thread: every prompt must be self-contained. Batch the spawns in as few messages as possible.
4. **DEDUP, THEN SORT** — main-thread reasoning, no agents: merge duplicates across angles, keep the strongest statement of each, drop anything the brief declared out of scope. **If this session wrote the code under review, that is exactly when not to soften a finding** — dedup edits for overlap, never for comfort. On the slim and prose profiles, now apply the materiality floor above: each survivor is either a numbered finding or a Note. Sorting a real defect down into Notes to shorten the review is the one failure this step must not produce — the floor asks *wrong vs. could-be-better*, never *big vs. small*.
5. **VERIFY** — one `finding-verifier` per **numbered** finding, its text passed verbatim plus the brief path. Notes are not verified (that is what earns them their one line and their unverified label). Batch ~5 at a time when numbered findings exceed 10. No numbered finding is posted or presented without a verifier verdict.
6. **POST** — first, on the prose and trivial profiles, CHECK: dispatch `compile-check` on the `verification` binding once, in the tree that holds the PR's head branch, only where that tree's head is the pinned `headRefOid` (`git rev-parse HEAD` there); where no such tree is at hand, CHECK is not run. Then **stop**: show the human the review body — the file at `<scratchpad path>`, carrying what this step lists below — in full, and post nothing before the human's word to post it; silence is not that word. On it, write the review onto the PR by the `vc` binding's *PR comment* slot, as a comment-only review whose body is the file at `<scratchpad path>` — never an approval or a request for changes; if the review is rejected for any reason, fall back to the slot's plain comment with the same body file. The posted body carries: depth profile run and why — the lane matched, or which no-match case sent it to full (and any dropped or added angles), each composed pack with no angle roster of its own, a missing standing list of prose-held invariants, head SHA reviewed, on the prose and trivial profiles the CHECK result, or `not run` and why, numbered findings ranked severity then confidence with verdict and decisive evidence each, refuted findings named in one line so the human can see what was considered and killed, the `### Notes` list (slim and prose profiles), and an explicit statement of what was **not** verifiable from here (device checks, host-project test runs). Zero findings is a valid, good review — post it and say so plainly.

   **Slim-profile length budget:** ~150 words per numbered finding — the claim, the decisive `file:line` evidence, the fix, and nothing else. One code block only where the code is the argument. Drop the per-finding provenance ("reported by four of six angles"), the mutation-test narratives, and the recap of what verified clean; a two-sentence header line covers the last of those. If a finding genuinely needs more than that to be believed, it is a blocker and the extra words are earned — otherwise the budget holds. The full profile has no budget: at G2 the human is weighing evidence, not scanning.
7. **RULE, THEN ACT** — relay the posted review in-conversation and hand the ruling to the human. Fixes are governed by the lane:
   - **Feature/design lane:** nothing is fixed without a ruling. The posted review is evidence the human weighs at G2; `g2-accept` owns what follows.
   - **Issue lane:** a CONFIRMED **blocker** is the run's own unfinished work — fix it on the same branch, push to the same PR, and, the line shown to the human and on their word, post a one-line follow-up comment, by the *PR comment* slot's plain comment, saying which finding the push addresses. Everything below blocker stays on the PR for the human, unruled and unfixed.
   - Either way: **never merge, never approve, never close the PR.**
