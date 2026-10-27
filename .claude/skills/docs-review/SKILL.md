---
name: docs-review
description: "Review programming docs against the code they describe, as a findings report, never a rewrite, new docs or non-code prose. Use when asked to review, audit, critique or assess docs, or on 'what's wrong with these docs'."
disable-model-invocation: true
kind: execution
status: active
removal-date: null
bindings: [product-docs, verification]
---

# docs-review

## What this is

A rigorous review of existing programming docs — READMEs, API references, SDK guides, package docs, tutorials, migration guides — against the source code they describe, for three things in priority order:

1. **Accuracy** — does what the docs claim match what the code actually does?
2. **Clarity** — will the target reader understand it?
3. **Structure & navigation** — can someone find what they need?

Grammar polish, style preferences, and tone nits are out of scope unless they actively hurt clarity. A review that flags taste preferences gets ignored, which devalues the real findings.

> The review reports on the docs; it never rewrites them.

## Bindings

- `product-docs` (optional) — where the documentation under review lives and which layout it is in, so the docs under review are identified from the project rather than guessed. Absent → review exactly the paths the caller named, and say in the report that the docs' location was the caller's, not the project's.
- `verification` (optional) — the command that actually runs, so a documented example can be checked by running it rather than by reading it. Absent, or `none` → every runnable-example check stays an inspection and is reported as such.
- No agent: this skill dispatches nothing. Every check below is performed in this session, against files read here.

<!-- harness:binding:start product-docs .claude/bindings/codex.md -->
<!-- Interview: Where do this project's product docs live, is the layout a wiki vault or a Documentation~/CHANGELOG surface, is there a schema file for them, and which agent reconciles them after G2 — or none bound? -->
- Location: `Documentation~/` (the hub `Documentation~/index.md`, the per-type API reference under `Documentation~/api/`), plus `CHANGELOG.md` and `KNOWN_LIMITATIONS.md` at the root
- Layout: docs+changelog
- Schema file: none
- Reconciling agent: codex-reconcile
<!-- harness:binding:end product-docs -->

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

## Before reviewing: set up the inputs

Accuracy review is the most valuable thing this skill does, and it requires the source code the docs describe. Confirm you have both before starting.

- **Docs present, code present** — ideal. Proceed.
- **Docs present, code missing** — ask for the code, or at minimum the specific files or modules the docs reference. If it cannot be provided, proceed but explicitly flag every accuracy check as "unverified" in the report rather than guessing.
- **Docs reference code not in the provided source** — note as "unverifiable — file not provided" rather than assume it is wrong or skip silently.

Which files are docs and which are code comes from the `product-docs` binding where it is bound; without it, from the paths the caller named. Docs are usually `.md`, `.mdx`, `.rst`, or text in a docs directory; code is everything else — read the repo layout if it is unclear.

## Workflow

### Step 1: Identify the target reader

Before critiquing anything, figure out who the docs are for. Different readers need different things:

- **First-time users / newcomers** — need context, why-use-this, a working quick start
- **Integrators using the API** — need accurate reference, examples, gotchas, edge cases
- **Contributors / maintainers** — need architecture, setup, conventions
- **Mixed audience** — needs strong navigation so each reader can find their section

Infer this from the docs themselves (intro, table of contents, tone). If it is ambiguous, that is itself a finding — docs that do not know their audience usually fail all three dimensions at once. State the assumed target reader at the top of the report so it can be corrected.

### Step 2: Skim to build a map

Read through once at reading speed before critiquing. Note:

- What does this doc claim to cover? What is its scope?
- What is the structure? (section order, headings, overall flow)
- What code symbols are referenced? (functions, classes, files, modules, config keys)
- What examples are shown?

You are building context, not judging yet. Reviews that start judging on page one miss the shape of the whole thing.

### Step 3: Verify accuracy against the code

This is where the skill earns its keep. For every code-related claim, open the actual source and verify. Things to check:

- **Function and method signatures** — name, parameter names, parameter types, defaults, return type, async vs sync
- **Class structure** — attributes, methods, inheritance, visibility as described
- **Behavior claims** — if docs say "returns `null` on failure" or "raises `TimeoutError` after 30s", confirm in code
- **Error handling** — claimed exceptions, error codes, and status codes match what the code actually produces
- **Code examples** — would they run as written? Imports present? Variables defined? No typos in symbol names? Output shown matches what the code would produce? Where the `verification` binding names a command, run it and report the result; where it does not, this check is an inspection and the report says so
- **Installation and setup** — commands work, package names exist, version numbers are real
- **Configuration** — field names, types, defaults, and required-ness match the actual config schema or code
- **Defaults, limits, constants** — timeouts, max sizes, retry counts, rate limits match constants in code
- **Cross-references** — "see the `Foo` class" — does `Foo` exist? Does it do what this section implies?
- **Version-specific claims** — deprecations, "as of vX.Y", "new in vZ" should be correct

When the code contradicts the docs, the docs are almost always what needs to change. (Occasionally the code has a bug the docs happen to describe correctly — if it looks like that, flag it as a finding for the human to decide.)

### Step 4: Evaluate clarity

Clarity is the reader's experience. For the inferred target reader, ask:

- **Is jargon defined on first use?** Terms the target reader might not know need introduction. Terms they definitely know do not.
- **Are prerequisites stated before dependent steps?** If step 3 requires an env var set in the reader's shell, step 1 or 2 should mention it.
- **Are references specific?** Ambiguous "it"/"this"/"the above" creates friction. In "Call `client.connect()` — this returns a Promise", what does "this" point to?
- **Do examples match the explanation?** If the prose says one thing and the example shows another, reader trust drops fast.
- **Is there enough surrounding context for code blocks?** A naked code block usually needs a sentence before ("to open a connection, call:") and sometimes one after.
- **Too much, too soon?** Does the intro bury newcomers in edge cases before the happy path is on screen?
- **Mental model set up correctly?** If the first section gives the wrong conceptual model, everything after it is harder.

### Step 5: Evaluate structure & navigation

Can a reader find what they need?

- **Heading hierarchy** — do headings describe their sections? Do H2/H3 nest sensibly? Can you understand the shape of the doc from headings alone?
- **Discoverability** — if someone searches "how to authenticate" or "rate limits", would they land in the right place?
- **Flow** — does top-to-bottom reading make sense? Either progressive (simple → complex) or topical (grouped by subject) both work; inconsistent mixing usually does not.
- **Table of contents / index** — for anything longer than one screen, is there a way to jump around?
- **Cross-links** — when one section references another, is there a link?
- **Duplication** — is the same thing explained in two places, and do the two versions stay in sync?
- **Dead ends** — sections that introduce something but do not link to where it is fully covered

### Step 6: Prioritize and write the report

Group findings by severity using this framework:

- **Critical** — docs will mislead users into writing broken code, cause a security or data-loss mistake, or describe behavior that does not exist. Examples:
  - Function signature in docs does not match the code
  - Quick-start example has a typo that prevents it from running
  - Docs omit a required step that causes silent data corruption
  - Security/auth instructions are wrong
- **High** — significant clarity or completeness problem that blocks typical use cases, but will not produce a bug on its own.
  - Major feature is undocumented
  - Key concept assumed without introduction
  - Wrong mental model set up in intro
  - No working example for the main use case
- **Medium** — impairs experience for a meaningful fraction of readers, but most get through.
  - Section harder to follow than necessary
  - Examples present but unrealistic or underspecified
  - Navigation gaps, missing cross-links
  - Inconsistent terminology in the same doc
- **Low** — polish, minor inconsistencies, nice-to-haves.
  - Stylistic inconsistencies that do not confuse
  - Small formatting issues
  - Missing links that exist elsewhere

A review with a long Low section and nothing Critical is a signal that the docs are genuinely in good shape. Do not inflate severity to look thorough. Do not pad Critical with High-looking items.

## Output format

Use this exact structure:

```
# Documentation Review: <doc name or subject>

**Scope reviewed:** <what files / sections were covered>
**Target reader assumed:** <your inferred target reader>
**Source code referenced:** <what code was used to verify claims>
**Inputs:** <product-docs bound, or the caller's paths; verification bound and run, or examples inspected only>

## Summary
<2–4 sentences: overall state, biggest strength, biggest weakness, and the single change that would most improve the docs.>

## Critical findings
<Numbered list, finding format below. If none, write "None found." Don't pad.>

## High findings
<Same.>

## Medium findings
<Same.>

## Low findings
<Same. If long, group similar nits.>

## What's working well
<3–5 bullets on what the docs do right. Not flattery — signal for the author about what to preserve.>
```

### Finding format

Each finding:

```
**<Short title>** — `<path/to/file>` § <Section name>
- **Evidence:** <what the docs say vs what the code says, or the specific confusing passage, with line numbers or quotes>
- **Impact:** <why this matters for the target reader>
- **Suggested fix:** <concrete recommendation, 1–2 sentences; for larger fixes, sketch the new structure rather than rewriting>
```

## Principles

**Be specific. Always cite a location.** "The API section is confusing" is not a finding; "The `connect()` docs in `api.md § Connections` don't mention the required `timeout` parameter — see `src/client.py:42`" is.

**Tie accuracy findings to code.** Quote or point to the relevant source. This makes the review trustworthy and the fix obvious.

**Don't confuse taste with defects.** If you'd word something differently but the existing wording is clear and correct, leave it alone.

**Suggested fixes should be short.** A sentence or two. If a section needs a rewrite, say so and sketch the new structure — don't deliver the rewrite.

**If you can't verify something, say so.** Note it as unverified rather than guessing — and say which binding was missing where that is why.

**Prioritize honestly.** The severity of a finding is about reader impact, not how much effort went into finding it.

## What this skill does NOT do

- Rewrite the docs wholesale — review, don't ghostwrite.
- Flag style preferences (Oxford comma, title vs sentence case, line length) unless they cause real confusion.
- Critique the underlying API or product design — scope is the docs, not what they describe.
- Grade grammar unless grammar issues cause misunderstanding.
- Score or grade the docs on a numeric scale — severity-categorized findings are more actionable.
- Decide anything. The findings go to the human; the ruling and the fixes are theirs.
