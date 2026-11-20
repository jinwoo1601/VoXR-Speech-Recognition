---
name: close-tree
description: "Close a merged or halted tree from the hub, keeping the branch, or stop a handed-off session. Use on a 'FINISHED' line, a bare 'STARTED' line naming a predecessor, the human's word for a merged tree or to clear .scratch; never on 'BLOCKED'."
kind: execution
status: active
bindings: [{vc: [Main branch, Base-revision reads]}, {verification: [Shell, Path form]}, {trees: [Hub, List read, Clean read, Remove procedure]}, {session-launch: [Hub session name, List command, Stop command, Confirm window, Release wait]}]
---

# close-tree

## What this is

The hub session's half of a branch's close: the tree a finished branch was built in is removed from the hub once its checks pass, or kept with one line naming the check that failed. A report line is a trigger, never proof — a tree's close reads the `vc` binding's log read, the session list and the tree, never the report's detail, and `## A handed-off session`, which stops a handed-off session and removes nothing, takes from its line only the branch and the predecessor's session id. The removed line relays a `FINISHED` line's detail as printed and decides nothing by it. The removal rides on the gate ruling that authorised the merge — the G1 or G2 ruling, or a light-lane close by class — or, for a halted tree, on the human's halt ruling, whose words its halt record carries (`.claude/references/core/halt.md` `## The halt`), and takes no ruling of its own (`adr-0024`). `## The human's clear`, on the human's word alone, empties this project's `.scratch/` by one command and reads no binding.

> At a tree's close, nothing is stopped before the branch is merged or a halt record names it at its current head, nothing is read clean while a session is at the tree, nothing is removed before the release wait, and a half-removal or a refused step is told, never retried.

## When it runs

- A `FINISHED - <branch>; lane: <lane>; <detail>` line arriving in this session → `## Workflow` for `<branch>`; the detail decides nothing, and the removed line relays it.
- A bare `STARTED - <branch>; lane: <lane>; <skill>; predecessor <session id>` line arriving in this session → `## A handed-off session` for `<branch>` and `<session id>`; `## Workflow` does not run.
- The human's word on a tree `orient`'s TREES states merged and awaiting removal, proposed by its PROPOSE, or the human's word that the branch finished on a tree TREES states as "in progress, or finished with its report lost", for which PROPOSE offers this skill → `## Workflow` for that tree's branch.
- The human's word to close the tree again after a held or refused line for `<branch>` → `## Workflow` for `<branch>`.
- The human's word to halt a branch whose session is dead or idle → `.claude/references/core/halt.md` `## The hub's halt`, whose write is that reference's and not this skill's, then `## Workflow` for that branch.
- The human's word to clear this project's `.scratch/` → `## The human's clear`; `## Workflow` does not run.
- A `BLOCKED` line, a `STARTED` line naming no predecessor, or any other line → nothing runs, and the line is the human's to read.

This skill never answers a report, never gives a session work, and writes nothing to `memory/`.

## Bindings

- `vc` (required) — the *Base-revision reads* slot's log read MERGED runs, and the main branch TREE refuses and MERGED reads from. Absent → stop and say so before `## Workflow` or `## A handed-off session` runs; `## The human's clear` reads no binding, so this stop does not reach it.
- `verification` (optional) — the *Path form*, whose conversion a pack's `## The probe` may name for `<tree>`, which STOPPED's holder probes run. Absent → as that `## The probe` says; never a stop for that reason alone.
- `trees` (optional) — the Hub path and the Hub slot's idiom; the List read of TREE, MERGED and READ BACK; the Clean read of CLEAN; and the Remove procedure of REMOVE, with the held result it names where it names one, which READ BACK reads. Absent → `## Workflow` has no tree to remove: one line saying so, and it does not run; `## A handed-off session` runs with this session's project root as the tree, as it says.
- `session-launch` (optional) — the List command, the Stop command and the Confirm window, which STOPPED and `## A handed-off session` run, the Stop command as filled or `none` only, since each stop is `scripts/hub.py stop`, which reads the List command and the Confirm window itself; and the Release wait, read only where the verb prints `manual: Remove procedure` and the Remove procedure as the binding writes it does not wait itself, which RELEASE then waits before that procedure runs, the default Remove procedure waiting for the release itself; the Hub session name is not read, since this skill sends nothing; what the CLI behind those commands does is `.claude/references/core/claude-code-cli.md`'s. Absent, or the List command missing or `none` → STOPPED cannot run: R3's line naming the binding or the slot, and the tree kept. The Stop command or the Confirm window missing or `none` → R3's line naming the slot where a session is at the tree, and STOPPED holds where none is. The Release wait missing where it is read → RELEASE cannot run: R3's line naming the slot, and the tree kept.
- No agent: this skill dispatches nothing.

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
- Main branch: `main`
- Auto-mode entries: excluded
- Base-revision reads: `git show <revision>:<repo-relative-path>`; `git diff --name-status <base>..<head>` for the changed-file list; `git diff <base>..<head> -- <path>`; `git log --oneline <base>..<head>`; `git status --short` for the working-tree read; `git branch --show-current` for the current branch; `git rev-parse HEAD` for the current commit; `git diff --numstat --no-renames <base>..<head>` for the per-file line-count read; `git config --get <key>` for a config key's read, a bare `git config <key>` matching no allow rule
<!-- harness:binding:end vc -->

<!-- harness:binding:start verification .claude/bindings/unity.md -->
- Command 2: PlayMode, no command of its own: Command 1's one invocation runs it in the same bracket, never a second invocation (most parser and command tests are PlayMode, so it is never dropped) — expected green: the `PlayMode:` line as Command 1's, same exit-0 run, and `git status --short` showing no `Tests` or `Tests.meta` leftovers.
- Command 3: only when `NativeBridge~/` or a source `confgen` links (`NativeBridge~/harness/confgen/confgen.csproj`) changed: the arm64 build with the Unity 6000.3.7f1 Android SDK's `cmake.exe` and `ninja.exe` per the recipe, then the desktop harness Windows-native in Git Bash — MSVC from Visual Studio 2022 Community through CMake's "Visual Studio 17 2022" generator, the same SDK's `cmake.exe` by absolute path, `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` (`## One-time provisioning` in the one checkout, `## Provisioning a branch tree` in a branch tree; `webrtc-audio-processing-2.1.tar.xz` and `abseil-cpp-20240722.0.tar.gz` among it) — `(cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json)`; on a bridge ABI change the rebuilt `.so` is committed at `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`; on a front-end change the Release `voxr-frontend.dll` from `NativeBridge~/build-windows/bin/` is committed at `Runtime/Plugins/x86_64/voxr-frontend.dll` — expected green: `libvosk-bridge.so` built with no errors, and the harness exits 0 with 52/52 transcripts matching; a transcript mismatch (exit 1) stops for the human's ruling, never a re-pin
- Command 4: only when `Runtime/` public API or any `Samples~/` source changed: the out-of-band Samples~ compile on native dotnet in Git Bash, the csproj generated per the recipe into `.scratch/samples-compile/` — `(cd .scratch/samples-compile && dotnet build SamplesCompile.csproj -v m -nologo)` — the only check that compiles `Samples~`; a missing Unity or host input fails the build naming its path — expected green: `Build succeeded` and `0 Error(s)`; the one CS0618 warning on `PermissionDeniedAndDontAskAgain` shows when the compile runs (a fresh build, or any input changed since the last one), and a rebuild with no input changed skips the compile and prints `0 Warning(s)`, also green; and a mutation that breaks a sample turning it red
- Shell: Git Bash (an MSYS2 bash on Windows), in which every Command runs directly, each Windows program reached on PATH (`dotnet`) or by absolute path; Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1` and a plain Git Bash does not, and every bound command behaves the same either way
- Path form: Windows form `D:\…`, compared case-insensitively with either separator; the MSYS forms `/d/…`, `/c/…` and `/tmp` appear only inside commands, as command words bash resolves, and are never written as a path; an argument a Windows program receives and a path inside a generated file are in drive-letter form (`C:/…` or `D:\…`); the working-dir read `pwd -W` already prints this form (`D:/…`)
<!-- harness:binding:end verification -->

<!-- harness:binding:start trees .claude/bindings/vc-git.md -->
- Hub: a command runs in another tree by the idiom `git -C "<path>" <command>`, the path quoted, and only there — a command in the session's own tree runs bare from its root; the main checkout is `D:\Workspace\VoXR-Speech-Recognition` — it stays on `main` and holds `memory/`; every hub write while the hub is on `main` — `scripts/hub.py write --root D:\Workspace\VoXR-Speech-Recognition --main main`, from the hub or from any tree — is preceded by `git pull --no-rebase origin main` in the hub and followed by `git push` there, each run from a tree by this idiom, as the vc Check-in procedure states
- List read: `git worktree list` — one line per tree, the hub's first
- Clean read: `git status --short` in the tree — empty is clean
- Remove procedure: `py .claude/references/vc-git/tree-remove.py "<path>"`, run as written from the hub or any tree of the repository — one command, never wrapped in the Hub slot's idiom: it waits for the directory's release, runs `git worktree remove "<path>"` (never forced, so a tree with a pending change is refused), reads back, and prints `removed`, `refused`, `absent`, `held` or `half-removed`; the branch is kept
<!-- harness:binding:end trees -->

<!-- harness:binding:start session-launch .claude/bindings/core.md -->
- Hub session name: `voxr-hub` — this project's own hub session, which the human opens in the project root with `claude --remote-control voxr-hub -n voxr-hub --autocompact 250k`; the `--autocompact 250k` is the hub's compaction backstop, per launch and never in a settings file
- List command: `claude agents --json` — one object per running session on this machine, each with its `cwd`; see the reference for its shape
- Stop command: `claude stop <id>` — the `id` the list command shows
- Confirm window: 30 seconds (provisional → the tuning pass)
- Release wait: none — the vc-git Remove procedure (`.claude/references/vc-git/tree-remove.py`) waits for the directory's release itself
<!-- harness:binding:end session-launch -->

## Workflow

`<branch>` is the branch the trigger names.

**TREE** to **READ BACK** — first the shell returns to this session's project root. Then one command, alone in its call, run at this session's project root: `py <script> close-tree --branch <branch> [--detail '<detail>'] [--waiting <item>]...`, `<script>` as `.claude/references/core/gate-close.md` `## The clear` gives it, `<branch>` by its own name, never prefixed with the main branch's name, `<detail>` the `FINISHED` line's detail as printed, in single quotes, each `'` in it written `'\''`, `--detail` omitted where the human's word triggered the close, and one `--waiting <item>` per item this session already holds as waiting on `<branch>`, in the order the human ruled, none where it holds none. The verb runs TREE, the List read, run in the hub by the trees contract's hub clause, for the tree on `<branch>` and its path, then MERGED, STOPPED with each composed pack's holder probe, CLEAN, REMOVE (the default Remove procedure waiting for the release itself) and READ BACK, as `py <script> close-tree --help` states each, then a halted feature's writer trees; it runs each `py <script> stop <id>` itself, so its Bash call is given a `timeout` of 600000 ms, never the tool's shorter default. The table below reads what it prints, each line by its first word.

| `close-tree` prints | `## Workflow` |
|---|---|
| `stop: stopped <id> at <tree>` or `stop: gone <id> at <tree>` | a session at the tree the verb stopped, its conversation kept, or found gone; nothing is said for it, and the line after it is read |
| `removed: <line>` | the tree is removed and the branch kept: `<line>`, as printed, is the one line to the human |
| `kept: <line>` | the tree is kept: `<line>`, as printed, is the one line to the human, then each `!` line and `children:` line printed after it, as printed, for the human to run |
| `held: <line>` | the tree is kept: `<line>`, as printed, is the one line to the human. Nothing in this skill ends a holder: a held tree waits for the human's word. |
| `half-removed: <line>` | the tree is unregistered and its directory left: `<line>`, as printed, is the one line to the human; `orient` names the directory as a leftover until the human removes it |
| `no-tree: <branch>: <which>` | one line to the human saying which, as printed after `no-tree: `; nothing else runs |
| `skipped: trees absent` | `## Workflow` has no tree to remove: one line saying so, and nothing else runs |
| `manual: <slot>` | that slot's part runs as the binding writes it — for `manual: holder probe <pack>`, that pack's `.claude/references/<pack>/tree-holder.md` command under `## The probe` run once from this session's project root, `<tree>` the tree's path in the form, and by any conversion call, its `## The probe` names, and read by its `## Reading`, held or unreadable keeping the tree with `references/outcomes.md`'s held line in that form; for `manual: Remove procedure`, RELEASE runs first, the `session-launch` binding's Release wait, where the procedure as the binding writes it does not wait itself, then the procedure — then each part after it as `py <script> close-tree --help` states it, any part failing ending the close with its line from `references/outcomes.md`; nothing the verb already did is run again |
| `re-run: <call>; stopped <ids or none> this call` | the verb's time ran short with work left: each `<call>`, in the order printed, runs again, alone in its call, at this session's project root, its Bash call given 600000 ms, and its lines are read by this table; a `re-run:` line naming no session stopped this call that follows another `re-run:` line of this close is said to the human instead, and the close ends |
| exit 1 with no outcome line, or any other output | its first stderr line, or the output, is said to the human in one line; nothing more runs |

**A halted feature's writer trees.** Where MERGED passed by a halt record whose `branch:` line names `<branch>` and the tree is removed, the verb closes in the same call each unit tree the record's `unit:` lines name that the List read shows, in the record's order, each with its own lines, read by the table above; a unit with no tree is passed over, and every branch the record names is kept.

**Freed dependents.** Where this close's `removed:` line for `<branch>` takes its merged form, its `<line>` holding `branch kept - no commit outside`, not the halted form's `branch kept - halted at` (`references/outcomes.md`), then once that line is said, one more command, alone in its call, run at the project root: `py <script> ticket status --root "<project root>"`, `<script>` and `<project root>` as `.claude/references/core/gate-close.md` `## The clear` gives them. Each line it prints that begins `tickets: freed` is said to the human as printed, after the removed line, as a proposal: nothing is cut or launched, and the cut is the human's word. Each line it prints that begins `tickets: dependents:` or `tickets: dependents not read:` is said as printed. No other line of it is said, whatever its exit, and no stop is put; the call refused by permission or policy is said in one line, the refusal's words, and never retried in another form. These lines follow the close's one line and are no part of it: the removed line stays that line.

**A refused step.** The verb's one call refused by permission or policy stops the close there: nothing runs after it, the refused call is never retried in another form, and the human hears the refused line, `<step>` TREE, `<path>` `unread` and `<state>` `kept`, as `references/outcomes.md` gives it. A refusal is no read's result: never a failed check, never held or unreadable.

## Outcomes

Every close tells the human one line, formed as `references/outcomes.md` says, beside the table of every outcome, the tree and its sessions; read it before saying the line: the removed line where the tree is removed, or R3's line, the held line or the refused line where a close ends short of `removed`.

## A handed-off session

It runs on a bare `STARTED` only — a brief written with no start step, where no manifest gave its script, or before the start step was built; otherwise the successor ran PREDECESSOR's verbs itself at its start and sends no `STARTED`. `<branch>` and `<session id>` are the ones the `STARTED` line names; the line is a trigger and never proof. The tree is kept: nothing is removed, read clean or written, and MERGED is not read.

1. **TREE** — as `## Workflow`'s TREE.
2. **PREDECESSOR** — first the shell returns to this session's project root. Then one command, alone in its call: `py <script> predecessor "<path>" <session id> --stop --relay <branch>`, `<script>` as `## Workflow`'s, `<path>` the tree's path, its Bash call given 600000 ms, never the tool's shorter default, as the `stop <id>` act below gives its own, since the verb runs that act itself, with every check it names, and prints the line this step says for the case; the table after this list reads what it prints. On that table's `manual: Path form` row only, the trees contract's `predecessor` verb on the tree's path and `<session id>`, which decides the case where the Path form is Windows form; under another Path form this step decides the same cases by that Path form itself, as the contract says. One act per output, `<path>` the tree's path, each line one line to the human, its template plain ASCII and `<cwd>`, `<path>` and `<session id>` printed as read:
   - `stop <id>` → `py <script> stop <id>`, `<script>` as `## Workflow`'s, its conversation kept, its Bash call given a `timeout` of 600000 ms, never the tool's shorter default, since the verb may wait up to its `CHILD_WAIT`, 300 seconds, on a live child process before its stop and re-reads the List command within the *Confirm window* after it: the verb checks the tree's branch merged, a later session started at the tree, or a halt record in the hub's `memory/` naming the branch at its current head, stops it and re-reads the List command until that `<id>` is gone, within the `session-launch` binding's *Confirm window*; exit 0 → nothing, silently; a confirm refusal → `<branch>: predecessor <session id> still listed at <path>`; any other exit, a `children` refusal among them → `<branch>: predecessor <session id> not stopped - <the verb's refusal line>`, and nothing is stopped.
   - `gone` → nothing, silently.
   - `elsewhere <cwd>` → `<branch>: predecessor <session id> not stopped - listed at <cwd>, not at <path>`, and nothing is stopped.
   - `alone` → `<branch>: predecessor <session id> not stopped - no newer session at <path>`, and nothing is stopped.
   - `no-id` → `<branch>: predecessor <session id> not stopped - no stop id at <path>`, and nothing is stopped.
On the `manual: Path form` row, the `predecessor` verb exiting 1 or 2 → one line to the human naming it, and nothing is stopped; the `--stop --relay` call exiting 1 or 2 prints its `relay:` line, which the table reads. Nothing is kept between lines: a replayed `STARTED` reads `gone`.

| `predecessor --stop --relay` prints | PREDECESSOR |
|---|---|
| `relay: <line>` | `<line>`, as printed, is the one line to the human, a template above filled as read; nothing more runs |
| `silent: <case>` | nothing, silently |
| `manual: Path form` | it read and stopped nothing: the list above runs, the case decided by that Path form |
| no `relay:`, `silent:` or `manual:` line, from a usage error or a crash | as the verb's call refused, below |

The verb's call refused by permission or policy → `<branch>: predecessor <session id> - call refused at <path>: <the refused call>`, and nothing after it runs; the refused call is never retried in another form.

Where `trees` reads absent, the tree is this session's project root: TREE reads nothing, `<path>` is the project root, and the steps run as written.

The `session-launch` binding absent, or a slot these steps need missing or `none` → one line to the human naming it, and nothing is stopped.

At a tree's close and for a handed-off session alike, this skill writes nothing to `memory/`, keeps the branch and sends nothing to any session; a tree's close says the removed line on success, and a handed-off session's stop says nothing on success.

A handoff the hub session takes on the human's word, at any boundary of `## Workflow` or of `## A handed-off session`, or where either stops, as `handoff` `## When to hand off` says, has as its objective the next step — after the last step or a stop, the hub session's next act: the next report line or the human's next word. The brief targets the hub or, where `trees` reads absent, the project root, so `handoff` launches nothing and shows the paste line (`handoff`'s `references/launch.md` condition 3); its brief, note and pointer are `handoff`'s, not this skill's.

## The human's clear

The human's word to clear this project's `.scratch/` runs this section, in any project shape, tied to neither a handoff nor a compaction; nothing runs it unasked. It reads no binding and asks nothing: the word is the human stop. Where `trees` reads absent a branch session's files may lie there too, and the timing is the human's (`adr-0183`).

One command, alone in its call, run at the project root: `py <script> scratch-clear --root "<project root>" --keep session-brief.md`, `<script>` and `<project root>` as `.claude/references/core/gate-close.md` `## The clear` gives them. With no manifest the clear cannot run: say so, and nothing else runs.

At exit 0, each `kept:` line the command prints, with the `pending:` lines after it, is relayed as printed, and nothing else is said. A non-zero exit: one line saying so, with its first stderr line. A refused call: the refusal's words, and no retry in another form.
