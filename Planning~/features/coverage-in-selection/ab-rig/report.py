#!/usr/bin/env python3
"""Both-directions A/B report for issue #65 §5.2 (requirements F14).

F14 is a hard requirement, not a reporting nicety: measuring only the flattering direction is
the failure mode that cost this design two G1 reopens. So the columns are fixed here, in the
script, BEFORE the numbers are known, and every one of them is printed even when it is zero --
a zero is only admissible as a stated measured result, never as an unexamined default.

Everything is judged at the recogniser's gate (minScore 0.6), not at the parser's raw output,
because a result below the gate does not fire and so is not user-visible.

  newly-firing   nothing cleared the gate before, something does now
  newly-silent   something cleared the gate before, nothing does now
  intent-change  a different command is the top firing result
  slots-gained   the same intent fires carrying MORE arguments than before
  slots-lost     the same intent fires carrying FEWER arguments than before
  count-change   a different NUMBER of commands clears the gate (extraction split/merged)
  score-only     the top firing result is the same command with the same slots
"""
import sys
import collections

MIN_SCORE = 0.6


def parse(path):
    rows = {}
    for line in open(path):
        parts = line.rstrip("\n").split("\t")
        utt, _count, payload = parts[0], parts[1], parts[2]
        results = []
        if payload:
            for chunk in payload.split(" | "):
                intent, score, slots = chunk.split(":", 2)
                results.append((intent, float(score), slots))
        rows[utt] = results
    return rows


def firing(results):
    return [r for r in results if r[1] >= MIN_SCORE]


def classify(before, after):
    fb, fa = firing(before), firing(after)
    if not fb and not fa:
        return None
    if not fb:
        return "newly-firing"
    if not fa:
        return "newly-silent"
    if fb[0][0] != fa[0][0]:
        return "intent-change"
    if len(fb) != len(fa):
        return "count-change"
    nb = 0 if not fb[0][2] else len(fb[0][2].split(","))
    na = 0 if not fa[0][2] else len(fa[0][2].split(","))
    if na > nb:
        return "slots-gained"
    if na < nb:
        return "slots-lost"
    if fb[0][1] != fa[0][1]:
        return "score-only"
    return None


def main():
    families = {}
    for line in open(sys.argv[1]):
        fam, text = line.rstrip("\n").split("\t", 1)
        families[text] = fam

    before, after = parse(sys.argv[2]), parse(sys.argv[3])

    buckets = collections.defaultdict(list)
    per_family = collections.defaultdict(collections.Counter)

    for utt in before:
        kind = classify(before[utt], after[utt])
        fam = families.get(utt, "?")
        per_family[fam]["total"] += 1
        if kind:
            buckets[kind].append((fam, utt, before[utt], after[utt]))
            per_family[fam][kind] += 1

    order = [
        "newly-firing",
        "slots-gained",
        "intent-change",
        "count-change",
        "slots-lost",
        "newly-silent",
        "score-only",
    ]

    print(f"utterances compared: {len(before)}   gate: minScore {MIN_SCORE}\n")
    print("=" * 78)
    print("BOTH DIRECTIONS — every column printed, including the zeros")
    print("=" * 78)
    for kind in order:
        print(f"  {kind:16} {len(buckets[kind])}")

    print("\nBY GENERATION FAMILY")
    header = f"  {'family':10}{'total':>7}" + "".join(f"{k[:11]:>13}" for k in order)
    print(header)
    for fam in sorted(per_family):
        c = per_family[fam]
        print(
            f"  {fam:10}{c['total']:>7}" + "".join(f"{c[k]:>13}" for k in order)
        )

    def fmt(results):
        if not results:
            return "(nothing)"
        return " | ".join(
            f"{i}:{s:.3f}{'' if not sl else ' [' + sl + ']'}" for i, s, sl in results
        )

    for kind in order:
        if kind == "score-only":
            continue  # listed by count only; the interesting ones are the behavioural moves
        if not buckets[kind]:
            print(f"\n### {kind}: none")
            continue
        print(f"\n### {kind}  ({len(buckets[kind])})")
        for fam, utt, b, a in buckets[kind]:
            print(f"  [{fam}] {utt!r}")
            print(f"      before: {fmt(b)}")
            print(f"      after : {fmt(a)}")


if __name__ == "__main__":
    main()
