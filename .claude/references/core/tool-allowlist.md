# Tool allowlist — per role

`validate`'s V6 requires an agent's `tools:` — split on commas, whitespace stripped — to be a subset of its row in its own pack's `PACK.md` `roles:`, whose keys are closed over that pack's `provides.agents`: an agent with no row fails V6 with `no allowlist row`, fail-closed per P6 — on its agent file, or on the `PACK.md`, naming the agent, when it is declared without a file — and a row for an agent the pack does not provide fails V6 as a stray allowlist row.

- **Bash is granted only to an agent that must run something** — a build, a version-control read, a reproduction. "read-only Bash" is not a tool category, Glob, Grep, and Read cover find, grep, wc, and ls.
- **Write and Edit go only to an agent whose deliverable is a file**, and its body names the single path prefix it may write under.

Every pack declares the rows of the agents it ships in its own `PACK.md`, core included; this reference holds none. The amendment's §4.5 matrix also states two pattern rows — the craft-pack spec authors (Read, Glob, Grep, Write, Edit) and the knowledge agents (Read, Glob, Grep, plus WebFetch and WebSearch when sourced); a pack that ships such an agent declares its row in its own `PACK.md`.
