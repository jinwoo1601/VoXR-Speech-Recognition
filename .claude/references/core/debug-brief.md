# Debug brief — <slug>

<!-- Written by the implement skill (when verification fails for a non-obvious reason), by the
     light lane, or by the PM where a persisted plan names a red check, into .scratch/debugger-<slug>-brief.md before dispatch.
     The debugger reads this file by ABSOLUTE path; it is its entire context — anything not written here does not exist for the agent.
     Common fields per .claude/references/core/delegation-contract.md, then the debugger extensions. -->

## Task

- Agent: `debugger`
- Baseline model: `<the agent file's model:>`; downgrade for this dispatch: `none`
- Binding to consult: the project's `verification` binding, quoted under `## Reproduction`
- Task: find the root cause of the symptom below and propose the minimal fix; apply nothing

## Symptom

- Observed: `<what happens>`
- Expected: `<what should happen>`
- Failing output, verbatim:

```
<the output as it appeared>
```

## Red check

<!-- For a red check — a test a persisted plan names, shown to fail without the code it pins and to pass with it — this section replaces `## Symptom`; delete whichever of the two does not apply. -->

- Test and its command: `<the test>` — `<the command>`, run from `<absolute path>`
- Pinning change: `<files and lines — or: the revision read idiom the vc binding names, with the revisions>`
- Scratch path: `<absolute path>`; steps that build the copy without the pinning change: `<the steps, in order>`
- Expected red failure: `<the assertion on the pinned behaviour the test exists to catch>`

## Reproduction

- Command: `<the command>`, run from `<absolute path>`
- Current output: `<the tail that shows the failure>`
- Bound verification command: `<the command, verbatim — or: none bound>` — expected green: `<pattern, or n/a>`

## Scope

- The root cause may sit in: `<absolute paths>`
- Declared out of bounds: `<absolute paths the investigation does not enter>`

## Inputs

- Plan file: `<absolute path>`
- Requirements rows in play: `<absolute path>` (rows `<F…>`)
- Change set: `<the read idiom the vc binding names, with the revisions>` — when relevant

## Deliverable — what "root cause found", or for a red check the red and green evidence, must contain

1. **Evidence** — `file:line` and the mechanism: why this code produces the observed output.
2. **The minimal fix proposal** — the files and the change, described, not applied.
3. **The proving reproduction** — the command whose changed output would prove the fix.
4. **Alternatives ruled out** — each candidate cause, and how it was eliminated.

For a red check, the four blocks `debugger`'s `## Output` names, instead of the list above:

1. **Red evidence** — the failing output in the copy, verbatim, and why it is the pinned failure.
2. **Green evidence** — the pass in the tree.
3. **Fix proposal** — `none — red check`.
4. **Alternatives ruled out** — each other reason the test could fail in the copy, and how it was eliminated.

## Acceptance criteria for this dispatch

1. ...
2. ...

## Declared out of scope

- ...

## Constraints

- Never edit, create, or delete any file.
- Bash runs the reproduction and the verification command only, and in red-check mode also the scratch-copy steps `## Red check` names.
- A scratch copy, if one is needed, lives only under the path this brief names.

## Verification command

`<the bound command, verbatim — or: none bound>` — expected green: `<pattern, or n/a>`

## Stop-and-report rule

If the symptom cannot be reproduced, report `PARTIAL` with everything that was tried. A missing field is answered `BLOCKED` naming it. Do not adapt.
