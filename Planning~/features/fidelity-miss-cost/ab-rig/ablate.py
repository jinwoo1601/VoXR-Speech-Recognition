#!/usr/bin/env python3
"""Phase 4 measurement for feat-fidelity-miss-cost (issue #65 §5.1).

Two passes over the SAME real decoder output, replayed through the real parser at two
revisions (see stage.sh):

  4A  the 16 committed transcripts, unmodified.  Regression check: the delta must be
      exactly zero, because every committed transcript is an exact phrase match and so
      misses no required literal.  This is the pass that would have been the whole of F8;
      on its own it is vacuous as evidence ABOUT the change, which is why 4B exists.

  4B  every single-word-deletion variant of those transcripts.  This is the phenomenon
      under test -- a dropped short function word, which §1 names as the failure VOSK
      makes most often -- and the corpus cannot exercise it any other way without
      regenerating the fixtures, which the NFRs forbid.

Nothing here invents an utterance: every replayed line is a committed transcript, or a
committed transcript minus exactly one word.

Usage:  ablate.py <before-dir> <after-dir> <expectations.json> [--out report.md]
"""

import json
import subprocess
import sys
from pathlib import Path

MIN_SCORE = 0.6  # VoxrCommandRecogniser.cs:30, the default gate
UNK = "[unk]"


def transcripts(expectations_path):
    """Every non-empty final in the committed baseline, tagged with its fixture."""
    data = json.loads(Path(expectations_path).read_text())
    out = []
    for case in data["cases"]:
        for i, final in enumerate(case.get("expectedFinals", [])):
            if final.strip():
                out.append((case["file"], i, final))
    return out


def utterances(expectations_path):
    """One line per fixture, as the PARSER sees it.

    expectedFinals are BRIDGE-level finals. For 15 of the 16 fixtures there is exactly one
    and it is an exact phrase match. The 16th (`split_…`) deliberately breaks one command
    across a mid-utterance pause, and the utterance buffer rejoins its two finals before
    parsing — which is why the fixture manifest asserts a single set_distance_named with
    both slots filled. Joining here is therefore not a convenience: it is what the package
    actually does, and it is the level Tests~/Fixtures/audio/manifest.json (F7) asserts at.
    """
    data = json.loads(Path(expectations_path).read_text())
    out = []
    for case in data["cases"]:
        joined = " ".join(f for f in case.get("expectedFinals", []) if f.strip())
        if joined.strip():
            out.append((case["file"], joined))
    return out


def variants(finals):
    """Single-word deletions. [unk] is never deleted: the scorer never charges it, so
    removing it is not the degradation under study."""
    out = []
    for fixture, idx, text in finals:
        words = text.split()
        for i, w in enumerate(words):
            if w == UNK:
                continue
            ablated = " ".join(words[:i] + words[i + 1 :])
            if ablated.strip():
                out.append((fixture, idx, text, w, ablated))
    return out


def run(rig_dir, lines):
    """Replay lines through a staged build; returns {line: (count, [(intent, score)])}."""
    proc = subprocess.run(
        ["dotnet", "run", "--no-build", "--"],
        cwd=rig_dir,
        input="\n".join(lines) + "\n",
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        sys.exit(f"rig failed in {rig_dir}:\n{proc.stderr}")

    parsed = {}
    for row in proc.stdout.splitlines():
        parts = row.split("\t")
        if len(parts) != 3:
            continue
        line, count, payload = parts
        winners = []
        if payload.strip():
            for entry in payload.split(" | "):
                intent, score, _slots = entry.split(":", 2)
                winners.append((intent, float(score)))
        parsed[line] = (int(count), winners)
    return parsed


def top(result):
    """The round-1 winner -- the only candidate a user can see at default settings."""
    count, winners = result
    return winners[0] if winners else (None, 0.0)


def fmt(v):
    return f"{v:.4f}"


def main():
    if len(sys.argv) < 4:
        sys.exit(__doc__)
    before_dir, after_dir, expectations = sys.argv[1:4]
    out_path = None
    if "--out" in sys.argv:
        out_path = sys.argv[sys.argv.index("--out") + 1]

    finals = transcripts(expectations)
    joined = utterances(expectations)
    vs = variants(finals)

    ablated = [v[4] for v in vs]
    all_lines = list(
        dict.fromkeys([t[2] for t in finals] + [j[1] for j in joined] + ablated)
    )

    before = run(before_dir, all_lines)
    after = run(after_dir, all_lines)

    lines = []
    w = lines.append

    w("# Phase 4 — A/B measurement, `feat-fidelity-miss-cost` (#65 §5.1)")
    w("")
    w(f"- **before:** `{(Path(before_dir) / 'STAGED_FROM').read_text().strip().splitlines()[0]}`")
    w(f"- **after:** `{(Path(after_dir) / 'STAGED_FROM').read_text().strip().splitlines()[0]}`")
    w(f"- **gate:** default `minScore` = {MIN_SCORE}")
    w("")

    # ---- 4A ----
    w("## 4A — Regression: the committed corpus, unmodified")
    w("")
    w("### 4A-i — as the parser sees it (fixture level, F7)")
    w("")
    moved = []
    for fixture, text in joined:
        b, a = top(before[text]), top(after[text])
        if b != a or before[text][0] != after[text][0]:
            moved.append((fixture, text, b, a))
    w(f"Replayed **{len(joined)}** fixtures (`silence_negative` has no final and "
      f"contributes none; `split_…`'s two finals are rejoined, as the utterance buffer "
      f"rejoins them before parsing).")
    w("")
    if moved:
        w("**NON-ZERO DELTA — investigate before trusting anything below.**")
        w("")
        w("| fixture | utterance | before | after |")
        w("|---|---|---|---|")
        for fixture, text, b, a in moved:
            w(f"| `{fixture}` | `{text}` | {b[0]} {fmt(b[1])} | {a[0]} {fmt(a[1])} |")
    else:
        w("**Delta is exactly zero.** Every fixture is an exact phrase match at this level, "
          "so no required literal is missed and the constant is never reached. This is the "
          "regression check passing — and it is precisely why it is not evidence ABOUT the "
          "change, which is what 4B is for. It also answers **F7**: 0 of the 16 cases in "
          "`Tests~/Fixtures/audio/manifest.json` change `expectedIntent` or `expectedSlots`.")
    w("")

    w("### 4A-ii — bridge-level finals, replayed individually")
    w("")
    w("The design (§7, A1.4) states that *all* committed transcripts are exact phrase "
      "matches. That is true of 15 of the 16 fixtures. It is **not** true of `split_…`, "
      "whose two finals are each half of one command by construction — so the corpus is "
      "not quite as inert as the design assumed. Recorded here because the claim was load-"
      "bearing in the argument that F8's original plan was vacuous.")
    w("")
    split_moved = []
    for fixture, idx, text in finals:
        b, a = top(before[text]), top(after[text])
        if b != a:
            split_moved.append((fixture, text, b, a))
    if split_moved:
        w("| fixture | final | before | after | crosses 0.6? |")
        w("|---|---|---|---|---|")
        for fixture, text, b, a in split_moved:
            crossed = "**yes**" if b[1] < MIN_SCORE <= a[1] else "no"
            w(f"| `{fixture}` | `{text}` | {b[0] or '—'} {fmt(b[1])} | "
              f"{a[0] or '—'} {fmt(a[1])} | {crossed} |")
        w("")
        w("These halves only reach the parser in isolation if the pause exceeds the buffer "
          "window — the case the fixture exists to exercise. Where it does, `target hotel "
          "one` now clears the gate as `approach_target`. That is §5.1 behaving exactly as "
          "designed (a 3-element pattern, one dropped literal, `2/3`), applied to half a "
          "command; it is named here rather than buried because it is the one place the "
          "committed corpus shows the change at all.")
    else:
        w("_No individual final moves._")
    w("")

    # ---- 4B ----
    w("## 4B — Single-word ablation of those same transcripts")
    w("")
    newly_fire, changed_winner, newly_emitted, unchanged, still_below = [], [], [], [], []
    for fixture, idx, text, dropped, ablated_text in vs:
        b, a = top(before[ablated_text]), top(after[ablated_text])
        row = (fixture, text, dropped, ablated_text, b, a)
        if b == a:
            unchanged.append(row)
        elif b[1] < MIN_SCORE <= a[1]:
            # The only class a user at default settings can observe.
            newly_fire.append(row)
        elif b[0] is None:
            # Crossed the parse loop's `<= 0f` floor into visibility as an extra
            # sub-threshold candidate. Amendment A1 ruling 2 accepted exactly this.
            newly_emitted.append(row)
        elif b[0] != a[0]:
            changed_winner.append(row)
        else:
            still_below.append(row)

    w(f"**{len(vs)}** variants — every word of every committed transcript deleted once, "
      f"except the single `[unk]` token (the scorer never charges it, so removing it is "
      f"not the degradation under study).")
    w("")
    w("| outcome | count |")
    w("|---|---:|")
    w(f"| **newly clears `minScore`** (was rejected, now fires) | **{len(newly_fire)}** |")
    w(f"| winning intent changes among visible results | **{len(changed_winner)}** |")
    w(f"| newly emitted as a sub-threshold candidate (A1 ruling 2 residue) | "
      f"{len(newly_emitted)} |")
    w(f"| score rises, still under the gate | {len(still_below)} |")
    w(f"| unchanged | {len(unchanged)} |")
    w("")
    w(f"Only the first row is observable at default settings: **{len(newly_fire)} of "
      f"{len(vs)}** ablated real transcripts go from silently rejected to recognised.")
    w("")

    def table(title, rows, note=""):
        w(f"### {title}")
        w("")
        if note:
            w(note)
            w("")
        if not rows:
            w("_None._")
            w("")
            return
        w("| fixture | dropped | ablated utterance | before | after | Δ |")
        w("|---|---|---|---|---|---:|")
        for fixture, text, dropped, ablated_text, b, a in sorted(
            rows, key=lambda r: -(r[5][1] - r[4][1])
        ):
            bi = f"{b[0] or '—'} {fmt(b[1])}"
            ai = f"{a[0] or '—'} {fmt(a[1])}"
            w(f"| `{fixture}` | `{dropped}` | `{ablated_text}` | {bi} | {ai} | "
              f"+{fmt(a[1] - b[1])} |")
        w("")

    # A rescue is only a rescue if it recovers the intent the FULL utterance would have
    # produced. Where the deleted word was the discriminator between sibling patterns, the
    # rescue fires a different command instead — which is a materially different claim and
    # must not be counted as a win.
    faithful, misdirected = [], []
    for row in newly_fire:
        _fixture, source_text, _dropped, _ablated_text, _b, a = row
        intended = top(after[source_text])[0]
        (faithful if a[0] == intended else misdirected).append((row, intended))

    table("Newly clears the gate — recovers the intended command", [r for r, _ in faithful],
          f"**{len(faithful)} of {len(newly_fire)}** rescues fire the same intent the "
          f"undamaged transcript fires. These are the utterances the feature exists for: a "
          f"real transcript minus one word, silently rejected before, correctly recognised "
          f"after.")

    w("### Newly clears the gate — fires a DIFFERENT command")
    w("")
    if not misdirected:
        w("_None._")
        w("")
    else:
        w(f"**{len(misdirected)} of {len(newly_fire)}.** This is the sharpest edge of §5.1 "
          f"and it is reported separately because it is not a win. Where the dropped word "
          f"is the *discriminator* between sibling patterns, the surviving evidence fits "
          f"both siblings equally, the score clears the gate, and registration order picks "
          f"the winner — so a command fires where nothing fired before, and it may be the "
          f"wrong one. Note this is the FLUSH path: issue #70's tail guard closed exactly "
          f"this shape at the EAGER gate, but a final transcript that genuinely ends there "
          f"is not in progress and no tail rule applies.")
        w("")
        w("| fixture | dropped | ablated utterance | fires | undamaged utterance fires | "
          "after |")
        w("|---|---|---|---|---|---:|")
        for (fixture, source_text, dropped, ablated_text, b, a), intended in misdirected:
            w(f"| `{fixture}` | `{dropped}` | `{ablated_text}` | **{a[0]}** | "
              f"{intended} | {fmt(a[1])} |")
        w("")
    table("Winning intent changes", changed_winner,
          "Reordering among already-visible results. Each one needs arguing on its own "
          "merits; an empty table here means selection order was genuinely untouched.")
    table("Newly emitted sub-threshold candidates", newly_emitted,
          "The accepted residue of Amendment A1 ruling 2: raising `rawScore` by 0.5 per "
          "miss lifts these over the parse loop's `<= 0f` floor, so they appear as extra "
          "low-scoring results in later extraction rounds. All are far below `minScore`, so "
          "nothing changes at recogniser level — but a user running `minScore` under ~0.35 "
          "would start seeing them.")
    table("Score rises, still rejected", still_below,
          "The cost is halved, not abolished — these still fail, which is §5.1 working as "
          "designed rather than a partial fix.")

    text_out = "\n".join(lines) + "\n"
    if out_path:
        Path(out_path).write_text(text_out)
        print(f"wrote {out_path}")
    print(text_out)


if __name__ == "__main__":
    main()
