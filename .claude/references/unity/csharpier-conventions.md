# csharpier-conventions — how CSharpier lays out C#

The authoring half of this pack's formatter pair. `.claude/references/unity/csharpier-hook.sh` reformats every C# file a session edits, immediately after the edit; this file says how that formatter lays code out, so what gets written starts near the formatter's fixed point and the reformat is a small diff rather than a large one. Its reader is an agent about to write C# in a Unity project — not a human learning the tool, and not someone configuring it.

Everything below is **CSharpier's documented defaults**, stated as defaults. A project may override the few options CSharpier exposes in its own CSharpier configuration file; where it does, the project's configuration wins and this file is wrong for that project. Where a specific below could not be established with confidence, it says so in place rather than asserting a number — a confidently wrong default is worse than a hedged one, because the hook would silently "correct" the writer and nobody would learn the file was wrong.

## What CSharpier decides, and the writer does not

CSharpier is opinionated by design and near-non-configurable on purpose: it exposes a handful of options — print width, spaces or tabs, indent size, line endings — and nothing that changes a layout decision. Everything below that line is settled by the formatter, the same way every time, and is not worth an opinion.

- **Print width: 100 columns, by default.** What fits inside it stays on one line; what does not is broken.
- **Indentation: four spaces, by default.** Tabs and indent size are among the configurable options; absent a project override, assume four spaces.
- **Line endings: taken from the file, by default** (the `auto` setting), and every file ends with a single trailing newline.
- **Whitespace is normalised everywhere** — around operators, after keywords and commas, inside braces and brackets. Type it roughly and it comes out right; spend no effort here.
- **Runs of blank lines collapse to one.** A single blank line where it was put is preserved, so blank lines between members remain the one piece of layout worth placing deliberately.

## What to type, then

- **Allman braces.** The opening brace goes on its own line — types, members, control flow, and bare blocks alike. This is the formatter's brace style, not a preference; K&R braces simply produce a large reformat.
- **Stay inside 100 columns while writing.** Where a call, a signature, or an expression will not fit, break it the way the formatter would rather than leaving a 180-column line for the hook to explode.
- **Argument and parameter lists break all-or-nothing.** When a list does not fit, every argument moves to its own line at one extra indent level, with the closing parenthesis on a line of its own. Never two per line; never "the first argument stays up on the call line".
- **Long member chains break before the dot,** one call per line at one indent level.
- **Align nothing by hand.** Columns of assignments, trailing comments lined up, padded table-shaped initialisers — all of it collapses to single spaces. Hand alignment is lost work.
- **Comments keep their attachment and their text.** A comment stays with the node it leads or trails, and its text is not reflowed — so the file header block that `.claude/references/core/coding-conventions.md` requires survives verbatim, separator lines included.
- **Nothing inside a string literal is touched,** verbatim and raw string literals included.

## What CSharpier leaves to the writer

It is a formatter: it changes layout, never meaning, and it makes no editorial decision. These stay the writer's to get right the first time, because nothing downstream fixes them.

- **Member order.** Types, fields, and members are not reordered; the order written is the order that ships.
- **Using directives.** Understood not to be sorted, grouped, or pruned by the formatter — *this one is stated with moderate confidence only*. Either way, treat an unused or misordered using as the writer's to avoid, not the hook's to clean.
- **Names, access levels, and modifiers.** Unchanged. *Not established with confidence here:* whether any modifier-order normalisation happens. Assume none, and write modifiers in the conventional order.
- **Namespace style.** File-scoped or block-scoped, whichever is written is kept; the formatter does not convert between them.
- **Braces on single-statement bodies.** *Not established with confidence here:* whether CSharpier adds them. Always write them — that is correct under either behaviour.

## What the formatter will not do

**A file that does not parse is not formatted.** CSharpier needs valid C#; on a syntax error it leaves the file as written and reports the failure, which surfaces as a hook failure straight after the edit rather than as a silent no-op. So the first requirement on generated C# is that it parses — a half-written file left behind is not merely unformatted, it is a failed hook.

It also does not compile, lint, rename, remove dead code, or rule on style beyond layout, and it does not read or repair a project's own configuration. Escape hatches exist — a `csharpier-ignore` comment before a node, and a start/end comment pair around a region, *the exact spellings being version-specific and not restated here* — and generated code should essentially never need one: reaching for an ignore comment usually means hand alignment nobody asked for.

## Status

A reference, not a check definition: it reports nothing and scans nothing, and it belongs to no preflight audit. A project composing this pack reaches it at `.claude/references/unity/csharpier-conventions.md`, and its reader is the agent writing C# there — it is consulted before the writing, while the pack's hook enforces the same layout after it.

The defaults above are the tool's own. A project's CSharpier configuration overrides them, and a project's configuration is a project fact rather than a pack fact: where the two disagree, the project's file governs and this page is the one that is out of date.
