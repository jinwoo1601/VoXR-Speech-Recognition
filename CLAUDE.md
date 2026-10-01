# Project Instructions

<!-- harness:managed:start -->
<!-- Managed by harness.py — scaffold, add, and upgrade rewrite everything between these markers. -->

**Packs:** codex, core, design-track, feature-track, light-lane, unity, vc-git

**Bindings:** `.claude/bindings/<pack>.md` — one file per pack, one `##` section per key.

<!-- harness:managed:end -->

## Documentation (codebase knowledge)

Start at **`Documentation~/index.md`** — the hub with the full table of contents (getting started, guides, and the per-type API reference under `Documentation~/api/`). `documentationUrl` in `package.json` points here too.

Highest-value files when investigating an issue or change:

- **`KNOWN_LIMITATIONS.md`** (repo root) — known constraints with repro steps, root causes, and workarounds. Check here first; a reported "bug" may be a documented limitation.
- **`Documentation~/troubleshooting.md`** — platform support table and common issues/solutions.
- **`Documentation~/native-bridge.md`** — the C++ bridge architecture (pair with the build command below).
- **`Documentation~/command-recognition.md`** — the audio→command parsing pipeline.
- **`Documentation~/api/`** — authoritative reference for each public type (`speech-recogniser.md`, `command-recogniser.md`, `data-types.md`, `error-codes.md`, …).
