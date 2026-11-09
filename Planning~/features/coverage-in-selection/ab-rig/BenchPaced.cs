// ============================================================================
// Purpose:  The bench's paced arithmetic: stamps on both clocks and their split, the command stage's split, the real-time factor from the read trace, push lateness, the matched-chunk compare and its check over two dumps
// Layer:    Planning.AbRig
// Owns:     BenchPaced (internal static class), BenchPaced.Factor (internal sealed class)
// Depends:  Bench, BenchMetrics, BenchReport, Joined, JoinedInput
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AbRig
{
    static class BenchPaced
    {
        internal const long ReadSamples = 4096;
        internal const long PacedPadding = 48000;
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

        internal static void CheckMatched(JoinedInput.Dump fast, JoinedInput.Dump matched)
        {
            var byName = new Dictionary<string, JoinedInput.DumpCase>(StringComparer.Ordinal);
            foreach (JoinedInput.DumpCase c in matched.Cases)
                if (!byName.TryAdd(c.Name, c))
                    throw new JoinedInput.Stop(2, matched.Path + ": case \"" + c.Name + "\" twice");
            var inFast = new HashSet<string>(StringComparer.Ordinal);
            var onlyFast = new List<string>();
            foreach (JoinedInput.DumpCase c in fast.Cases)
            {
                inFast.Add(c.Name);
                if (!byName.ContainsKey(c.Name))
                    onlyFast.Add(c.Name);
            }
            var onlyMatched = new List<string>();
            foreach (JoinedInput.DumpCase c in matched.Cases)
                if (!inFast.Contains(c.Name))
                    onlyMatched.Add(c.Name);
            if (onlyFast.Count > 0 || onlyMatched.Count > 0)
                throw new JoinedInput.Stop(
                    2,
                    matched.Path
                        + ": cases differ from the fast dump's: only in fast "
                        + Joined.JsonArray(onlyFast.ToArray())
                        + ", only in matched "
                        + Joined.JsonArray(onlyMatched.ToArray())
                );

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
        }
    }
}
