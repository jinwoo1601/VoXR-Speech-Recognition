---
name: orient
description: "Say where things stand in three lines, from the status file, the latest session note and the branch, and name the next skill. Use at the start of a session or on 'where are we'."
kind: execution
status: active
bindings: [vc, board, verification, trees, session-launch]
---

# orient

## What this is

Session start: read the project's state, say where things stand in three lines, name the next skill. Nothing here decides anything.

> Orientation reports state; it never changes it.

## Bindings

- `vc` (required) — the base-revision reads that give the current branch, and the *Branch procedure*, whose forms STATE reads the lane from. Absent → after MEMORY, stop and say so, proposing the binding interview or, before the project is scaffolded, the project `CLAUDE.md` *Bindings* section.
- `board` (optional) — where cross-session state lives beyond `memory/STATUS.md`. Absent → `memory/STATUS.md` is the state.
- `verification` (optional) — the *Path form*, whose conversion a pack's `## The probe` may name for `<tree>`, which PROPOSE's holder probes run as `close-tree` STOPPED forms it, read by `references/trees.md`. Absent → as that `## The probe` says; never a stop for that reason alone.
- `trees` (optional) — the Hub path, whose `memory/` MEMORY reads and whose branch BRANCH states; the List read and the Location slot, which TREES reads; the Hub slot's idiom by which those reads run in the hub; and the Remove procedure PROPOSE names. Absent → today's path; TREES is then skipped. Filled → MEMORY, BRANCH and PROPOSE also read the hub, and TREES runs.
- `session-launch` (optional) — the List command, read by `references/trees.md`. Absent, or the List command missing or `none` → TREES and PROPOSE as today.
- No agent: this skill dispatches nothing.

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

<!-- harness:binding:start board .claude/bindings/core.md -->
<!-- Interview: Does this project track cross-session state on a board — which, and how is it read — or is memory/STATUS.md the only state? And at most how many characters may one memory/STATUS.md line carry, or is there no cap? And at most how many KB may memory/STATUS.md hold, or is there no budget? -->
- Mechanism: none — memory/STATUS.md
- STATUS line cap: 200
- STATUS budget: 10 KB
<!-- harness:binding:end board -->

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

<!-- harness:binding:start trees .claude/bindings/vc-git.md -->
<!-- Interview: Does this project run branches in their own working trees — where the main checkout and the trees live and how trees are named, how a branch and its tree are cut, listed, read clean, caught up with the main branch and removed, how writer branches are named, and whether writer trees are pooled — or a single `none` where every branch runs in the one checkout? -->
- none
<!-- none: every branch runs in the one checkout — the host project `VoXR TestGround` consumes this checkout by `file:` path, so a branch in another tree would never be the one tested -->
<!-- harness:binding:end trees -->

<!-- harness:binding:start session-launch .claude/bindings/core.md -->
<!-- Interview: Does the PM launch sessions itself — by which command a session is started in a given tree for a given branch with a one-line prompt, keeping its transcript and in the permission mode it runs in; under which name the hub session is opened and receives the launched sessions' reports; and by which commands the running sessions are listed and a finished one is stopped; how long a launch or a stop is waited on in that list before it is read as failed; and how long a stopped session may hold its tree's directory; past how many tokens of context a session hands off inside a phase, and by which command it reads its own context — or a single `none` where the human opens every session? -->
- Launch command: `cd "<tree>" && CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1 claude --bg --remote-control <branch> -n <branch> --permission-mode auto "<prompt>"`, run in Git Bash from the launching session, `<tree>` the project root in the Path form (`trees` is none) — the CLI's behaviour: `.claude/references/core/claude-code-cli.md`
- Hub session name: `voxr-hub` — this project's own hub session, which the human opens in the project root with `claude --remote-control voxr-hub -n voxr-hub`
- List command: `claude agents --json` — one object per running session on this machine, each with its `cwd`; see the reference for its shape
- Stop command: `claude stop <id>` — the `id` the list command shows
- Confirm window: 30 seconds (provisional → the tuning pass)
- Release wait: none — `trees` is none, so no tree is removed
- Context bound: about 200k tokens (provisional → the tuning pass)
- Context read: `py -c "import glob,json,os;p=glob.glob(os.path.expanduser('~/.claude/projects/*/'+os.environ['CLAUDE_CODE_SESSION_ID']+'.jsonl'))[0];t=[n for n in (u['input_tokens']+u['cache_creation_input_tokens']+u['cache_read_input_tokens'] for u in (e['message']['usage'] for e in map(json.loads,open(p,encoding='utf-8')) if e.get('type')=='assistant')) if n>0];print(t[-1])"` — run in Git Bash from the project root; prints the context of this session's last assistant turn that carries usage, from its transcript under Claude Code's projects directory in the user's home; lags at most one turn
<!-- harness:binding:end session-launch -->

## Workflow

1. **MEMORY** — read `memory/STATUS.md`'s `## Current` section only — from its heading to the next `## ` heading, at the line numbers the base's `scripts/harness.py outline` prints for that file, the base being the `base` path in `.claude/harness.json`, relative to the project root, which MEMORY reads itself; or, where no base is found (no manifest), by its heading in `memory/STATUS.md`; where STATUS has no `## Current` heading, say so and read it whole — and the latest `memory/<YYYY-MM-DD>.md`. No `memory/` → say so and propose `g0-open`. Where `trees` is filled, both are the hub's, read by absolute path under the Hub path, whichever tree the session opened in.
2. **MANIFEST** — read the project's manifest and report the doctor run the SessionStart hook put into this session's context at its start, resume or clear — and only where the context holds none, run the base's `scripts/harness.py doctor --root <project>` — where they exist, reporting its failures first; where they do not, say "no manifest — skipping" and continue.
3. **BRANCH** — the current branch per the `vc` binding's base-revision reads, cross-checked against its STATUS line. A mismatch is stated, not resolved. Where `trees` is filled, the same read, run in the hub by the Hub slot's idiom, also gives the hub's branch, and a hub off the main branch is a mismatch too.
4. **TREES** — where `trees` is filled, as the contract reads it: as `references/trees.md` says; otherwise skipped.
5. **STATE** — three lines: the branch and its lane, the stage, the next action. The lane, `design`, `feature`, `light` or `lab`, is the one the brief this session started from names, or else the one the branch gives, matched against the forms the `vc` binding's *Branch procedure* names (a `<…>` placeholder matches any text): its design, feature, light-lane or lab form, the lab form `lab-<topic>` where the binding names none; a branch with none of them, the main branch among them, is stated without a lane.
6. **PROPOSE** — the skill that fits the next action (`g0-open`, `g1-lock`, `implement`, `review-cycle`, `g2-accept`, `handoff`), then stop. The human decides. Where TREES ran, tree removals are proposed as `references/trees.md` `## PROPOSE` says.
