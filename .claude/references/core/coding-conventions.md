# Coding conventions

Every new code file opens with the header block below, before any import, include, or using line. `code-writer` applies it through rule 3 of `.claude/references/core/implementation-rules.md`, and the `CONV` angle of a review cycle checks it.

## Header block

```
// ============================================================================
// Purpose:  <concise description of what this file does>
// Layer:    <architectural layer or module path>
// Owns:     <type names defined in this file (with visibility and kind)>
// Depends:  <types this file directly depends on, or "(none)">
// ============================================================================
```

## Field rules

- **Purpose**: One line, start with a noun or gerund. Describe *what*, not *how*.
- **Layer**: Dot-separated path reflecting the file's location in the architecture (e.g. `Runtime`, `Runtime.Commands`, `Editor`, `Tests`, `Scripts`).
- **Owns**: Comma-separated list of types defined in the file. Include visibility and kind: `ParseResult (public readonly struct)`, `EditorPanel (internal static class)`. For files defining multiple types, list all. A script that defines no types writes `(script, no public types)`.
- **Depends**: Comma-separated list of project types this file directly uses. Write `(none)` if there are no project-internal dependencies. Exclude framework and language types (e.g. `List<T>`, `std::string`, `dict`).

## Comment style by language

| Language family | Comment prefix |
|---|---|
| C#, C, C++, Java, JS, TS | `//` |
| Python, Bash, CMake, YAML | `#` |
| Lua | `--` |
| SQL | `--` |

Use the separator line style that matches: `// ====...` for `//` languages, `# ====...` for `#` languages, etc.
