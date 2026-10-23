# Wiki mode

How `codex-reconcile` runs where the `product-docs` binding's `Layout` reads `wiki`.

## Schema first

Before touching anything, read the wiki's own schema/operating file (e.g. `<wiki root>/CLAUDE.md` — the binding's Schema file slot names it). It is NOT auto-injected into your context, and it outranks your instincts on page structure, frontmatter, linking, naming, and index/log bookkeeping. Follow it exactly, including its templates.

## Bookkeeping

Perform every index/hub/log update the schema requires for the pages you touched, including status fields and their index mirrors.
