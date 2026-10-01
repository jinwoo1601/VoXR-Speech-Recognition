import collections
CORPUS = "/mnt/d/Game Development/VoXR-Speech-Recognition/Planning~/features/coverage-in-selection/phase7-corpus.tsv"
MIN = 0.60
cats = [l.rstrip("\n").split("\t")[0] for l in open(CORPUS, encoding="utf-8")]

def load(path):
    rows = []
    for line in open(path, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        payload = f[2] if len(f) > 2 else ""
        out = []
        if payload:
            for part in payload.split(" | "):
                if not part: continue
                intent, score, slots = part.split(":", 2)
                if float(score) >= MIN:
                    out.append((intent, round(float(score), 6)))
        rows.append((f[0], out))
    return rows

A = {"OFF": load("arm0.tsv"), "FIRE": load("armFIRE.tsv"),
     "COMPETE": load("arm1.tsv"), "COMPETE+lead": load("arm3.tsv")}
off = A["OFF"]

print("%-14s %8s %8s %8s %10s" % ("arm", "changed", "n->0", "score-", "clean-lost"))
print("-" * 52)
detail = {}
for name in ("FIRE", "COMPETE", "COMPETE+lead"):
    arm = A[name]
    changed = n0 = drop = cleanlost = 0
    lost_rows = []
    for i in range(len(cats)):
        a, b = off[i][1], arm[i][1]
        if a == b: continue
        changed += 1
        if not b and a: n0 += 1
        ia = collections.Counter(x[0] for x in a); ib = collections.Counter(x[0] for x in b)
        if ia == ib: drop += 1
        # a command scoring >= 0.999 under OFF that no longer fires at all
        gone = [x for x in a if x[1] >= 0.999 and x[0] not in {y[0] for y in b}]
        if gone:
            cleanlost += 1
            lost_rows.append((i, off[i][0], a, b))
    detail[name] = lost_rows
    print("%-14s %8d %8d %8d %10d" % (name, changed, n0, drop, cleanlost))

print()
print("clean-lost = rows where a command that scored 1.0000 under OFF stops firing entirely.")
print()
for name in ("FIRE", "COMPETE", "COMPETE+lead"):
    rows = detail[name]
    print("=== %s : %d clean commands destroyed ===" % (name, len(rows)))
    for i, utt, a, b in rows[:40]:
        fa = " | ".join("%s:%.4f" % x for x in a) or "(none)"
        fb = " | ".join("%s:%.4f" % x for x in b) or "(none)"
        print("  [%-8s] %-48s %s  ->  %s" % (cats[i], utt, fa, fb))
    print()

# Does COMPETE+lead keep every phantom suppressed that COMPETE did?
c, cl = A["COMPETE"], A["COMPETE+lead"]
diff = [i for i in range(len(cats)) if c[i][1] != cl[i][1]]
print("=== COMPETE vs COMPETE+lead : %d rows differ ===" % len(diff))
for i in diff[:40]:
    fa = " | ".join("%s:%.4f" % x for x in c[i][1]) or "(none)"
    fb = " | ".join("%s:%.4f" % x for x in cl[i][1]) or "(none)"
    print("  [%-8s] %-48s %s  ->  %s" % (cats[i], c[i][0], fa, fb))
