---
type: requirements
feature: windows-native-harness
topic: windows-native-toolchain
status: accepted   # G2 ruled in-conversation 2026-10-14: accepted at 43dd329
updated: 2026-10-14
sources:
  - Planning~/design-docs/windows-native-toolchain.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0001-windows-native-wsl-removed.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0002-msvc-compiler.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0003-unity-bundled-cmake.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0004-pinned-vosk-win64.md
---

# feat-windows-native-harness — requirements

Track **Full**. Design `windows-native-toolchain` (`Planning~/design-docs/windows-native-toolchain.md`), locked at G1 2026-10-13.

## 1. User story

- **S1** — As the VoXR maintainer, I want the native bridge and push-audio harness to build and run Windows-native, so that Command 3's desktop check works from Claude Code's Windows shell with no WSL.

## 2. Feature & scope

Backlog row (`windows-native-toolchain.md` §6):

> `windows-native-harness` | The Windows x64 desktop build of the bridge and harness: a cross-compiler export macro, the GNU link flags guarded off MSVC, the libvosk name per platform, a Windows preset replacing `desktop-linux`, and harness README provisioning of the pinned `vosk-win64-0.3.45` (adr-0001–adr-0004); the verification binding's Command 3 desktop half and the recipe's desktop-harness section go Windows-native. Acceptance: 16/16 against the committed `NativeBridge~/harness/expectations.json`. **Full.** | None.

**Agreed at G0 on 2026-10-14** by the maintainer, in session.

**In**

1. The bridge and the push-audio harness build on Windows x64 with MSVC from Visual Studio 2022 (adr-0002), using the Unity-bundled cmake 3.22.1 and ninja 1.10.2 by absolute path (adr-0003). **Amended 2026-10-14:** ninja is not used — the build uses CMake's "Visual Studio 17 2022" generator (ruled "Use visual studio"); cmake 3.22.1 stays Unity-bundled by absolute path.
2. A cross-compiler export macro in `NativeBridge~/src/vosk_bridge.h`.
3. The GNU link flags in `NativeBridge~/CMakeLists.txt` guarded off MSVC.
4. The libvosk library name chosen per platform (today hard-coded `libvosk.so`).
5. A Windows preset in `NativeBridge~/CMakePresets.json` replacing `desktop-linux`, which is deleted (adr-0001).
6. `NativeBridge~/harness/README.md` provisioning of the pinned `vosk-win64-0.3.45` into the gitignored `NativeBridge~/vendor/` (adr-0004), plus its build and run steps.
7. The verification binding's Command 3 desktop half (`.claude/bindings/unity.md`) and the desktop-harness section of `Planning~/verification-recipe.md` go Windows-native.
8. Acceptance: 16/16 against the committed `NativeBridge~/harness/expectations.json`. A divergent Windows transcript stops for the human; never a silent re-pin.
9. Left to this feature's architecture doc (named by the design, §6 and its Open list): the route into the Visual Studio environment ("Visual Studio 17 2022" generator vs Ninja inside vcvars); the preset shape; the export-macro form; confirming the vosk-win64 zip contains `libvosk.lib` and whether its `libvosk.dll` is identical to the shipped `Runtime/Plugins/x86_64/libvosk.dll` — a difference stops for the human.
10. **Added 2026-10-14** (ruled "All recommended"): the `CMAKE=` line of `Planning~/verification-recipe.md` `### Building NativeBridge`, rewritten from its `/mnt/c/...` form to Git Bash form, so Command 3's arm64 half runs verbatim in Git Bash.

**Out**

1. Owned by backlog row 2, `windows-native-dotnet-fixtures`: Command 4 (the `Samples~` compile); the parser A/B rig's DocCheck run procedure; `Tests~/Fixtures/generate.sh` (adr-0005); the verification binding's prerequisites, Shell and Path-form lines; the recipe's other WSL sections; the closing `wsl` / `/mnt/` sweep that closes deferral WSL-1. **Amended 2026-10-14:** except the one `CMAKE=` line of `### Building NativeBridge`, now In item 10.
2. Any Windows bridge in or loaded into the Unity Editor (DR-4's editor half stands, design §4).
3. Product docs (e.g. `Documentation~/native-bridge.md`) — after G2, by the codex reconciler.
4. `.claude/settings.local.json` and history docs.

## 3. Why — the value it delivers

This feature carries `automated-verification.md` §3 goal 2 — Claude-runnable, no Unity, no device — onto the host Claude actually runs on, Claude Code's Windows-native shell (`windows-native-toolchain.md` §5). Command 3's desktop half runs with no second OS.

It also unblocks backlog row 2, `windows-native-dotnet-fixtures`, and later `feat-joined-rig-parser`, whose host the design settled Windows-native (`windows-native-toolchain.md` §4).

## 4. Observable behaviour

1. The maintainer or Claude provisions `NativeBridge~/vendor/` once per the harness README: the pinned `vosk-win64-0.3.45.zip`, and the small English model `vosk-model-small-en-us-0.15` (already present).
2. Command 3's desktop half runs from Git Bash as the verification binding writes it: configure, build, and run the harness executable against the model, the 16 fixtures under `Tests~/Fixtures/audio/tts` and `NativeBridge~/harness/expectations.json`.
3. It prints the JSON report and exits 0 with 16/16; exit 1 still means a transcript mismatch, exit 2 an operational error.
4. `wsl.exe` is never invoked; Command 3's arm64 half behaves as before.

## 5. Functional requirements

**Ruled on 2026-10-14** by the maintainer, in session ("Confirmed"): F3's symbol-list check is **Must**.

**Ruled on 2026-10-14** by the maintainer, in session ("agreed"): F2 admits one change to `expectations.json`, the `libvosk` pin's label, to name the win64 prebuilt; every transcript, the grammar and the gain stay.

**Ruled on 2026-10-14** by the maintainer, in session ("Use visual studio"): the build reaches Visual Studio 2022 through CMake's "Visual Studio 17 2022" generator; ninja is not used, so F1 no longer names it.

**Ruled on 2026-10-14** by the maintainer, in session ("All recommended. Go"): the recipe's `### Building NativeBridge` `CMAKE=` line joins this feature (§2 In item 10, F7), taken from row 2; the "Visual Studio 17 2022" generator route is read as within the locked design (design §4, adr-0002 hand the route to the architecture doc), so adr-0003's and design §1's "and ninja 1.10.2" wording reopens nothing.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The desktop bridge and harness configure and build on Windows x64 with MSVC from Visual Studio 2022 and Unity 6000.3.7f1's bundled cmake 3.22.1 named by absolute path, with nothing else installed. | S1 | Must | The bound Command 3 desktop half, run verbatim in Git Bash, builds with no errors. | adr-0002; adr-0003 |
| F2 | The Windows harness run reproduces the committed baseline exactly; only the `libvosk` pin's label in `expectations.json` changes, to name the win64 prebuilt. | S1 | Must | Exit 0, 16/16; in `git diff main -- NativeBridge~/harness/expectations.json` the only changed line is the `libvosk` pin (every `expectedFinals`, the `grammar` and the `gain` unchanged); a divergent transcript stops for the human — no re-pin without the human's ruling. | `windows-native-toolchain.md` §1 rule 4 |
| F3 | The Android arm64 build is unaffected. | S1 | Must | Command 3's arm64 half builds `libvosk-bridge.so` with no errors, and its exported symbol list matches that of the committed `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`. | `windows-native-toolchain.md` §0 Out (Android); adr-0002 consequences (GNU link flags guarded off MSVC, not removed) |
| F4 | The Linux/WSL desktop route is gone from this feature's sites: `NativeBridge~/CMakePresets.json`, `NativeBridge~/CMakeLists.txt`, `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/README.md`, the binding's Command 3, and the recipe's desktop-harness section. | S1 | Must | A grep of those sites for `wsl`, `/mnt/` and `desktop-linux` (case-insensitive) is empty. | adr-0001 |
| F5 | The harness README provisions the pinned zip and the model into the gitignored `NativeBridge~/vendor/`, then gives the Windows build and run steps. | S1 | Must | Following it from an emptied `vendor/` yields a green run. | adr-0004 |
| F6 | The zip's contents are confirmed: it carries `libvosk.lib`, and its `libvosk.dll` is byte-identical to the shipped `Runtime/Plugins/x86_64/libvosk.dll`. | S1 | Must | The hashes are recorded in the architecture doc; a missing `.lib` or a hash mismatch stops for the human before the build proceeds. | adr-0004 |
| F7 | The binding's Command 3 desktop half and the recipe's desktop-harness step (§2b) state the Windows-native route: tools by absolute path, Git Bash path conversion handled, the same green criterion (exit 0, 16/16); and the recipe's `### Building NativeBridge` `CMAKE=` line is in Git Bash form, so the arm64 half Command 3 runs "per the recipe" runs as written. | S1 | Must | The verification agent runs Command 3 from the binding verbatim — both halves — and gets green. | adr-0001; adr-0003; `windows-native-toolchain.md` §3 (Git Bash path conversion); ruling 2026-10-14 (§2 In item 10) |
| F8 | No Windows bridge ships in or loads into the Unity Editor. | S1 | Must | `git diff --stat main -- Runtime/` empty. | `windows-native-toolchain.md` §4 (DR-4's editor half stands) |

## 6. Non-functional requirements

- **Toolchain drift:** a tool missing at its absolute path (the Unity editor or Visual Studio moved) fails the build loudly, naming the missing path; it never falls back to another tool on PATH. Source: `windows-native-toolchain.md` §3, §1 rule 2.
- **Degraded vendor:** an unprovisioned `vendor/` fails loudly; the build never silently links the shipped DLL or a Linux library.

## 7. Non-goals / deferred

The G0 **Out** list in §2, plus:

- Any change to recognition or harness behaviour beyond what portability needs.
- Re-baselining `expectations.json` (the `libvosk` label update in F2 is not a re-baseline).
- Any Linux desktop build.
- Visual Studio 2026.
- CI.
- Clearing stale build caches (`build/`, `build-1.1.0/`, `build-1.2.0/`, `build-desktop/`), except where one is in the way.

## 8. Dependencies & assumptions

**Depends on:** no feature. Backlog row 2, `windows-native-dotnet-fixtures`, depends on this one.

**Assumption:** Unity 6000.3.7f1 is installed at `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\`.

**Assumption:** Visual Studio Community 2022 17.14 is installed with the x64 C++ tools and Windows SDK 10.0.26100.0.

**Assumption:** one-time network access for the zip.

**Assumption:** the model is already in `vendor/`.

**Assumption:** the 16 fixture WAVs are unchanged.

**Assumption:** the shipped `libvosk.dll` came from the v0.3.45 Windows release (`Planning~/v3.1-editor-mic-plan.md:258`).

## 9. Acceptance criteria (G2)

- [x] F1: the desktop bridge and harness build on Windows x64 with MSVC from Visual Studio 2022 and Unity's bundled cmake. (Phase 2 gate; MSVC 19.44 / MSBuild 17.14 via the VS 17 2022 generator)
- [x] F2: 16/16, exit 0; `expectations.json` changed only in its `libvosk` pin label. (16/16 exit 0 at Phases 2–3, the review fix and the G2 re-run; `expectations.json` diff is the label line only)
- [x] F3: the arm64 `.so` builds and its exported symbol list matches the committed one. (symbol diff vs the committed `.so` empty, 15 lines, every gate)
- [x] F4: no `wsl`, `/mnt/` or `desktop-linux` at this feature's sites. (Phase 3 gate grep: 0 at all six sites)
- [x] F5: the README provisions `vendor/` from empty to a green run. (Phase 3 gate; re-run from an absent `vendor/` after the review fix, 2026-10-14)
- [x] F6: the zip carries `libvosk.lib`, and its `libvosk.dll` hash matches the shipped one, recorded in the architecture doc. (architecture doc *Provisioned inputs*; `sha256sum -c` OK)
- [x] F7: the binding's Command 3 — both halves — runs verbatim and green, with the recipe's §2b and its `CMAKE=` line in Git Bash form. (Phase 3 gate: Command 3 both halves verbatim from the binding)
- [x] F8: no change under `Runtime/`. (`git diff main -- Runtime/` empty, every gate)
- [x] The commands of the `verification` binding this change triggers are green: Command 3, both halves. (review fix gate and the G2 re-run; EditMode 203/203 and PlayMode 704/704 also green)
- [x] Human-only verification: none — every check is Claude-runnable, which is goal 2's point (ruled 2026-10-14).

**Ruled at G2 on 2026-10-14** by the maintainer, in conversation: accepted at head `43dd329`, with the preflight in view — F5 re-run from an absent `vendor/` after the review fix changed the README's provisioning, and six `memory/` `.meta` added, before the ruling.

## 10. Open questions

1. **The route into the Visual Studio environment** — **Resolved 2026-10-14** by the maintainer: the "Visual Studio 17 2022" generator ("Use visual studio").
2. **The preset's name and shape**, including whether its `binaryDir` reuses `build-desktop/`, whose stale cache names `/usr/bin/c++` — the architecture doc's.
3. **The export-macro form** — the architecture doc's.
4. **Git Bash path-conversion handling** (`MSYS_NO_PATHCONV`) — the architecture doc's.
5. **Whether the arm64 `.so` must be recommitted** — the architecture doc's.
