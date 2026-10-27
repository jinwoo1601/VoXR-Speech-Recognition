---
name: g2-accept
description: "Take a feature through G2 acceptance on the human's ruling, then its product docs, merge and close-out. Use on 'accept the feature' or 'ready for G2'."
kind: orchestration
agents: [gate-preflight, doc-writer, compile-check, architect-reconcile]
stops: [the G2 ruling before record or product docs or merge, a conflict or red re-verification at catch-up]
status: active
bindings: [vc, verification, product-docs, board, process-docs, trees, session-launch]
---

# g2-accept

## What this is

**G2 is the human's ruling; everything here prepares evidence for it or executes its consequences.** Preflight audits the preconditions, the human rules, and only then are the ruling recorded, product docs written, the branch merged, and the state closed out.

> Nothing crosses G2 on the assistant's judgment. Preflight prepares, the human rules, and only then do product docs and merge exist.

## Bindings

- `vc` (required) — what STAGE needs staged for the ruling form, the read commands the preflight runs, the *Branch procedure*, whose lab form Prerequisites refuses, and MERGE's merge procedure. Absent → stop and say so before any brief is filed.
- `verification` (required) — the commands whose run evidence the preflight's item 2 looks for, quoted into the preflight brief; `none` is a filled value (item 2 reports `nothing to run`). On the tree path, also the commands CATCH-UP's `compile-check` runs in the branch's tree, where `none` has CATCH-UP record `nothing to run`. Absent → stop and say so before the brief is filed.
- `process-docs` (required) — the docs `gate-preflight` audits and RECORD writes to.
- `product-docs` (optional) — the location and the reconciling agent. `none bound` → PRODUCT DOCS is reported as not applicable. A location with a named reconciling agent → that agent is dispatched from a decision-style brief only if it exists in the composed set, else the step is reported, never improvised.
- `board` (optional) — default `none — memory/STATUS.md`. A bound board → CLOSE OUT advances or retires its row.
- `trees` (optional) — the Hub path ROOT compares with this session's root and the `scripts/hub.py lock` commands take as `--root`; the List read that confirms the branch's tree; the Catch-up procedure; and the Clean read and the Hub slot's idiom by which HUB READ and MERGE run in the hub. Absent → today's path. Filled → the steps run as `references/tree-path.md` orders them.
- `session-launch` (optional) — the Hub session name, to which the tree path's REMOVE sends `FINISHED`. Absent, or the Hub session name missing or `none` → REMOVE as today and no report sent.
- Agents — `gate-preflight` missing → stop before any dispatch and say so. `doc-writer` missing → RECORD is reported as not done; the ruling stands in the conversation and the session note. `compile-check` missing → where CATCH-UP brings the main branch in, stop before the merge and say so. `architect-reconcile` missing → the architecture doc's `status:` move, and a lite feature's whole RECORD, are reported as not done, the ruling standing as said of `doc-writer`.

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
- Command 3: only when `NativeBridge~/` or a source `confgen` links (`NativeBridge~/harness/confgen/confgen.csproj`) changed: the arm64 build with the Unity 6000.3.7f1 Android SDK's `cmake.exe` and `ninja.exe` per the recipe, then the desktop harness Windows-native in Git Bash — MSVC from Visual Studio 2022 Community through CMake's "Visual Studio 17 2022" generator, the same SDK's `cmake.exe` by absolute path, `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` — `cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json`; on a bridge ABI change the rebuilt `.so` is committed at `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` — expected green: `libvosk-bridge.so` built with no errors, and the harness exits 0 with 37/37 transcripts matching; a transcript mismatch (exit 1) stops for the human's ruling, never a re-pin
- Command 4: only when `Runtime/` public API or any `Samples~/` source changed: the out-of-band Samples~ compile on native dotnet in Git Bash, the csproj generated per the recipe into `.scratch/samples-compile/` — `(cd .scratch/samples-compile && dotnet build SamplesCompile.csproj -v m -nologo)` — the only check that compiles `Samples~`; a missing Unity or host input fails the build naming its path — expected green: `Build succeeded` and `0 Error(s)`, the one CS0618 warning on `PermissionDeniedAndDontAskAgain` expected, and a mutation that breaks a sample turning it red
- Fallback: none — no Command goes through a live Editor: Commands 1 and 2 run batchmode with the host's Editor closed, Commands 3 and 4 run outside Unity
- Failure rule: a non-zero exit, a results XML missing or not written by this run, `failed` or `inconclusive` above 0, or a Samples~ green whose mutation check stayed green; on-device Quest checks — mic capture, `OnApplicationPause` delivery on doff or the system menu, any native-bridge change — are human-only, reported as deferred and never claimed
- Prerequisites: Unity 6000.4.7f1 with an active licence and the host imported at least once; for Command 3 the Unity 6000.3.7f1 Android NDK and SDK (its `cmake.exe` for both halves, `ninja.exe` for the arm64 half), Visual Studio 2022 Community with the x64 C++ tools, and `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` (the pinned `vosk-win64-0.3.45` and the small model), and the .NET SDK 10 with dotnet on PATH (confgen); for Command 4 the .NET SDK 10 installed natively with `dotnet` on PATH, the Unity 6000.4.7f1 data folder and the host's `Library/ScriptAssemblies` (`UnityEngine.UI.dll`, `Unity.InputSystem.dll`)
- Targeted: Command 1 or 2 with `-testFilter "<target>"` added — a test name, fixture or namespace regex
- Tree-safe: no — the host consumes this checkout by path, and a batchmode run takes the host's Editor lock
- Shell: Git Bash (an MSYS2 bash on Windows), in which every Command runs directly, each Windows program reached on PATH (`dotnet`) or by absolute path; Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1` and a plain Git Bash does not, and every bound command behaves the same either way
- Working-dir read: `pwd -W`
- Path form: Windows form `D:\…`, compared case-insensitively with either separator; the MSYS forms `/d/…`, `/c/…` and `/tmp` appear only inside commands, as command words bash resolves, and are never written as a path; an argument a Windows program receives and a path inside a generated file are in drive-letter form (`C:/…` or `D:\…`); the working-dir read `pwd -W` already prints this form (`D:/…`)
<!-- harness:binding:end verification -->

<!-- harness:binding:start product-docs .claude/bindings/codex.md -->
<!-- Interview: Where do this project's product docs live, is the layout a wiki vault or a Documentation~/CHANGELOG surface, is there a schema file for them, and which agent reconciles them after G2 — or none bound? -->
- Location: `Documentation~/` (the hub `Documentation~/index.md`, the per-type API reference under `Documentation~/api/`), plus `CHANGELOG.md` and `KNOWN_LIMITATIONS.md` at the root
- Layout: docs+changelog
- Schema file: none
- Reconciling agent: codex-reconcile
<!-- harness:binding:end product-docs -->

<!-- harness:binding:start board .claude/bindings/core.md -->
<!-- Interview: Does this project track cross-session state on a board — which, and how is it read — or is memory/STATUS.md the only state? And at most how many characters may one memory/STATUS.md line carry, or is there no cap? And at most how many KB may memory/STATUS.md hold, or is there no budget? -->
- Mechanism: none — memory/STATUS.md
- STATUS line cap: 200
- STATUS budget: 10 KB
<!-- harness:binding:end board -->

<!-- harness:binding:start process-docs .claude/bindings/core.md -->
<!-- Interview: Where do this project's design docs and feature docs live, what are the files called, and at which path is a phase's plan file written, and at most how many KB may an architecture doc's body, a plan file and a brief each hold, at which path pattern? -->
- Design docs: `Planning~/design-docs/<topic>.md`, one file per design topic, its decision records beside it per design-track's ADR convention; the root-level `Planning~/*.md` vN docs are pre-harness history, read-only
- Feature docs: `Planning~/features/<feature>/requirements.md` and `Planning~/features/<feature>/architecture.md`, one directory per feature
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
- Architecture-doc body budget: 40 KB over `Planning~/features/<feature>/architecture.md`; the closed features' oversized docs, all written before the harness, are ruled in `memory/doctor-rulings.md`
- Plan-file budget: 10 KB over `Planning~/features/<feature>/plan-*.md`
- Brief budget: 15 KB over `.scratch/**/*brief*.md`
<!-- harness:binding:end process-docs -->

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

## Prerequisites

Confirm before proceeding:
- The review cycle is done and its fixes are committed.
- The checkout is on the feature branch per the `vc` binding's base-revision reads.
- Every command the `verification` binding names is green at the head presented (`compile-check`'s last PASS).
- The branch is not in the lab form: the lab form the `vc` binding's *Branch procedure* names, or `lab-<topic>` where it names none (a `<…>` placeholder matches any text). A lab branch merges nothing → stop and say so before any brief is filed.

## Workflow

Where `trees` is filled, as the contract reads it, these steps run as `references/tree-path.md` orders them; otherwise they run as written.

1. **PREFLIGHT** — file `.scratch/g2-accept-<feature>-preflight-brief.md` from the common fields of `.claude/references/core/brief-fields.md` (the `vc`, `board`, `verification`, and `trees` bindings quoted; the feature's process docs by absolute path per `process-docs`), then dispatch `gate-preflight` through the Agent tool without a `name`. The skill applies the test of `.claude/references/core/feature-lite.md` `## The mark` here; for a lite feature the brief says `lite`, gives the backlog's path and the feature's name, quotes the row and cites its criteria as A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
2. **PRESENT** — read the audit: per-item PASS / FAIL / UNVERIFIABLE with evidence. A FAIL → remediate and re-run the preflight, never presented as acceptable, save item 2's human-only FAIL, carried by the packet (`.claude/references/core/gate-packet.md` `## The G2 packet`); the audit is relayed in HARD STOP's packet, not alone.
3. **STAGE** — per the `vc` binding: nothing to stage when the ruling form is in-conversation; the vc pack's staging skill when the binding names one.
4. **HARD STOP — the G2 ruling.** Put the G2 packet (`.claude/references/core/gate-packet.md` `## The G2 packet`) by `.claude/references/core/ruling-form.md`, asking for the human's explicit acceptance with the audit in view; an acceptance given earlier is held as `## The early word` says, and a stood word is that ruling; "Accept, but fix X" is taken as `## The G2 packet` says. Preflight PASS is not acceptance; silence, enthusiasm, or a "looks good" about anything but the gate is not a ruling. The ruling's form — where and how it is given: in conversation, a pull-request approval, … — is the one the `vc` binding names; `ruling-form.md` governs what the PM's message puts to the human. Only that ruling unlocks steps 5–8.
5. **RECORD** — file `.scratch/doc-writer-<feature>-g2-brief.md` from `.claude/references/core/decision-brief.md`: the ruling — who, when, in what form — as the decision record, the requirements doc as the target per `process-docs`, naming beside the G2 record its frontmatter `status:` from `draft` to `accepted` and its `updated:` to the ruling's date. Dispatch `doc-writer`. For every feature, RECORD also moves the architecture doc's frontmatter `status:` from `draft` to `accepted` and its `updated:` to the ruling's date: file `.scratch/architect-reconcile-<feature>-g2-brief.md` naming those two lines and dispatch `architect-reconcile` in its record mode — for a lite feature, the brief and dispatch below, which name them beside the G2 record. For a lite feature the target is the architecture doc, and the brief and dispatch that follow take the place of `doc-writer`'s: file `.scratch/architect-reconcile-<feature>-g2-brief.md` — `lite` and the rest `.claude/references/core/feature-lite.md` `## The acceptance source` asks of a lite feature's brief, the ruling, the head accepted, and the record's form and place as `.claude/references/core/feature-lite.md` `## The three placements` fixes them — and dispatch `architect-reconcile` in its record mode to write the G2 record.
6. **PRODUCT DOCS** — per the `product-docs` binding as `## Bindings` above says. Written only after the ruling, every claim verified against live source.
7. **MERGE** — per the `vc` binding's merge procedure. **The G2 ruling is itself the word to merge**; there is no second stop. Relay first what was authored *after* the ruling — RECORD's edit and any product-doc changes, the fix's diff after "Accept, but fix X" or the human-only re-run, and each option taken after the ruling with its option text — as a diff the human can read, then merge. Show, do not ask again.
8. **CLOSE OUT** — the PM's `memory/STATUS.md` line (within the `board` binding's *STATUS line cap* — absent or `none` → no cap stated); the board row per the `board` binding when one is bound. The same act writes the session's dated note as `.claude/references/core/gate-close.md` `## The dated note` says.
9. **HANDOFF** — invoke `handoff` for the next session's task.
