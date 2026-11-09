// ============================================================================
// Purpose:  The bench's paced arithmetic: stamps on both clocks and their split, the command stage's split, the real-time factor from the read trace, push lateness, the matched-chunk compare and its check over two dumps, the wall view, the 20 ms finding and the paced section's assembly
// Layer:    Planning.AbRig
// Owns:     BenchPaced (internal static class), BenchPaced.Factor (internal sealed class)
// Depends:  Bench, BenchMetrics, BenchReport, Joined, JoinedInput, JoinedScorer, VoxrSlotDefinition, VoxrCommandDefinition
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using VoXR.Commands;

namespace AbRig
{
    static class BenchPaced
    {
        internal const long ReadSamples = 4096;
        internal const long PacedPadding = 49152;
        internal const string Unpaced = "unpaced";

        internal sealed class Factor
        {
            internal int Measured;
            internal int Merged;
            internal long Decode;
            internal long Samples;
        }

        internal static (string Audio, string Wall, string Compute) Split(
            long consumed,
            long wall,
            long e,
            long r,
            long clip
        )
        {
            string audio = BenchReport.Offset(consumed - e, r);
            if (consumed > clip + PacedPadding)
                return (audio, Unpaced, Unpaced);
            return (
                audio,
                BenchReport.Offset(wall * r - e * 1000000, 1000000 * r),
                BenchReport.Offset(wall * r - consumed * 1000000, 1000000 * r)
            );
        }

        internal static (string Audio, string Wall, string Compute) CommandSplit(
            long? fast,
            long? paced,
            long e,
            long r,
            long t
        )
        {
            string audio = fast.HasValue ? Bench.FrameOffset(fast.Value, e, r, t) : "none";
            string wall = paced.HasValue ? Bench.FrameOffset(paced.Value, e, r, t) : "none";
            string compute =
                fast.HasValue && paced.HasValue
                    ? BenchReport.Offset(paced.Value - fast.Value, t)
                    : "none";
            return (audio, wall, compute);
        }

        internal static Factor Reads(List<(long Consumed, long At, long Pushed)> reads, long clip)
        {
            var f = new Factor();
            var size = new long[reads.Count];
            for (int k = 0; k < reads.Count; k++)
            {
                size[k] = reads[k].Consumed - (k == 0 ? 0 : reads[k - 1].Consumed);
                if (size[k] > ReadSamples && reads[k].Consumed <= clip)
                    f.Merged++;
            }
            for (int k = 0; k + 1 < reads.Count; k++)
                if (
                    reads[k].Consumed <= clip
                    && size[k] <= ReadSamples
                    && size[k + 1] <= ReadSamples
                    && reads[k].Consumed < reads[k].Pushed
                )
                {
                    f.Measured++;
                    f.Decode += reads[k + 1].At - reads[k].At;
                    f.Samples += size[k];
                }
            return f;
        }

        internal static BenchMetrics.Value Ratio(long decode, long samples, long r) =>
            samples == 0
                ? BenchMetrics.Value.Undefined("no measured reads")
                : BenchMetrics.Value.Of((double)decode * r / (1000000.0 * samples));

        internal static BenchMetrics.Value Corpus(List<Factor> cases, long r)
        {
            long decode = 0;
            long samples = 0;
            foreach (Factor f in cases)
            {
                decode += f.Decode;
                samples += f.Samples;
            }
            return Ratio(decode, samples, r);
        }

        internal static string Lateness(List<(long End, long Due, long Made)> pushes)
        {
            long max = long.MinValue;
            foreach ((long End, long Due, long Made) p in pushes)
                max = Math.Max(max, p.Made - p.Due);
            return (max / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " ms";
        }

        internal static string Matched(JoinedInput.DumpCase fast, JoinedInput.DumpCase matched)
        {
            string at = "matched-check: case \"" + fast.Name + "\": ";
            var f = Bench.Selected(fast);
            var m = Bench.Selected(matched);
            string[] a = f.Finals.ConvertAll(x => x.Text).ToArray();
            string[] b = m.Finals.ConvertAll(x => x.Text).ToArray();
            if (Joined.FirstDifference(a, b) >= 0)
                return at
                    + "finals fast "
                    + Joined.JsonArray(a)
                    + ", matched "
                    + Joined.JsonArray(b);
            for (int i = 0; i < a.Length; i++)
            {
                long ca = f.Finals[i].Result.Consumed.Value;
                long cb = m.Finals[i].Result.Consumed.Value;
                if (Math.Abs(ca - cb) > ReadSamples)
                    return at
                        + "final "
                        + (i + 1)
                        + " \""
                        + a[i]
                        + "\" at consumed fast "
                        + ca
                        + ", matched "
                        + cb
                        + ": more than 4096 apart";
            }

            string Shown(JoinedInput.DumpResult x) =>
                x == null ? "none" : x.Consumed.Value.ToString(CultureInfo.InvariantCulture);

            JoinedInput.DumpResult pa = f.FirstPartial;
            JoinedInput.DumpResult pb = m.FirstPartial;
            if ((pa == null) != (pb == null))
                return at + "first partial at consumed fast " + Shown(pa) + ", matched " + Shown(pb);
            if (pa != null && Math.Abs(pa.Consumed.Value - pb.Consumed.Value) > ReadSamples)
                return at
                    + "first partial at consumed fast "
                    + Shown(pa)
                    + ", matched "
                    + Shown(pb)
                    + ": more than 4096 apart";
            return null;
        }

        internal static Dictionary<string, JoinedInput.DumpCase> Paired(
            JoinedInput.Dump fast,
            JoinedInput.Dump other,
            string side
        )
        {
            var byName = new Dictionary<string, JoinedInput.DumpCase>(StringComparer.Ordinal);
            foreach (JoinedInput.DumpCase c in other.Cases)
                if (!byName.TryAdd(c.Name, c))
                    throw new JoinedInput.Stop(2, other.Path + ": case \"" + c.Name + "\" twice");
            var inFast = new HashSet<string>(StringComparer.Ordinal);
            var onlyFast = new List<string>();
            foreach (JoinedInput.DumpCase c in fast.Cases)
            {
                inFast.Add(c.Name);
                if (!byName.ContainsKey(c.Name))
                    onlyFast.Add(c.Name);
            }
            var onlyMatched = new List<string>();
            foreach (JoinedInput.DumpCase c in other.Cases)
                if (!inFast.Contains(c.Name))
                    onlyMatched.Add(c.Name);
            if (onlyFast.Count > 0 || onlyMatched.Count > 0)
                throw new JoinedInput.Stop(
                    2,
                    other.Path
                        + ": cases differ from the fast dump's: only in fast "
                        + Joined.JsonArray(onlyFast.ToArray())
                        + ", only in "
                        + side
                        + " "
                        + Joined.JsonArray(onlyMatched.ToArray())
                );
            return byName;
        }

        internal static Dictionary<string, JoinedInput.DumpCase> CheckMatched(
            JoinedInput.Dump fast,
            JoinedInput.Dump matched
        )
        {
            Dictionary<string, JoinedInput.DumpCase> byName = Paired(fast, matched, "matched");
            var failing = new List<string>();
            foreach (JoinedInput.DumpCase f in fast.Cases)
            {
                JoinedInput.DumpCase m = byName[f.Name];
                string line =
                    m.Failures.Count > 0
                        ? "matched-check: case \""
                            + f.Name
                            + "\": failed decode in the matched dump: "
                            + string.Join("; ", m.Failures)
                        : Matched(f, m);
                if (line != null)
                    failing.Add(line);
            }
            if (failing.Count > 0)
            {
                int failed = failing.Count;
                failing.Add(
                    "matched-check failed on "
                        + failed
                        + "/"
                        + fast.Cases.Count
                        + " cases against the fast dump "
                        + fast.Path
                        + ": no numbers, no report"
                );
                throw new JoinedInput.Stop(3, failing.ToArray());
            }
            return byName;
        }

        internal static JoinedInput.DumpCase WallView(JoinedInput.DumpCase d)
        {
            var v = new JoinedInput.DumpCase
            {
                Name = d.Name,
                SampleRate = 1000000,
                Samples = d.Samples * 1000000 / d.SampleRate,
            };
            v.Finals.AddRange(d.Finals);
            foreach (JoinedInput.DumpResult x in d.Results)
                v.Results.Add(
                    new JoinedInput.DumpResult
                    {
                        Seq = x.Seq,
                        Final = x.Final,
                        Consumed = x.Wall,
                        Origin = x.Origin,
                        Payload = x.Payload,
                        Raw = x.Raw,
                        Wall = x.Wall,
                    }
                );
            return v;
        }

        internal static List<string> Finding(
            string name,
            JoinedInput.DumpCase fast,
            JoinedInput.DumpCase twenty,
            string fastClass,
            string twentyClass
        )
        {
            string at = "case \"" + name + "\": ";
            var lines = new List<string>();
            if (twenty.Failures.Count > 0)
                lines.Add(
                    at + "failed decode in the 20 ms dump: " + string.Join("; ", twenty.Failures)
                );
            string[] a = Bench.Selected(fast).Finals.ConvertAll(x => x.Text).ToArray();
            string[] b = Bench.Selected(twenty).Finals.ConvertAll(x => x.Text).ToArray();
            if (Joined.FirstDifference(a, b) >= 0)
                lines.Add(
                    at + "finals fast " + Joined.JsonArray(a) + ", 20 ms " + Joined.JsonArray(b)
                );
            if (fastClass != twentyClass)
                lines.Add(at + "class fast " + fastClass + ", 20 ms " + twentyClass);
            return lines;
        }

        internal static BenchReport.Paced Assemble(
            JoinedInput.Manifest manifest,
            Dictionary<string, JoinedInput.DumpCase> dumpByCase,
            Dictionary<string, JoinedInput.DumpCase> matchedByCase,
            Dictionary<string, JoinedInput.DumpCase> pacedByCase,
            List<JoinedScorer.Outcome> outcomes,
            JoinedInput.Pin pin,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands,
            long t,
            long pace,
            int matchedExit,
            int pacedExit
        )
        {
            var section = new BenchReport.Paced
            {
                Pace = pace,
                MatchedExit = matchedExit,
                PacedExit = pacedExit,
            };
            var factors = new List<Factor>();
            for (int i = 0; i < manifest.Cases.Count; i++)
            {
                JoinedInput.ManifestCase c = manifest.Cases[i];
                JoinedInput.DumpCase p = pacedByCase[c.Name];
                JoinedInput.DumpCase m = matchedByCase[c.Name];
                JoinedScorer.Outcome s = outcomes[i];
                JoinedScorer.Outcome w = Joined.ScoreRecogniser(c, WallView(p), slots, commands);
                Joined.AppliedGrammarGuard(c.Name, w.AppliedGrammar, pin);
                if (c.Speech.Length == 0)
                {
                    section.Split.Add((c.Name, "first partial", "n/a", "n/a", "n/a"));
                    section.Split.Add((c.Name, "finals", "n/a", "n/a", "n/a"));
                    section.Split.Add((c.Name, "command event", "n/a", "n/a", "n/a"));
                }
                else
                {
                    long e = c.Speech[^1].End;
                    long r = p.SampleRate;
                    long clip = p.Samples;
                    var selected = Bench.Selected(p);
                    JoinedInput.DumpResult first = selected.FirstPartial;
                    (string Audio, string Wall, string Compute) partial =
                        first == null
                            ? ("none", "none", "none")
                            : Split(first.Consumed.Value, first.Wall.Value, e, r, clip);
                    section.Split.Add(
                        (c.Name, "first partial", partial.Audio, partial.Wall, partial.Compute)
                    );
                    if (selected.Finals.Count == 0)
                        section.Split.Add((c.Name, "finals", "none", "none", "none"));
                    for (int k = 0; k < selected.Finals.Count; k++)
                    {
                        JoinedInput.DumpResult x = selected.Finals[k].Result;
                        (string Audio, string Wall, string Compute) f = Split(
                            x.Consumed.Value,
                            x.Wall.Value,
                            e,
                            r,
                            clip
                        );
                        section.Split.Add((c.Name, "final " + (k + 1), f.Audio, f.Wall, f.Compute));
                    }
                    (string Audio, string Wall, string Compute) command = CommandSplit(
                        Bench.FireFrame(s, t),
                        Bench.FireFrame(w, t),
                        e,
                        r,
                        t
                    );
                    section.Split.Add(
                        (c.Name, "command event", command.Audio, command.Wall, command.Compute)
                    );
                }
                Factor read = Reads(m.Reads, m.Samples);
                factors.Add(read);
                section.Factors.Add(
                    (
                        c.Name,
                        Ratio(read.Decode, read.Samples, m.SampleRate).ToString(),
                        read.Measured,
                        read.Merged,
                        m.Pushes.Count == 0 ? "undefined (no pushes)" : Lateness(m.Pushes),
                        p.Pushes.Count == 0 ? "undefined (no pushes)" : Lateness(p.Pushes)
                    )
                );
                section.Finding.AddRange(Finding(c.Name, dumpByCase[c.Name], p, s.Class, w.Class));
            }
            section.Corpus = Corpus(factors, 48000).ToString();
            return section;
        }
    }
}
