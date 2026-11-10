// ============================================================================
// Purpose:  The bench's conditioned scoring: the cond/ inputs read and checked, per-condition curves and the negative set's false fires, assembled into a report section, the SIR tables
// Layer:    Planning.AbRig
// Owns:     BenchCurves (internal static class), BenchCurves.Inputs (internal sealed class), BenchCurves.Set (internal sealed class), BenchCurves.Entry (internal sealed class)
// Depends:  BenchConditions, BenchMetrics, BenchReport, JoinedInput, JoinedScorer
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AbRig
{
    static class BenchCurves
    {
        internal sealed class Inputs
        {
            internal string DumpPath;
            internal string ManifestPath;
            internal string ConditionsPath;
            internal int Exit;
            internal string Condition;
            internal ulong Seed;
            internal List<BenchConditions.Step> Steps;
            internal long? Pace;
            internal JoinedInput.Dump Dump;
            internal JoinedInput.Manifest Manifest;
            internal JsonElement Sidecar;
            internal Dictionary<string, (string Source, string Condition)> Clips;
            internal long EndCases;
            internal long EndPassed;
        }

        internal sealed class Set
        {
            internal string Condition;
            internal readonly List<Entry> Entries = new List<Entry>();
        }

        internal sealed class Entry
        {
            internal JoinedInput.ManifestCase C;
            internal JoinedInput.DumpCase D;
            internal JoinedScorer.Outcome S;
            internal string[] Reference;
            internal JoinedInput.Word[] Hypothesis;
            internal BenchReport.Case Row;
            internal string Source;
            internal string Condition;
        }

        const string NoCleanForm = "undefined (no clean form: silence is its clean form)";
        const string TooEasy = "0 — set too easy";

        static readonly string[] Groups =
        {
            "negative-out-of-set",
            "negative-near-miss",
            "noise-only",
            "silence",
        };

        static readonly string[] GameGroups =
        {
            "negative-out-of-set",
            "negative-near-miss",
            "game-audio-only",
            "silence",
        };

        static readonly string[] Metrics =
        {
            "Intent accuracy",
            "Semantic accuracy",
            "Phantom rate",
            "WER",
            "ECE (" + BenchMetrics.EceBins.ToString(CultureInfo.InvariantCulture) + " bins)",
            "NCE",
            "False-fire rate (per negative case)",
        };

        static string Int(long v) => v.ToString(CultureInfo.InvariantCulture);

        static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        internal static void Read(Inputs x)
        {
            JsonElement sidecar = JoinedInput.ParseObject(
                File.ReadAllText(x.ConditionsPath, JoinedInput.StrictUtf8),
                x.ConditionsPath
            );
            x.Steps = BenchConditions.Steps(
                x.Condition,
                sidecar.GetProperty("gameAudio").GetString() != BenchConditions.NotSupplied
            );
            var grid = new List<string>();
            foreach (JsonElement id in sidecar.GetProperty("grid").EnumerateArray())
                grid.Add(id.GetString());
            var ids = new List<string>();
            foreach (BenchConditions.Step step in x.Steps)
                ids.Add(step.Id);
            string seed = x.Seed.ToString(CultureInfo.InvariantCulture);
            string written = sidecar.GetProperty("seed").GetString();
            if (written != seed || string.Join(", ", grid) != string.Join(", ", ids))
                throw new JoinedInput.Stop(
                    2,
                    "run: "
                        + x.ConditionsPath
                        + ": written for seed "
                        + written
                        + ", condition "
                        + string.Join(", ", grid)
                        + "; this run: seed "
                        + seed
                        + ", condition "
                        + x.Condition
                        + ": a stale cond/"
                );

            JoinedInput.Dump dump = JoinedInput.ReadDump(x.DumpPath, x.Pace);
            if (dump.Schema != 2)
                throw new JoinedInput.Stop(2, x.DumpPath + ": schema 1, no positions");
            JoinedInput.Manifest manifest = JoinedInput.ReadManifest(x.ManifestPath);
            JoinedInput.Join(dump, manifest, null, pinned: false);

            var failed = new List<string>();
            foreach (JoinedInput.DumpCase c in dump.Cases)
                if (c.Failures.Count > 0)
                    failed.Add(
                        "run: conditioned case \""
                            + c.Name
                            + "\": failed decode: "
                            + string.Join("; ", c.Failures)
                    );
            if (failed.Count > 0)
            {
                failed.Add("run: no report");
                throw new JoinedInput.Stop(2, failed.ToArray());
            }

            var clips = new Dictionary<string, (string Source, string Condition)>(
                StringComparer.Ordinal
            );
            var files = new List<string>();
            foreach (JsonElement k in sidecar.GetProperty("clips").EnumerateArray())
            {
                string file = k.GetProperty("file").GetString();
                clips.Add(
                    file,
                    (k.GetProperty("source").GetString(), k.GetProperty("condition").GetString())
                );
                files.Add(file);
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (JoinedInput.ManifestCase c in manifest.Cases)
            {
                names.Add(c.Name);
                if (!clips.ContainsKey(c.Name))
                    missing.Add(
                        "run: "
                            + x.ManifestPath
                            + ": case \""
                            + c.Name
                            + "\" is not in "
                            + x.ConditionsPath
                    );
            }
            foreach (string file in files)
                if (!names.Contains(file))
                    missing.Add(
                        "run: " + x.ConditionsPath + ": clip \"" + file + "\" is not in " + x.ManifestPath
                    );
            if (missing.Count > 0)
                throw new JoinedInput.Stop(2, missing.ToArray());

            string[] lines = JoinedInput.SplitLines(
                File.ReadAllText(x.DumpPath, JoinedInput.StrictUtf8)
            );
            JsonElement end = JoinedInput.ParseObject(
                lines[lines.Length - 1],
                x.DumpPath + ":" + lines.Length
            );
            x.Dump = dump;
            x.Manifest = manifest;
            x.Sidecar = sidecar;
            x.Clips = clips;
            x.EndCases = end.GetProperty("cases").GetInt64();
            x.EndPassed = end.GetProperty("passed").GetInt64();
        }

        internal static int Fires(JoinedScorer.Outcome s) => s.FireTimes.Length;

        internal static int Phrases(List<string[]> references)
        {
            var texts = new HashSet<string>(StringComparer.Ordinal);
            foreach (string[] reference in references)
            {
                string text = string.Join(" ", reference);
                if (text.Length > 0)
                    texts.Add(text);
            }
            return texts.Count;
        }

        internal static string Cell(string v, int phrases, int cases) =>
            v + " (n=" + Int(phrases) + " phrases, " + Int(cases) + " cases)";

        internal static string Cell(BenchMetrics.Value v, int phrases, int cases) =>
            Cell(v.ToString(), phrases, cases);

        internal static (string PerCase, string PerHour) FalseFire(
            int fires,
            int cases,
            long samples,
            long rate
        )
        {
            if (cases == 0)
            {
                string none = BenchMetrics.Value.Undefined("no negative cases").ToString();
                return (none, none);
            }
            if (fires == 0)
                return (TooEasy, TooEasy);
            return (
                BenchMetrics.Value.Of((double)fires / cases).ToString(),
                BenchMetrics.Value.Of(fires / ((double)samples / rate / 3600)).ToString()
            );
        }

        static List<string[]> References(List<Entry> entries)
        {
            var references = new List<string[]>();
            foreach (Entry e in entries)
                references.Add(e.Reference);
            return references;
        }

        static long Rate(List<Entry> entries) => entries.Count > 0 ? entries[0].D.SampleRate : 0;

        static List<Entry> Group(Set set, string category)
        {
            var group = new List<Entry>();
            foreach (Entry e in set.Entries)
                if (e.C.Category == category)
                    group.Add(e);
            return group;
        }

        static List<Entry> Basis(Set set)
        {
            var basis = new List<Entry>();
            foreach (Entry e in set.Entries)
                if (
                    e.C.Category != "negative-out-of-set"
                    && e.C.Category != "negative-near-miss"
                    && e.C.Category != "noise-only"
                    && e.C.Category != "game-audio-only"
                )
                    basis.Add(e);
            return basis;
        }

        static string[] Column(List<Entry> basis, List<Entry> negatives)
        {
            var positives = new List<Entry>();
            var spoken = new List<Entry>();
            foreach (Entry e in basis)
            {
                if (!string.IsNullOrEmpty(e.C.ExpectedIntent))
                    positives.Add(e);
                if (e.Reference.Length > 0)
                    spoken.Add(e);
            }
            var classes = new List<(string Class, int ExtraFires)>();
            foreach (Entry e in positives)
                classes.Add((e.S.Class, e.S.ExtraFires));
            var words = new List<(string[] Ref, JoinedInput.Word[] Hyp)>();
            foreach (Entry e in spoken)
                words.Add((e.Reference, e.Hypothesis));
            BenchMetrics.CommandScore command = BenchMetrics.Command(classes);
            BenchMetrics.Scored scored = BenchMetrics.Score(words);
            int fires = 0;
            long samples = 0;
            foreach (Entry e in negatives)
            {
                fires += Fires(e.S);
                samples += e.D.Samples;
            }
            int p = Phrases(References(positives));
            int w = Phrases(References(spoken));
            int q = Phrases(References(negatives));
            return new[]
            {
                Cell(command.Intent, p, positives.Count),
                Cell(command.Semantic, p, positives.Count),
                Cell(command.Phantom, p, positives.Count),
                Cell(scored.Wer, w, spoken.Count),
                Cell(scored.Ece, w, spoken.Count),
                Cell(scored.Nce, w, spoken.Count),
                Cell(
                    FalseFire(fires, negatives.Count, samples, Rate(negatives)).PerCase,
                    q,
                    negatives.Count
                ),
            };
        }

        static void Rates(List<Entry> group, List<string> perCase, List<string> perHour)
        {
            int fires = 0;
            long samples = 0;
            double seconds = 0;
            foreach (Entry e in group)
            {
                fires += Fires(e.S);
                samples += e.D.Samples;
                seconds += (double)e.D.Samples / e.D.SampleRate;
            }
            (string PerCase, string PerHour) rate = FalseFire(
                fires,
                group.Count,
                samples,
                Rate(group)
            );
            string tail =
                " (fires "
                + Int(fires)
                + ", cases "
                + Int(group.Count)
                + ", "
                + seconds.ToString("F1", CultureInfo.InvariantCulture)
                + " s)";
            perCase.Add(rate.PerCase + tail);
            perHour.Add(rate.PerHour + tail);
        }

        internal static BenchReport.Conditioned Section(
            Inputs x,
            List<Entry> fast,
            List<Entry> conditioned
        )
        {
            JsonElement p = x.Sidecar;
            string Text(string key) => p.GetProperty(key).GetString();
            string Number(string key) => R(p.GetProperty(key).GetDouble());
            var grid = new List<string>();
            foreach (JsonElement id in p.GetProperty("grid").EnumerateArray())
                grid.Add(id.GetString());
            JsonElement range = p.GetProperty("levelRange");
            var section = new BenchReport.Conditioned
            {
                Seed = Text("seed"),
                Conditions = x.Condition + " — " + string.Join(", ", grid),
                Noise = Text("noise"),
                Room =
                    Text("room")
                    + (p.TryGetProperty("drrDb", out _) ? ", DRR " + Number("drrDb") + " dB" : ""),
                GameAudio = Text("gameAudio"),
                Filter =
                    "high-pass "
                    + Number("highPassHz")
                    + " Hz, peaking "
                    + Number("peakHz")
                    + " Hz "
                    + Number("peakGainDb")
                    + " dB Q "
                    + Number("peakQ")
                    + ", low-pass "
                    + Number("lowPassHz")
                    + " Hz",
                Level =
                    "peak "
                    + R(range[0].GetDouble())
                    + "–"
                    + R(range[1].GetDouble())
                    + ", "
                    + Text("levelDistribution"),
                Exit = x.Exit,
                EndCases = x.EndCases,
                EndPassed = x.EndPassed,
            };
            foreach (Entry e in conditioned)
                section.Rows.Add((e.Row, e.Source, e.Condition));

            var sets = new Dictionary<string, Set>(StringComparer.Ordinal);
            var clean = new Set { Condition = "clean" };
            sets.Add(clean.Condition, clean);
            foreach (BenchConditions.Step step in x.Steps)
                sets.Add(step.Id, new Set { Condition = step.Id });
            foreach (Entry e in conditioned)
                sets[e.Condition].Entries.Add(e);
            var fastByName = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var fastSilence = new List<Entry>();
            foreach (Entry e in fast)
            {
                fastByName.Add(e.C.Name, e);
                if (e.C.Category == "silence")
                    fastSilence.Add(e);
            }

            var speeds = new List<List<BenchConditions.Step>>();
            foreach (BenchConditions.Step step in x.Steps)
            {
                if (
                    speeds.Count == 0
                    || speeds[^1][0].Speed != step.Speed
                    || speeds[^1][0].Game != step.Game
                )
                    speeds.Add(new List<BenchConditions.Step>());
                speeds[^1].Add(step);
            }

            foreach (List<BenchConditions.Step> steps in speeds)
            {
                int speed = steps[0].Speed;
                bool game = steps[0].Game;
                string[] groups = game ? GameGroups : Groups;
                string title = game ? "SIR" : "Speed " + Int(speed / 10) + "." + Int(speed % 10);
                if (game)
                    section.Sir = true;
                var curveHeader = new List<string> { "Metric", "clean" };
                var negativeHeader = new List<string> { "Group", "Rate", "clean" };
                foreach (BenchConditions.Step step in steps)
                {
                    curveHeader.Add(step.Id);
                    negativeHeader.Add(step.Id);
                }

                var cleanBasis = new List<Entry>();
                foreach (Entry e in Basis(sets[steps[0].Id]))
                    cleanBasis.Add(fastByName[e.Source]);
                var cleanNegatives = new List<Entry>();
                foreach (string group in groups)
                    cleanNegatives.AddRange(group == "silence" ? fastSilence : Group(clean, group));
                var columns = new List<string[]> { Column(cleanBasis, cleanNegatives) };
                foreach (BenchConditions.Step step in steps)
                {
                    Set at = sets[step.Id];
                    var negatives = new List<Entry>();
                    foreach (string group in groups)
                        negatives.AddRange(Group(at, group));
                    columns.Add(Column(Basis(at), negatives));
                }
                var curveRows = new List<string[]>();
                for (int m = 0; m < Metrics.Length; m++)
                {
                    var row = new string[columns.Count + 1];
                    row[0] = Metrics[m];
                    for (int k = 0; k < columns.Count; k++)
                        row[k + 1] = columns[k][m];
                    curveRows.Add(row);
                }
                section.Curves.Add((title, curveHeader.ToArray(), curveRows));

                var negativeRows = new List<string[]>();
                foreach (string group in groups)
                {
                    var perCase = new List<string> { group, "per case" };
                    var perHour = new List<string> { group, "per hour" };
                    if (group == "noise-only" || group == "game-audio-only")
                    {
                        perCase.Add(NoCleanForm);
                        perHour.Add(NoCleanForm);
                    }
                    else
                        Rates(group == "silence" ? fastSilence : Group(clean, group), perCase, perHour);
                    foreach (BenchConditions.Step step in steps)
                        Rates(Group(sets[step.Id], group), perCase, perHour);
                    negativeRows.Add(perCase.ToArray());
                    negativeRows.Add(perHour.ToArray());
                }
                section.Negatives.Add((title, negativeHeader.ToArray(), negativeRows));
            }
            return section;
        }
    }
}
