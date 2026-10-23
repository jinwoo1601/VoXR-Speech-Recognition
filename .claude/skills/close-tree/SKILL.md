---
name: close-tree
description: "Close a merged branch's tree from the hub, keeping the branch, or stop a handed-off session. Use when a 'FINISHED' line or a 'STARTED' line naming a predecessor arrives, or on the human's word for a merged tree; never on 'HANDOFF' or 'BLOCKED'."
kind: execution
status: active
bindings: [vc, verification, trees, session-launch]
---

# close-tree

## What this is

The hub's half of a branch's close: the tree a finished branch was built in is removed from the hub once its checks pass, or kept with one line naming the check that failed. A report line is a trigger, never proof — a tree's close reads the `vc` binding's log read, the session list and the tree, never the report's detail, and `## A handed-off session`, which stops a handed-off session and removes nothing, takes from its line only the branch and the predecessor's session id. The removal rides on the gate ruling that authorised the merge — the G1, G2 or G2-lite ruling, or a light-lane close by class — and takes no ruling of its own (`adr-0024`).

> At a tree's close, nothing is stopped before the branch is merged, nothing is read clean while a session is at the tree, nothing is removed before the release wait, and a half-removal or a refused step is told, never retried.

## When it runs

- A `FINISHED - <branch>; lane: <lane>; <detail>` line arriving in this session → `## Workflow` for `<branch>`; the detail is never read.
- A `STARTED - <branch>; lane: <lane>; <skill>; predecessor <session id>` line arriving in this session → `## A handed-off session` for `<branch>` and `<session id>`; `## Workflow` does not run.
- The human's word on a tree `orient`'s TREES states merged and awaiting removal, proposed by its PROPOSE, or the human's word that the branch finished on a tree TREES states as "in progress, or finished with its report lost", for which PROPOSE offers this skill → `## Workflow` for that tree's branch.
- The human's word to close the tree again after a held or refused line for `<branch>` → `## Workflow` for `<branch>`.
- A `HANDOFF` or `BLOCKED` line, or a `STARTED` line naming no predecessor → nothing runs, and the line is the human's to read.

This skill never answers a report, never gives a session work, and writes nothing to `memory/`.

## Bindings

- `vc` (required) — the log read MERGED runs. Absent → stop and say so before anything runs.
- `verification` (optional) — the *Path form*, whose conversion a pack's `## The probe` may name for `<tree>`, which STOPPED's holder probes run. Absent → as that `## The probe` says; never a stop for that reason alone.
- `trees` (optional) — the Hub path and the Hub slot's idiom; the List read of TREE, MERGED and READ BACK; the Clean read of CLEAN; and the Remove procedure of REMOVE, with the held result it names where it names one, which READ BACK reads. Absent → `## Workflow` has no tree to remove: one line saying so, and it does not run; `## A handed-off session` runs with this session's project root as the tree, as it says.
- `session-launch` (optional) — the List command, the Stop command and the Confirm window, which STOPPED and `## A handed-off session` run, and the Release wait, which RELEASE waits; the Hub session name is not read, since this skill sends nothing; what the CLI behind those commands does is `.claude/references/core/claude-code-cli.md`'s. Absent, or the List command missing or `none` → STOPPED cannot run: R3's line naming the binding or the slot, and the tree kept. The Stop command or the Confirm window missing or `none` → R3's line naming the slot where a session is at the tree, and STOPPED holds where none is. The Release wait missing → RELEASE cannot run: R3's line naming the slot, and the tree kept.
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
- Context read: `python3 -c "import glob,json,os;p=glob.glob(os.path.expanduser('~/.claude/projects/*/'+os.environ['CLAUDE_CODE_SESSION_ID']+'.jsonl'))[0];t=[n for n in (u['input_tokens']+u['cache_creation_input_tokens']+u['cache_read_input_tokens'] for u in (e['message']['usage'] for e in map(json.loads,open(p,encoding='utf-8')) if e.get('type')=='assistant')) if n>0];print(t[-1])"` — run in Git Bash from the project root; prints the context of this session's last assistant turn that carries usage, from its transcript under Claude Code's projects directory in the user's home; lags at most one turn
<!-- harness:binding:end session-launch -->

## Workflow

`<branch>` is the branch the trigger names.

1. **TREE** — the List read, run in the hub by the trees contract's hub clause (`.claude/references/core/trees-contract.md` `## The hub clause`): the tree on `<branch>`, and its path. No tree on `<branch>`, or `<branch>` is the main branch → one line saying which; stop, and nothing else runs.
2. **MERGED** — the `vc` binding's log read from the main branch to `<branch>` lists no commit, or, failing that, the same read from the branch of any other tree the List read names, run where the `vc` binding runs a log read whose destination is that branch, lists none; a tree on no branch is passed over. Otherwise R3's line, naming the branches read; nothing is stopped.
3. **STOPPED** — first the shell returns to this session's project root. Then the List command: a session is at the tree when its working directory, as the list shows it, is the tree's path or lies under it, compared as the trees contract compares paths. None → the holder probes. Any session at the tree that shows no `<id>` for the Stop command → R3's line, and nothing is stopped. Otherwise each is ended by the Stop command with its `<id>`, its conversation kept; then the List command is re-read until no session is at the tree, within the `session-launch` binding's *Confirm window*, or R3's line naming what is still listed. Then the holder probes: for each name in `.claude/harness.json`'s `packs`, in the manifest's order, `.claude/references/<pack>/tree-holder.md` where it exists, its command under `## The probe` is run once from this session's project root, `<tree>` replaced by the tree's path as TREE read it, in the form, and by any conversion call, its `## The probe` names — that call run first from the same root — in double quotes, and its exit code and output are read by its `## Reading` as held or not held — anything else, a conversion that fails or prints no path, or no command readable under `## The probe`, is unreadable. Held → the held line, and unreadable → the held line's unreadable form; either way the tree is kept, nothing is read clean or removed, no later probe runs, and the close ends. Not held → the next probe. Every probe not held, none composed, or no manifest → on to CLEAN. Nothing in this skill ends a holder: a held tree waits for the human's word.
4. **CLEAN** — the Clean read, run in the tree by the Hub slot's idiom: empty, or R3's line with what it showed.
5. **RELEASE** — where the Remove procedure waits for the directory's release itself, or the `session-launch` binding's *Release wait* reads `none`, nothing; otherwise wait the *Release wait*: a stopped session can hold its tree's directory for a few seconds after it leaves the list. The slot missing → R3's line naming it, and the tree kept.
6. **REMOVE** — the Remove procedure for the tree's path, run in the hub by the trees contract's hub clause.
7. **READ BACK** — the List read, run in the hub, and the tree's path: still listed, unlisted with its directory gone, or unlisted with its directory present, as `## Outcomes` tells them apart. Still listed, where REMOVE's output carries the held result the `trees` binding's Remove procedure names → held: the held line, `<source>` `REMOVE` and `<what the read showed>` the procedure's message; still listed otherwise → the removal refused.

**A refused step.** A tool call of any step refused by permission or policy — TREE's or READ BACK's List read, MERGED's log read, STOPPED's List command, Stop command, a holder probe or its conversion call, the Clean read, RELEASE's wait, or a command of the Remove procedure — stops the close at that step: nothing after it runs, the refused call is never retried in another form, and the human hears the refused line. A refusal is no read's result: never a failed check, never held or unreadable, and never one of READ BACK's cases.

## Outcomes

A close that ends short of `removed` tells the human one line — R3's line, the held line or the refused line — formed as `references/outcomes.md` says, beside the table of every outcome, the tree and its sessions; read it before saying the line. A removed tree is said nothing.

## A handed-off session

`<branch>` and `<session id>` are the ones the `STARTED` line names; the line is a trigger and never proof. The tree is kept: nothing is removed, read clean or written, and MERGED is not read.

1. **TREE** — as `## Workflow`'s TREE.
2. **PREDECESSOR** — first the shell returns to this session's project root. Then the trees contract's `predecessor` verb on the tree's path and `<session id>`, which decides the case where the Path form is Windows form; under another Path form this step decides the same cases by that Path form itself, as the contract says. One act per output, `<path>` the tree's path, each line one line to the human, its template plain ASCII and `<cwd>`, `<path>` and `<session id>` printed as read:
   - `stop <id>` → the Stop command with that `<id>`, its conversation kept; then the case is re-read, by the verb where the contract delivers it, until `gone`, within the `session-launch` binding's *Confirm window*, or the line `<branch>: predecessor <session id> still listed at <path>`.
   - `gone` → nothing, silently.
   - `elsewhere <cwd>` → `<branch>: predecessor <session id> not stopped - listed at <cwd>, not at <path>`, and nothing is stopped.
   - `alone` → `<branch>: predecessor <session id> not stopped - no newer session at <path>`, and nothing is stopped.
   - `no-id` → `<branch>: predecessor <session id> not stopped - no stop id at <path>`, and nothing is stopped.

The verb exiting 1 or 2 → one line to the human naming it, and nothing is stopped. Nothing is kept between lines: a replayed `STARTED` reads `gone`.

Where `trees` reads absent, the tree is this session's project root: TREE reads nothing, `<path>` is the project root, and the steps run as written.

The `session-launch` binding absent, or a slot these steps need missing or `none` → one line to the human naming it, and nothing is stopped.

At a tree's close and for a handed-off session alike, this skill writes nothing to `memory/`, keeps the branch, sends nothing to any session, and says nothing on success.

A handoff the hub takes at a step boundary of `## Workflow` or of `## A handed-off session`, or where either stops, as `handoff` `## When to hand off` says, has as its objective the next step — after the last step or a stop, the hub's next act: the next report line or the human's next word. The brief targets the hub, so `handoff` launches nothing and shows the paste line (`handoff`'s `references/launch.md` condition 3); its brief, note and pointer are `handoff`'s, not this skill's. Where `trees` reads absent, `## A handed-off session` takes no rhythm handoff: `handoff` has no hub to target there.
