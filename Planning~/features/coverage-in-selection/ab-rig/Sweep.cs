// Re-runs architecture.md §9.9's exhaustive start-index sweep, which produced the
// "29 blocked, 28 recovered, 1 genuinely silent" figure. Re-swept twice since: after #82
// (28/27/1) and after the #124 bar (28/27/1, unchanged) — the latter is what ships today.
//
// Enumerates every (command, pattern, startIdx) for round 1 (searchStart = 0) over the
// corpus on stdin, finds the round-1 winner, and reports utterances where that winner is
// under the gate while some LATER-starting admissible candidate is over it.
//
// The winner is picked by a local restatement of IsBetterCandidate's key order, which is
// self-checked against Parse()'s actual first result on every utterance — a mismatch is
// printed loudly, so the restatement cannot silently drift from the shipped rule.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using VoXR.Commands;

namespace AbRig
{
    static class Sweep
    {
        const float Gate = 0.6f;

        static readonly MethodInfo TryMatchScored = typeof(VoxrCommandParser).GetMethod(
            "TryMatchScored",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        static readonly MethodInfo BuildTables = typeof(VoxrCommandParser).GetMethod(
            "BuildCoverageTables",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        static object Match(VoxrCommandParser p, string[] tokens, int startIdx, string[] pattern)
        {
            var ps = TryMatchScored.GetParameters();
            var argv = new object[ps.Length];
            argv[0] = tokens;
            argv[1] = startIdx;
            argv[2] = pattern;
            argv[3] = 0;
            for (int i = 4; i < argv.Length; i++)
                argv[i] = Type.Missing;
            return TryMatchScored.Invoke(p, argv);
        }

        static T Field<T>(object mr, string name) => (T)mr.GetType().GetField(name).GetValue(mr);

        // The bar-off arm predates the field, so this must not throw there.
        static bool LeadingMissed(object mr)
        {
            var f = mr.GetType().GetField("LeadingRequiredMissed");
            return f != null && (bool)f.GetValue(mr);
        }

        sealed class Cand
        {
            public string Intent;
            public int StartIdx;
            public float Score;
            public int ConsumedEndIdx;
            public int LiteralCount;
            public bool LeadingMissed;
        }

        internal static void Run()
        {
            // Reuse Program's demo grammar verbatim (its private statics), so the sweep is
            // measured against the same grammar the A/B replay and the committed pin use.
            object Call(string name) =>
                typeof(Program)
                    .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);

            var slots = (VoxrSlotDefinition[])Call("Slots");
            var defs = (VoxrCommandDefinition[])Call("Commands");
            var followUp = (string[])Call("FollowUpWords");

            var parser = new VoxrCommandParser(
                slots,
                defs,
                VoxrCommandParser.DefaultCoverageWeight,
                followUp
            );

            int blocked = 0,
                recovered = 0,
                silent = 0,
                total = 0,
                winnerMismatch = 0,
                barredWinner = 0;
            var silentList = new List<string>();
            var blockedList = new List<string>();

            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;
                total++;
                var tokens = line.Split(' ');
                BuildTables.Invoke(parser, new object[] { tokens });

                Cand best = null;
                var all = new List<Cand>();

                for (int ci = 0; ci < defs.Length; ci++)
                {
                    var patterns = defs[ci].Patterns;
                    for (int pi = 0; pi < patterns.Length; pi++)
                    {
                        for (int s = 0; s < tokens.Length; s++)
                        {
                            if (tokens[s] == "[unk]")
                                continue;
                            var mr = Match(parser, tokens, s, patterns[pi]);
                            float score = Field<float>(mr, "Score");
                            if (score <= 0f)
                                continue;
                            if (
                                Field<int>(mr, "MissedRequired") > Field<int>(mr, "MatchedRequired")
                            )
                                continue;

                            var c = new Cand
                            {
                                Intent = defs[ci].Intent,
                                StartIdx = s,
                                Score = score,
                                ConsumedEndIdx = Field<int>(mr, "ConsumedEndIdx"),
                                LiteralCount = Field<int>(mr, "LiteralCount"),
                                LeadingMissed = LeadingMissed(mr),
                            };
                            all.Add(c);

                            if (best == null)
                            {
                                best = c;
                                continue;
                            }
                            if (c.StartIdx != best.StartIdx)
                            {
                                if (c.StartIdx < best.StartIdx)
                                    best = c;
                                continue;
                            }
                            if (c.Score != best.Score)
                            {
                                if (c.Score > best.Score)
                                    best = c;
                                continue;
                            }
                            if (c.ConsumedEndIdx != best.ConsumedEndIdx)
                            {
                                if (c.ConsumedEndIdx > best.ConsumedEndIdx)
                                    best = c;
                                continue;
                            }
                            if (c.LiteralCount > best.LiteralCount)
                                best = c;
                        }
                    }
                }

                if (best == null)
                    continue;

                // Self-check: the restated key order must name the same command Parse fires first.
                var parsed = parser.Parse(line);
                if (best.LeadingMissed)
                    barredWinner++;
                else if (parsed.Length > 0 && parsed[0].Command.Intent != best.Intent)
                {
                    winnerMismatch++;
                    Console.WriteLine(
                        $"WINNER-MISMATCH \"{line}\" sweep={best.Intent}@{best.StartIdx}:{best.Score.ToString("F3", CultureInfo.InvariantCulture)} parse={parsed[0].Command.Intent}:{parsed[0].Command.Score.ToString("F3", CultureInfo.InvariantCulture)}"
                    );
                }

                if (best.Score >= Gate)
                    continue;

                Cand over = null;
                foreach (var c in all)
                {
                    if (c.StartIdx > best.StartIdx && c.Score >= Gate)
                    {
                        if (over == null || c.Score > over.Score)
                            over = c;
                    }
                }
                if (over == null)
                    continue;

                blocked++;
                blockedList.Add(
                    $"  \"{line}\"  winner={best.Intent}@{best.StartIdx}:{best.Score.ToString("F3", CultureInfo.InvariantCulture)}  blocked={over.Intent}@{over.StartIdx}:{over.Score.ToString("F3", CultureInfo.InvariantCulture)}"
                );

                bool fired = false;
                foreach (var r in parsed)
                {
                    if (r.Command.Score >= Gate)
                        fired = true;
                }
                if (fired)
                    recovered++;
                else
                {
                    silent++;
                    silentList.Add(
                        $"  \"{line}\"  would-have={over.Intent}@{over.StartIdx}:{over.Score.ToString("F3", CultureInfo.InvariantCulture)}"
                    );
                }
            }

            Console.WriteLine($"\nutterances swept: {total}   gate {Gate}");
            Console.WriteLine($"winner restatement mismatches: {winnerMismatch}  (must be 0)");
            Console.WriteLine($"round-1 winner barred (self-check not applicable): {barredWinner}");
            Console.WriteLine($"round-1 winner under gate, later candidate over it: {blocked}");
            Console.WriteLine($"  recovered by a later extraction round: {recovered}");
            Console.WriteLine($"  genuinely silent: {silent}");
            foreach (var s in silentList)
                Console.WriteLine("GENUINELY SILENT" + s);
            Console.WriteLine("\nblocked detail:");
            foreach (var b in blockedList)
                Console.WriteLine(b);
        }
    }
}
