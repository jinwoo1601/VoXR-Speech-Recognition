import collections
CORPUS = "/mnt/d/Game Development/VoXR-Speech-Recognition/Planning~/features/coverage-in-selection/phase7-corpus.tsv"
MIN = 0.60
cats = [l.rstrip("\n").split("\t")[0] for l in open(CORPUS, encoding="utf-8")]

def load(path, gate):
    rows = []
    for line in open(path, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        payload = f[2] if len(f) > 2 else ""
        out = []
        if payload:
            for part in payload.split(" | "):
                if not part: continue
                intent, score, slots = part.split(":", 2)
                if not gate or float(score) >= MIN:
                    out.append((intent, round(float(score), 6), slots))
        rows.append((f[0], out))
    return rows

g = {a: load("arm%d.tsv" % a, True) for a in (0,1,2)}
u = {a: load("arm%d.tsv" % a, False) for a in (0,1,2)}

lost_fire, score_only, both = [], [], []
for i in range(len(cats)):
    ga, gb = g[0][i][1], g[1][i][1]
    if ga == gb: continue
    ia = collections.Counter(x[0] for x in ga)
    ib = collections.Counter(x[0] for x in gb)
    if ia != ib:
        # an intent stopped (or started) firing
        (lost_fire if not (set(ib) - set(ia)) else both).append(i)
    else:
        score_only.append(i)

print("arm0 -> arm1 (uniform), gated at %.2f: %d rows change" % (MIN, len(lost_fire)+len(score_only)+len(both)))
print("  %3d rows: a command STOPS firing (the bar's intended effect)" % len(lost_fire))
print("  %3d rows: same commands fire, but the SURVIVOR'S SCORE DROPS" % len(score_only))
print("  %3d rows: a new intent appears (rival won the round)" % len(both))
print()
print("  score-drop rows by category:", dict(collections.Counter(cats[i] for i in score_only)))
print("  stop-firing rows by category:", dict(collections.Counter(cats[i] for i in lost_fire)))
print()

print("=== ALL %d score-drop rows (the effect refuse-to-FIRE did not have) ===" % len(score_only))
for i in score_only:
    a = g[0][i][1]; b = g[1][i][1]
    print("  [%-8s] %-46s  %s  ->  %s" % (
        cats[i], g[0][i][0],
        " | ".join("%s:%.4f" % (x[0], x[1]) for x in a),
        " | ".join("%s:%.4f" % (x[0], x[1]) for x in b)))
print()

# How close to the gate do the survivors land?
margins = sorted(min(x[1] for x in g[1][i][1]) for i in score_only)
print("  survivor scores after the drop: min %.4f, max %.4f (gate %.2f)" % (margins[0], margins[-1], MIN))
print()

# Did any row lose a command ENTIRELY (n -> 0)?
to_zero = [i for i in lost_fire if not g[1][i][1]]
print("=== rows that now fire NOTHING (n=0 -> OnUnrecognisedSpeech): %d ===" % len(to_zero))
for i in to_zero:
    print("  [%-8s] %-46s  was: %s" % (cats[i], g[0][i][0],
        " | ".join("%s:%.4f" % (x[0], x[1]) for x in g[0][i][1])))
