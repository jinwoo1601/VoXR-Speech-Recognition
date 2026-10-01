#!/usr/bin/env python3
"""Corpus generation for the issue #65 §5.2 A/B (requirements F14/F15).

Phase 0 established that the 16 committed transcripts cannot exercise this feature at all:
every one begins its match at the first recognised token and consumes through to the end, so
there are zero leading skips and zero trailing orphans and the A/B over them is identically
zero by construction. It stays a valid REGRESSION check -- a non-zero delta there would mean
something out of scope moved -- but it is not evidence about this change.

Item 1's inherited ablate.py generates single-word DELETIONS, which shorten the utterance and
so cannot manufacture an orphan directly; deletion only reaches the phenomenon indirectly,
where dropping a word lets a shorter sibling win and strand the rest. Leading skips need
in-grammar tokens BEFORE a match and trailing orphans need in-grammar tokens AFTER it.

So this generates four families. Deletion is inherited; the other three are what Phase 0 found
missing:

  delete  -- single-word deletion (the inherited ablation, kept for continuity)
  append  -- an in-grammar word after a real transcript   -> trailing orphans
  prepend -- an in-grammar word before a real transcript  -> leading skips
  concat  -- two real transcripts joined                  -> the multi-command guard

The probe words are chosen to span the predicate rather than to flatter it: some can begin a
pattern in the demo grammar and some cannot, because whether a leftover token STOPS the orphan
run is exactly the thing under test.
"""
import sys

# Words that CANNOT begin any demo-grammar pattern, so they are charged as orphans.
NON_STARTERS = ["target", "hotel", "one", "missiles", "distance", "heading"]

# Words that CAN begin a pattern, so the orphan run stops at them and nothing is charged.
STARTERS = ["cease", "launch", "disengage", "switch"]

# Not in the grammar at all. Real decoder output would deliver these as [unk], but
# freeSpeechMode, InjectText and VoxrBatchTestRunner all deliver them verbatim -- which is the
# F5 overclaim recorded as M-6, and the trailing side is newly exposed to it.
OUT_OF_GRAMMAR = ["please", "now"]

PROBES = NON_STARTERS + STARTERS + OUT_OF_GRAMMAR


def main():
    transcripts = [l.split() for l in open(sys.argv[1]) if l.split()]
    seen = set()
    rows = []

    def add(family, tokens):
        text = " ".join(tokens)
        if text and (family, text) not in seen:
            seen.add((family, text))
            rows.append((family, text))

    for toks in transcripts:
        add("baseline", toks)

        for d in range(len(toks)):
            add("delete", toks[:d] + toks[d + 1:])

        for w in PROBES:
            add("append", toks + [w])
            add("prepend", [w] + toks)

    for a in transcripts:
        for b in transcripts:
            if a is not b:
                add("concat", a + b)

    for family, text in rows:
        print(f"{family}\t{text}")


if __name__ == "__main__":
    main()
