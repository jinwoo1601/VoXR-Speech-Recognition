// ============================================================================
// Purpose:  The rig's --bench verbs: the hook set from a module initializer, verb dispatch, the metric toy checks, the speech-truth check, the fast and paced runs, the matched-chunk check
// Layer:    Planning.AbRig
// Owns:     Bench (internal static class), Bench.Prelude (sealed class)
// Depends:  Program, BenchMetrics, BenchPaced, BenchReport, Joined, JoinedInput, JoinedRecogniser, JoinedScorer, VoxrSlotDefinition, VoxrCommandDefinition, VoxrWavReader
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using VoXR.Commands;

namespace AbRig
{
    static class Bench
    {
        static int failures;
        static int checks;
        static string reading;

        const int TruthFloor = 328;
        const int FrameTolerance = 960;
        const int TailSamples = 48000;

        sealed class Prelude
        {
            internal string[] StagedFrom;
            internal JoinedInput.Dump Dump;
            internal JoinedInput.Manifest Manifest;
            internal JoinedInput.Pin Pin;
            internal JsonElement Voice;
            internal long EndCases;
            internal long EndPassed;
        }

#pragma warning disable CA2255
        [ModuleInitializer]
        internal static void Register()
        {
            Program.BenchHook = Run;
        }
#pragma warning restore CA2255

        static int Run(
            string[] args,
            string grammarJson,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            try
            {
                string verb = args[Array.IndexOf(args, "--bench") + 1];
                if (verb == "metrics-check")
                {
                    if (args.Length != 2)
                        throw new JoinedInput.Stop(2, "usage: --bench metrics-check");
                    return MetricsCheck();
                }
                if (verb == "truth-check")
                {
                    if (
                        args.Length != 6
                        || args[2] != "--manifest"
                        || args[4] != "--fixtures-root"
                    )
                        throw new JoinedInput.Stop(
                            2,
                            "usage: --bench truth-check --manifest <manifest.json> --fixtures-root <dir>"
                        );
                    return TruthCheck(JoinedInput.ReadManifest(args[3]), args[5]);
                }
                if (verb == "matched-check")
                {
                    if (
                        args.Length != 14
                        || args[2] != "--dump"
                        || args[4] != "--manifest"
                        || args[6] != "--pin"
                        || args[8] != "--fixtures-root"
                        || args[10] != "--matched"
                        || args[12] != "--pace"
                        || !long.TryParse(
                            args[13],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out long p
                        )
                        || p < 1
                        || p > 16384
                    )
                        throw new JoinedInput.Stop(
                            2,
                            "usage: --bench matched-check --dump <dump.jsonl> --manifest <manifest.json> --pin <expectations.json> --fixtures-root <dir> --matched <matched.jsonl> --pace <samples>"
                        );
                    return MatchedCheck(args[3], args[5], args[7], args[9], args[11], p);
                }
                long pace = 0;
                bool named =
                    (args.Length == 18 || args.Length == 28)
                    && args[14] == "--profile"
                    && args[16] == "--mode";
                int b = named ? 18 : 14;
                bool paced = args.Length == b + 10;
                if (
                    (args.Length != b && !paced)
                    || args[2] != "--dump"
                    || args[4] != "--manifest"
                    || args[6] != "--pin"
                    || args[8] != "--fixtures-root"
                    || args[10] != "--harness-exit"
                    || args[12] != "--report"
                    || (args[11] != "0" && args[11] != "1")
                    || (
                        paced
                        && (
                            args[b] != "--matched"
                            || args[b + 2] != "--matched-exit"
                            || args[b + 4] != "--paced"
                            || args[b + 6] != "--paced-exit"
                            || args[b + 8] != "--pace"
                            || (args[b + 3] != "0" && args[b + 3] != "1")
                            || (args[b + 7] != "0" && args[b + 7] != "1")
                            || !long.TryParse(
                                args[b + 9],
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out pace
                            )
                            || pace < 1
                            || pace > 16384
                        )
                    )
                )
                    throw new JoinedInput.Stop(
                        2,
                        "usage: --bench run --dump <dump.jsonl> --manifest <manifest.json> --pin <expectations.json> --fixtures-root <dir> --harness-exit 0|1 --report <report.md> [--profile <name> --mode <name>] [--matched <matched.jsonl> --matched-exit 0|1 --paced <paced.jsonl> --paced-exit 0|1 --pace <samples>]"
                    );
                return FastRun(
                    args[3],
                    args[5],
                    args[7],
                    args[9],
                    int.Parse(args[11]),
                    args[13],
                    named ? args[15] : null,
                    named ? args[17] : null,
                    grammarJson,
                    slots,
                    commands,
                    paced ? args[b + 1] : null,
                    paced ? int.Parse(args[b + 3]) : 0,
                    paced ? args[b + 5] : null,
                    paced ? int.Parse(args[b + 7]) : 0,
                    pace
                );
            }
            catch (JoinedInput.Stop stop)
            {
                foreach (string line in stop.Lines)
                    Console.Error.WriteLine("bench: " + line);
                return stop.Code;
            }
            catch (Exception e)
            {
                if (reading == null)
                {
                    Console.Error.WriteLine("bench: " + e.GetType().Name + ": " + e.Message);
                    return 2;
                }
                string at =
                    e is JsonException je && je.LineNumber.HasValue
                        ? ":" + (je.LineNumber.Value + 1)
                        : "";
                Console.Error.WriteLine(
                    "bench: " + reading + at + ": " + e.GetType().Name + ": " + e.Message
                );
                return 2;
            }
        }

        static int FastRun(
            string dumpPath,
            string manifestPath,
            string pinPath,
            string root,
            int harnessExit,
            string reportPath,
            string profile,
            string mode,
            string grammarJson,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands,
            string matchedPath,
            int matchedExit,
            string pacedPath,
            int pacedExit,
            long pace
        )
        {
            reading = reportPath;
            if (File.Exists(reportPath))
                File.Delete(reportPath);
            Prelude prelude = FastPrelude(
                dumpPath,
                matchedPath == null ? null : 0,
                manifestPath,
                pinPath,
                root
            );
            string[] stagedFrom = prelude.StagedFrom;
            JoinedInput.Dump dump = prelude.Dump;
            JoinedInput.Manifest manifest = prelude.Manifest;
            JoinedInput.Pin pin = prelude.Pin;
            JsonElement voice = prelude.Voice;
            long endCases = prelude.EndCases;
            long endPassed = prelude.EndPassed;
            Dictionary<string, JoinedInput.DumpCase> matchedByCase = null;
            Dictionary<string, JoinedInput.DumpCase> pacedByCase = null;
            if (matchedPath != null)
            {
                reading = matchedPath;
                JoinedInput.Dump matched = JoinedInput.ReadDump(matchedPath, 16384);
                if (matched.Schema != 2)
                    throw new JoinedInput.Stop(2, matchedPath + ": schema 1, no positions");
                reading = pacedPath;
                JoinedInput.Dump twenty = JoinedInput.ReadDump(pacedPath, pace);
                if (twenty.Schema != 2)
                    throw new JoinedInput.Stop(2, pacedPath + ": schema 1, no positions");
                reading = null;
                matchedByCase = BenchPaced.CheckMatched(prelude.Dump, matched);
                pacedByCase = BenchPaced.Paired(prelude.Dump, twenty, "20 ms");
            }

            Joined.GrammarPinGuard(grammarJson, pin, stagedFrom);
            (float tickRate, KeyValuePair<string, string>[] config) = Joined.RecogniserConfig();

            var dumpByCase = new Dictionary<string, JoinedInput.DumpCase>(StringComparer.Ordinal);
            foreach (JoinedInput.DumpCase c in dump.Cases)
                dumpByCase.Add(c.Name, c);
            var outcomes = new List<JoinedScorer.Outcome>();
            foreach (JoinedInput.ManifestCase c in manifest.Cases)
            {
                JoinedScorer.Outcome s = Joined.ScoreRecogniser(
                    c,
                    dumpByCase[c.Name],
                    slots,
                    commands
                );
                Joined.AppliedGrammarGuard(c.Name, s.AppliedGrammar, pin);
                outcomes.Add(s);
            }

            long t = (long)JoinedRecogniser.TickRate;
            var rows = new List<BenchReport.Case>();
            var words = new List<(string[] Ref, JoinedInput.Word[] Hyp)>();
            var classes = new List<(string Class, int ExtraFires)>();
            for (int i = 0; i < manifest.Cases.Count; i++)
            {
                JoinedInput.ManifestCase c = manifest.Cases[i];
                JoinedInput.DumpCase d = dumpByCase[c.Name];
                JoinedScorer.Outcome s = outcomes[i];
                BenchReport.Case row = Stamps(c, d, s, t);
                string[] reference = BenchMetrics.Reference(c.Phrase, c.Segments);
                JoinedInput.Word[] hypothesis = BenchMetrics.Hypothesis(d.Words);
                row.Wer =
                    reference.Length == 0
                        ? "excluded (silence)"
                        : BenchMetrics.Align(reference, hypothesis).Wer.ToString();
                row.Pending =
                    s.Pending && s.FireTimes.Length == 0 ? "pending, timed out"
                    : s.Pending ? "pending"
                    : "-";
                rows.Add(row);
                words.Add((reference, hypothesis));
                classes.Add((s.Class, s.ExtraFires));
            }
            BenchMetrics.Scored scored = BenchMetrics.Score(words);
            BenchMetrics.CommandScore command = BenchMetrics.Command(classes);

            BenchReport.Paced section =
                matchedPath == null
                    ? null
                    : BenchPaced.Assemble(
                        manifest,
                        dumpByCase,
                        matchedByCase,
                        pacedByCase,
                        outcomes,
                        pin,
                        slots,
                        commands,
                        t,
                        pace,
                        matchedExit,
                        pacedExit
                    );
            var header = new BenchReport.Header
            {
                Profile = profile,
                Mode = mode,
                Gain = dump.Gain,
                Libvosk = dump.Libvosk,
                ModelConf = dump.ModelConf,
                Grammar = dump.Grammar,
                StagedFrom = stagedFrom,
                TickRate = tickRate,
                Config = config,
                Voice = voice.GetString(),
                Provenance = manifest.Provenance,
                HarnessExit = harnessExit,
                EndCases = endCases,
                EndPassed = endPassed,
                Cases = dump.Cases.Count,
            };
            string text = BenchReport.Build(header, rows, command, scored, section);
            reading = reportPath;
            BenchReport.Write(reportPath, text);
            return 0;
        }

        static Prelude FastPrelude(
            string dumpPath,
            long? pace,
            string manifestPath,
            string pinPath,
            string root
        )
        {
            reading = "./STAGED_FROM";
            string[] stagedFrom = JoinedInput.SplitLines(File.ReadAllText("STAGED_FROM"));
            reading = dumpPath;
            JoinedInput.Dump dump = JoinedInput.ReadDump(dumpPath, pace);
            if (dump.Schema != 2)
                throw new JoinedInput.Stop(2, dumpPath + ": schema 1, no positions");
            reading = manifestPath;
            JoinedInput.Manifest manifest = JoinedInput.ReadManifest(manifestPath);
            reading = pinPath;
            JoinedInput.Pin pin = JoinedInput.ReadPin(pinPath);
            reading = manifestPath;
            JsonElement manifestRoot = JoinedInput.ParseObject(
                File.ReadAllText(manifestPath, JoinedInput.StrictUtf8),
                manifestPath
            );
            if (
                !manifestRoot.TryGetProperty("voice", out JsonElement voice)
                || voice.ValueKind != JsonValueKind.String
            )
                throw new JoinedInput.Stop(2, manifestPath + ": no string `voice`");
            reading = dumpPath;
            string[] dumpLines = JoinedInput.SplitLines(
                File.ReadAllText(dumpPath, JoinedInput.StrictUtf8)
            );
            JsonElement end = JoinedInput.ParseObject(
                dumpLines[dumpLines.Length - 1],
                dumpPath + ":" + dumpLines.Length
            );
            long endCases = end.GetProperty("cases").GetInt64();
            long endPassed = end.GetProperty("passed").GetInt64();
            reading = root;
            TruthCheck(manifest, root);
            reading = null;
            JoinedInput.Join(dump, manifest, pin, pinned: true);

            var failing = new List<string>();
            int failedCases = 0;
            foreach (JoinedInput.DumpCase c in dump.Cases)
            {
                bool failed = false;
                if (c.Failures.Count > 0)
                {
                    failing.Add(
                        "self-check: case \""
                            + c.Name
                            + "\": failed decode: "
                            + string.Join("; ", c.Failures)
                    );
                    failed = true;
                }
                string[] expected = pin.Cases[c.Name];
                string[] actual = c.Finals.ToArray();
                if (Joined.FirstDifference(actual, expected) >= 0)
                {
                    failing.Add(
                        "self-check: case \""
                            + c.Name
                            + "\": finals expected "
                            + Joined.JsonArray(expected)
                            + ", actual "
                            + Joined.JsonArray(actual)
                    );
                    failed = true;
                }
                if (failed)
                    failedCases++;
            }
            if (failedCases > 0)
            {
                failing.Add(
                    "self-check failed on "
                        + failedCases
                        + "/"
                        + dump.Cases.Count
                        + " cases against the pin "
                        + pin.Path
                        + ": no numbers, no report"
                );
                throw new JoinedInput.Stop(3, failing.ToArray());
            }
            Console.Out.WriteLine(
                "self-check: "
                    + dump.Cases.Count
                    + "/"
                    + dump.Cases.Count
                    + " cases' finals equal the pin."
            );
            return new Prelude
            {
                StagedFrom = stagedFrom,
                Dump = dump,
                Manifest = manifest,
                Pin = pin,
                Voice = voice,
                EndCases = endCases,
                EndPassed = endPassed,
            };
        }

        static int MatchedCheck(
            string dumpPath,
            string manifestPath,
            string pinPath,
            string root,
            string matchedPath,
            long pace
        )
        {
            Prelude prelude = FastPrelude(dumpPath, 0, manifestPath, pinPath, root);
            reading = matchedPath;
            JoinedInput.Dump matched = JoinedInput.ReadDump(matchedPath, pace);
            if (matched.Schema != 2)
                throw new JoinedInput.Stop(2, matchedPath + ": schema 1, no positions");
            reading = null;
            BenchPaced.CheckMatched(prelude.Dump, matched);
            int cases = prelude.Dump.Cases.Count;
            Console.Out.WriteLine(
                "matched-check: "
                    + cases
                    + "/"
                    + cases
                    + " cases' finals and first partials within 4096 samples of the fast dump's at pace "
                    + pace
                    + "."
            );
            int measured = 0;
            int merged = 0;
            int mergedCases = 0;
            foreach (JoinedInput.DumpCase c in matched.Cases)
            {
                BenchPaced.Factor f = BenchPaced.Reads(c.Reads, c.Samples);
                measured += f.Measured;
                merged += f.Merged;
                if (f.Merged > 0)
                    mergedCases++;
            }
            Console.Out.WriteLine(
                "matched-check: read trace: "
                    + measured
                    + " measured reads, "
                    + merged
                    + " merged changes in "
                    + mergedCases
                    + "/"
                    + cases
                    + " cases."
            );
            return 0;
        }

        static BenchReport.Case Stamps(
            JoinedInput.ManifestCase c,
            JoinedInput.DumpCase d,
            JoinedScorer.Outcome s,
            long t
        )
        {
            var row = new BenchReport.Case
            {
                Name = c.Name,
                Category = c.Category,
                Class = s.Class,
                FinalCount = s.FinalCount,
                ExtraFires = s.ExtraFires,
            };
            if (c.Speech.Length == 0)
            {
                row.FirstPartial = "n/a";
                row.Finals = "n/a";
                row.CommandEvent = "n/a";
                return row;
            }
            long e = c.Speech[^1].End;
            long r = d.SampleRate;
            var selected = Selected(d);
            var finals = new List<string>();
            foreach ((JoinedInput.DumpResult Result, string Text) f in selected.Finals)
                finals.Add(BenchReport.Offset(f.Result.Consumed.Value - e, r));
            row.FirstPartial =
                selected.FirstPartial == null
                    ? "none"
                    : BenchReport.Offset(selected.FirstPartial.Consumed.Value - e, r);
            row.Finals = finals.Count > 0 ? string.Join(", ", finals) : "none";
            long? k = FireFrame(s, t);
            row.CommandEvent = k.HasValue ? FrameOffset(k.Value, e, r, t) : "none";
            return row;
        }

        internal static (
            JoinedInput.DumpResult FirstPartial,
            List<(JoinedInput.DumpResult Result, string Text)> Finals
        ) Selected(JoinedInput.DumpCase d)
        {
            JoinedInput.DumpResult firstPartial = null;
            var finals = new List<(JoinedInput.DumpResult Result, string Text)>();
            foreach (JoinedInput.DumpResult x in d.Results)
            {
                using (JsonDocument doc = JsonDocument.Parse(x.Payload))
                {
                    JsonElement p = doc.RootElement;
                    if (!x.Final)
                    {
                        if (
                            firstPartial == null
                            && p.GetProperty("partial").GetString().Length > 0
                        )
                            firstPartial = x;
                    }
                    else
                    {
                        string text = p.GetProperty("text").GetString();
                        if (text.Length > 0)
                            finals.Add((x, text));
                    }
                }
            }
            return (firstPartial, finals);
        }

        internal static long? FireFrame(JoinedScorer.Outcome s, long t) =>
            s.FireTimes.Length > 0 ? (long)Math.Round(s.FireTimes[0] * t) : (long?)null;

        internal static string FrameOffset(long k, long e, long r, long t) =>
            BenchReport.Offset(k * r - t * e, t * r);

        static void Expect(string where, string what, string actual, string expected)
        {
            checks++;
            bool ok = actual == expected;
            if (!ok)
                failures++;
            Console.WriteLine(
                $"{(ok ? "ok  " : "FAIL")}  {where, -28} {what, -52} got {actual}  want {expected}"
            );
        }

        static JoinedInput.Word[] Hyp(string spec)
        {
            var words = new List<JoinedInput.Word>();
            foreach (string item in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int at = item.LastIndexOf('@');
                words.Add(
                    new JoinedInput.Word
                    {
                        Text = item.Substring(0, at),
                        Conf = float.Parse(item.Substring(at + 1), CultureInfo.InvariantCulture),
                    }
                );
            }
            return words.ToArray();
        }

        static List<(double Conf, bool Correct)> Pairs(string spec)
        {
            var pairs = new List<(double Conf, bool Correct)>();
            foreach (string item in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                pairs.Add(
                    (
                        double.Parse(item.Substring(2), CultureInfo.InvariantCulture),
                        item[0] == 'R'
                    )
                );
            return pairs;
        }

        static string Aligned((string[] Ref, JoinedInput.Word[] Hyp) c)
        {
            var a = BenchMetrics.Align(c.Ref, c.Hyp);
            var correct = new char[a.Correct.Length];
            for (int k = 0; k < correct.Length; k++)
                correct[k] = a.Correct[k] ? 'T' : 'F';
            return $"S={a.S} D={a.D} I={a.I} N={a.N} correct={new string(correct)} wer={a.Wer}";
        }

        static string Totals(BenchMetrics.Scored s) =>
            $"S={s.S} D={s.D} I={s.I} N={s.N} stray={s.Stray} wer={s.Wer}";

        static JoinedInput.DumpCase Timeline(
            long? partial,
            params (string Text, long Consumed)[] finals
        )
        {
            var d = new JoinedInput.DumpCase { Name = "toy", SampleRate = 48000 };
            long seq = 0;
            if (partial.HasValue)
                d.Results.Add(
                    new JoinedInput.DumpResult
                    {
                        Seq = seq++,
                        Final = false,
                        Consumed = partial,
                        Payload = "{\"partial\":\"fire\"}",
                    }
                );
            foreach ((string Text, long Consumed) f in finals)
                d.Results.Add(
                    new JoinedInput.DumpResult
                    {
                        Seq = seq++,
                        Final = true,
                        Consumed = f.Consumed,
                        Payload = "{\"text\":\"" + f.Text + "\"}",
                    }
                );
            return d;
        }

        static string Factored(BenchPaced.Factor f) =>
            $"measured={f.Measured} merged={f.Merged} decode_us={f.Decode} samples={f.Samples} factor={BenchPaced.Ratio(f.Decode, f.Samples, 48000)}";

        static string Found(List<string> lines) =>
            lines.Count == 0 ? "none" : string.Join(" | ", lines);

        static string ReadLines(string text, long? pace)
        {
            string path = Path.GetTempFileName();
            string Shown(long? v) =>
                v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "none";
            try
            {
                File.WriteAllText(path, text, JoinedInput.StrictUtf8);
                JoinedInput.Dump dump = JoinedInput.ReadDump(path, pace);
                JoinedInput.DumpCase c = dump.Cases[0];
                return "pace="
                    + Shown(dump.Pace)
                    + " wall="
                    + Shown(c.Results[0].Wall)
                    + " push="
                    + (
                        c.Pushes == null
                            ? "none"
                            : c.Pushes[0].End + "," + c.Pushes[0].Due + "," + c.Pushes[0].Made
                    )
                    + " read="
                    + (
                        c.Reads == null
                            ? "none"
                            : c.Reads[0].Consumed + "," + c.Reads[0].At + "," + c.Reads[0].Pushed
                    );
            }
            catch (JoinedInput.Stop stop)
            {
                string prefix = path + ":";
                string line = stop.Lines[0];
                return "stop "
                    + stop.Code
                    + ": "
                    + (
                        line.StartsWith(prefix, StringComparison.Ordinal)
                            ? line.Substring(prefix.Length)
                            : line
                    );
            }
            finally
            {
                File.Delete(path);
            }
        }

        static int MetricsCheck()
        {
            var c1 = (BenchMetrics.Reference("fire missiles", null), Hyp("fire@1 torpedoes@0.5"));
            var c2 = (BenchMetrics.Reference(null, new[] { "cease", "fire" }), Hyp("fire@1"));
            var c3 = (BenchMetrics.Reference("disengage", null), Hyp("disengage@1 now@0.5"));
            var c4 = (BenchMetrics.Reference("alpha one", null), Hyp("one@0.5 alpha@0.5"));
            var c5 = (
                BenchMetrics.Reference("resume fire", null),
                Hyp("[unk]@0.5 resume@1 fire@1")
            );
            var silence = (Array.Empty<string>(), Hyp("[unk]@0.5 fire@0.5"));

            Expect("1", "F7 align: a substitution", Aligned(c1), "S=1 D=0 I=0 N=2 correct=TF wer=0.5000");
            Expect("2", "F7 align: segments joined, a deletion", Aligned(c2), "S=0 D=1 I=0 N=2 correct=T wer=0.5000");
            Expect("3", "F7 align: an insertion", Aligned(c3), "S=0 D=0 I=1 N=1 correct=TF wer=1.0000");
            Expect("4", "F7 align: a three-way tie, diagonal first", Aligned(c4), "S=2 D=0 I=0 N=2 correct=FF wer=1.0000");
            Expect("5", "F7 align: [unk] kept, inserted", Aligned(c5), "S=0 D=0 I=1 N=2 correct=FTT wer=0.5000");

            var corpus = BenchMetrics.Score(new[] { c1, c2, c3, c4, c5, silence });
            Expect("6", "F7 score: rows 1-5 and silence", Totals(corpus), "S=3 D=1 I=2 N=9 stray=2 wer=0.6667");
            Expect("7", "F8 ECE: row 6", corpus.Ece.ToString(), "0.2500");
            Expect("8", "F8 NCE: row 6", corpus.Nce.ToString(), "0.5000");

            var w9 = Pairs("R@1 W@1 R@0.25 R@0.25 W@0");
            Expect("9", "F8 ECE: three bins", BenchMetrics.Ece(w9).ToString(), "0.5000");
            Expect("10", "F8 NCE: row 9", BenchMetrics.Nce(w9).ToString(), "undefined (a wrong word at confidence 1)");

            var w11 = Pairs("R@1 R@0.5 W@0.5 W@0");
            Expect("11", "F8 NCE: finite", BenchMetrics.Nce(w11).ToString(), "0.5000");
            Expect("12", "F8 ECE: row 11", BenchMetrics.Ece(w11).ToString(), "0.0000");

            Expect("13", "F8 NCE: all right", BenchMetrics.Nce(Pairs("R@0.5 R@0.75")).ToString(), "undefined (all words right)");
            Expect("14", "F8 NCE: all wrong", BenchMetrics.Nce(Pairs("W@0.5 W@0.25")).ToString(), "undefined (all words wrong)");
            Expect("15", "F8 NCE: a right word at 0", BenchMetrics.Nce(Pairs("R@0 W@0.5")).ToString(), "undefined (a right word at confidence 0)");
            Expect("16", "F8 ECE: no words", BenchMetrics.Ece(Pairs("")).ToString(), "undefined (no words)");
            Expect("17", "F8 NCE: no words", BenchMetrics.Nce(Pairs("")).ToString(), "undefined (no words)");

            var cmd = BenchMetrics.Command(
                new[]
                {
                    ("correct-fire", 0),
                    ("correct-fire", 1),
                    ("wrong-slots", 0),
                    ("wrong-intent", 3),
                    ("missed", 0),
                    ("correct-no-fire", 0),
                    ("false-fire", 1),
                }
            );
            var counts = new List<string>();
            for (int k = 0; k < JoinedScorer.Classes.Length; k++)
                counts.Add(JoinedScorer.Classes[k] + "=" + cmd.Counts[k]);
            Expect("18", "F6 counts", string.Join(" ", counts), "correct-fire=2 correct-no-fire=1 wrong-slots=1 wrong-intent=1 missed=1 false-fire=1");
            Expect("19", "F6 intent accuracy: row 18", cmd.Intent.ToString(), "0.6000");
            Expect("20", "F6 semantic accuracy: row 18", cmd.Semantic.ToString(), "0.4000");
            Expect("21", "F6 phantom rate: row 18", cmd.Phantom.ToString(), "0.8000");

            var none = BenchMetrics.Command(new[] { ("correct-no-fire", 0), ("false-fire", 2) });
            Expect(
                "22",
                "F6 rates: no expected intent",
                none.Intent + " | " + none.Semantic + " | " + none.Phantom,
                "undefined (no cases with an expected intent) | undefined (no cases with an expected intent) | undefined (no cases with an expected intent)"
            );

            var silent = BenchMetrics.Score(new[] { (Array.Empty<string>(), Hyp("fire@0.5")) });
            Expect("23", "F7 score: silence only", Totals(silent), "S=0 D=0 I=0 N=0 stray=1 wer=undefined (no reference words)");

            long t = (long)JoinedRecogniser.TickRate;
            var toy = new JoinedInput.ManifestCase
            {
                Name = "toy",
                Category = "toy",
                Speech = new[] { (0L, 48000L) },
            };
            var toyDump = new JoinedInput.DumpCase
            {
                SampleRate = 48000,
                Results =
                {
                    new JoinedInput.DumpResult { Final = false, Consumed = 24000, Payload = "{\"partial\":\"fire\"}" },
                    new JoinedInput.DumpResult { Final = true, Consumed = 48000, Payload = "{\"text\":\"fire\"}" },
                    new JoinedInput.DumpResult { Final = true, Consumed = 52800, Payload = "{\"text\":\"missiles\"}" },
                },
            };
            var toyOutcome = new JoinedScorer.Outcome { Class = "correct-fire", FireTimes = new[] { 1.5 } };
            BenchReport.Case stamped = Stamps(toy, toyDump, toyOutcome, t);
            Expect(
                "24",
                "F4 stamps: partial, finals, command event",
                stamped.FirstPartial + " | " + stamped.Finals + " | " + stamped.CommandEvent,
                "-0.500 | 0.000, +0.100 | +0.500"
            );

            var quiet = new JoinedInput.ManifestCase
            {
                Name = "toy-silence",
                Category = "toy",
                Speech = Array.Empty<(long Start, long End)>(),
            };
            BenchReport.Case unstamped = Stamps(quiet, toyDump, toyOutcome, t);
            Expect(
                "25",
                "F4 stamps: a silence case",
                unstamped.FirstPartial + " | " + unstamped.Finals + " | " + unstamped.CommandEvent,
                "n/a | n/a | n/a"
            );

            Expect("26", "F8 ECE: a float confidence on a decile edge", BenchMetrics.Ece(new[] { ((double)0.7f, true), (0.75, false) }).ToString(), "0.2250");

            var s27 = BenchPaced.Split(43200, 912000, 48000, 48000, 96000);
            Expect("27", "F2 split: a stamp before the end of speech", s27.Audio + " | " + s27.Wall + " | " + s27.Compute, "-0.100 | -0.088 | +0.012");
            var s28 = BenchPaced.Split(72000, 1534000, 48000, 48000, 96000);
            Expect("28", "F2 split: a stamp after the end of speech", s28.Audio + " | " + s28.Wall + " | " + s28.Compute, "+0.500 | +0.534 | +0.034");
            var s29 = BenchPaced.Split(150000, 3200000, 48000, 48000, 96000);
            Expect("29", "F3 split: a flush result", s29.Audio + " | " + s29.Wall + " | " + s29.Compute, "+2.125 | unpaced | unpaced");
            var s30 = BenchPaced.CommandSplit(135, 144, 48000, 48000, t);
            Expect("30", "F5 command split: fast, paced, paced - fast", s30.Audio + " | " + s30.Wall + " | " + s30.Compute, "+0.500 | +0.600 | +0.100");

            var f31 = BenchPaced.Reads(new List<(long, long, long)> { (4096, 10000, 16384), (8192, 18600, 16384), (12288, 27000, 16384), (16384, 35600, 16384) }, 16384);
            Expect("31", "F4 factor: back-to-back reads, the last unmeasured", Factored(f31), "measured=3 merged=0 decode_us=25600 samples=12288 factor=0.1000");
            var f32 = BenchPaced.Reads(new List<(long, long, long)> { (4096, 0, 8192), (8192, 12800, 8192) }, 8192);
            Expect("32", "F4 factor: one measured read", Factored(f32), "measured=1 merged=0 decode_us=12800 samples=4096 factor=0.1500");
            Expect("33", "F4 factor: corpus of rows 31-32, summed", BenchPaced.Corpus(new List<BenchPaced.Factor> { f31, f32 }, 48000).ToString(), "0.1125");
            var f34 = BenchPaced.Reads(new List<(long, long, long)> { (4096, 0, 16384), (12288, 5000, 16384), (16384, 9000, 16384) }, 16384);
            Expect("34", "F4 factor: a merged change, no measured reads", Factored(f34), "measured=0 merged=1 decode_us=0 samples=0 factor=undefined (no measured reads)");

            Expect("35", "F9 lateness: the largest push lateness", BenchPaced.Lateness(new List<(long, long, long)> { (960, 20000, 20150), (1920, 40000, 40020), (2880, 60000, 61200) }), "1.200 ms");

            var fastCase = Timeline(24576, ("fire", 49152));
            Expect("36", "F6 matched check: each stamp 4096 apart", BenchPaced.Matched(fastCase, Timeline(28672, ("fire", 53248))) ?? "pass", "pass");
            Expect("37", "F6 matched check: a final 4097 apart", BenchPaced.Matched(fastCase, Timeline(28672, ("fire", 53249))) ?? "pass", "matched-check: case \"toy\": final 1 \"fire\" at consumed fast 49152, matched 53249: more than 4096 apart");
            Expect("38", "F6 matched check: an extra final", BenchPaced.Matched(fastCase, Timeline(28672, ("fire", 53248), ("missiles", 57344))) ?? "pass", "matched-check: case \"toy\": finals fast [\"fire\"], matched [\"fire\",\"missiles\"]");
            Expect("39", "F6 matched check: a first partial on one side", BenchPaced.Matched(fastCase, Timeline(null, ("fire", 53248))) ?? "pass", "matched-check: case \"toy\": first partial at consumed fast 24576, matched none");

            string pacedDump =
                string.Join(
                    "\n",
                    "{\"kind\":\"header\",\"schema\":2,\"decoderRate\":100,\"manifest\":\"m\",\"fixtures\":\"f\",\"model\":\"o\",\"gain\":-18,\"libvosk\":\"v\",\"grammar\":[],\"ringCapacity\":65536,\"pace\":960}",
                    "{\"kind\":\"case\",\"case\":\"toy\",\"samples\":96000,\"sampleRate\":48000}",
                    "{\"kind\":\"result\",\"case\":\"toy\",\"seq\":0,\"final\":false,\"pushed\":96000,\"prevPushed\":95040,\"consumed\":72000,\"origin\":0,\"wall\":1534000,\"payload\":{\"partial\":\"fire\"}}",
                    "{\"kind\":\"case-end\",\"case\":\"toy\",\"results\":1,\"pushed\":96000,\"consumed\":96000,\"errors\":[],\"pushes\":[[960,20000,20150]],\"reads\":[[4096,10000,16384]]}",
                    "{\"kind\":\"end\",\"cases\":1,\"passed\":1}"
                ) + "\n";
            string fastDump = pacedDump
                .Replace(",\"pace\":960", "")
                .Replace(",\"wall\":1534000", "")
                .Replace(",\"pushes\":[[960,20000,20150]]", "")
                .Replace(",\"reads\":[[4096,10000,16384]]", "");
            string unread = "pace=none wall=none push=none read=none";
            Expect("40", "Stamps reader: a paced dump read paced", ReadLines(pacedDump, 960), "pace=960 wall=1534000 push=960,20000,20150 read=4096,10000,16384");
            Expect("41", "Stamps reader: a paced dump read as today", ReadLines(pacedDump, null), unread);
            Expect("42", "Stamps reader: a pace other than expected", ReadLines(pacedDump, 16384), "stop 2: 1: `pace` 960, expected 16384");
            Expect("43", "Stamps reader: a paced dump read as fast", ReadLines(pacedDump, 0), "stop 2: 1: `pace` 960, expected none (a fast dump)");
            Expect("44", "Stamps reader: a paced result without wall", ReadLines(pacedDump.Replace(",\"wall\":1534000", ""), 960), "stop 2: 3: missing or mistyped field `wall`");
            Expect("45", "Stamps reader: a paced case-end without reads", ReadLines(pacedDump.Replace(",\"reads\":[[4096,10000,16384]]", ""), 960), "stop 2: 4: missing or mistyped field `reads`");
            Expect("46", "Stamps reader: a push not a triple", ReadLines(pacedDump.Replace("[[960,20000,20150]]", "[[960,20000]]"), 960), "stop 2: 4: missing or mistyped field `pushes`");
            Expect("47", "Stamps reader: a fast dump read paced", ReadLines(fastDump, 960), "stop 2: 1: no `pace`, expected 960");
            Expect("48", "Stamps reader: a fast dump read as fast", ReadLines(fastDump, 0), unread);
            Expect("49", "Stamps reader: a wall in a fast dump", ReadLines(fastDump.Replace("\"origin\":0", "\"origin\":0,\"wall\":1534000"), 0), "stop 2: 3: `wall` without `pace` in the header");

            JoinedInput.DumpCase paced50 = Timeline(null, ("fire", 72000));
            paced50.Samples = 96000;
            paced50.Finals.Add("fire");
            paced50.Results[0].Wall = 1534000;
            JoinedInput.DumpCase w50 = BenchPaced.WallView(paced50);
            Expect("50", "F5 wall view: consumed and r from the wall clock", w50.SampleRate + " " + w50.Samples + " " + w50.Results[0].Consumed + " " + w50.Results[0].Final + " " + w50.Finals.Count, "1000000 2000000 1534000 True 1");
            Expect("51", "F7 finding: an extra 20 ms final", Found(BenchPaced.Finding("toy", Timeline(null, ("fire", 48000)), Timeline(null, ("fire", 48000), ("missiles", 52800)), "correct-fire", "correct-fire")), "case \"toy\": finals fast [\"fire\"], 20 ms [\"fire\",\"missiles\"]");
            Expect("52", "F7 finding: the classes differ", Found(BenchPaced.Finding("toy", Timeline(null, ("fire", 48000)), Timeline(null, ("fire", 48000)), "correct-fire", "missed")), "case \"toy\": class fast correct-fire, 20 ms missed");
            Expect("53", "F7 finding: finals and classes equal", Found(BenchPaced.Finding("toy", Timeline(null, ("fire", 48000)), Timeline(null, ("fire", 48000)), "correct-fire", "correct-fire")), "none");

            Console.WriteLine();
            Console.WriteLine(
                $"{checks - failures}/{checks} metric checks verified against hand-computed toy cases."
            );
            return failures == 0 ? 0 : 1;
        }

        static int TruthCheck(JoinedInput.Manifest manifest, string root)
        {
            var missing = new List<string>();
            foreach (JoinedInput.ManifestCase c in manifest.Cases)
                if (c.Speech == null)
                    missing.Add(manifest.Path + ": case \"" + c.File + "\": no key `speech`");
            if (missing.Count > 0)
                throw new JoinedInput.Stop(2, missing.ToArray());

            string Shown(long? v) =>
                v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "none";

            var failed = new List<string>();
            foreach (JoinedInput.ManifestCase c in manifest.Cases)
            {
                short[] pcm = ReadPcm(Path.Combine(root, c.File));
                int n =
                    c.Segments != null && c.Segments.Length > 0 ? c.Segments.Length
                    : !string.IsNullOrEmpty(c.Phrase) ? 1
                    : 0;
                int k = c.Speech.Length;
                bool ok;
                string detail;
                if (n > 0)
                {
                    long? last = null;
                    for (long i = (long)pcm.Length - TailSamples - 1; i >= 0; i--)
                        if (Math.Abs((int)pcm[i]) >= TruthFloor)
                        {
                            last = i + 1;
                            break;
                        }
                    long? end = k > 0 ? c.Speech[k - 1].End : null;
                    long? diff = end.HasValue && last.HasValue ? end - last : null;
                    bool inClip = true;
                    foreach (var span in c.Speech)
                        if (span.End > pcm.Length)
                            inClip = false;
                    ok =
                        k == n
                        && inClip
                        && diff.HasValue
                        && Math.Abs(diff.Value) <= FrameTolerance;
                    detail =
                        $"segments={k}/{n} end={Shown(end)} last={Shown(last)} diff={Shown(diff)}";
                }
                else
                {
                    long floorSamples = 0;
                    foreach (short s in pcm)
                        if (Math.Abs((int)s) >= TruthFloor)
                            floorSamples++;
                    ok = k == 0 && floorSamples == 0;
                    detail = $"segments={k}/0 floor-samples={Shown(floorSamples)}";
                }
                if (!ok)
                    failed.Add(c.Name);
                Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {c.Name, -68} {detail}");
            }

            int total = manifest.Cases.Count;
            if (failed.Count > 0)
                throw new JoinedInput.Stop(
                    2,
                    "truth check failed on "
                        + failed.Count
                        + "/"
                        + total
                        + " clips: "
                        + string.Join(", ", failed)
                );
            Console.WriteLine();
            Console.WriteLine(
                $"{total}/{total} clips' speech truth verified against the committed WAVs."
            );
            return 0;
        }

        static short[] ReadPcm(string path)
        {
            float[] f;
            try
            {
                f = VoXR.Testing.VoxrWavReader.Read(File.ReadAllBytes(path), path);
            }
            catch (InvalidDataException x)
            {
                throw new JoinedInput.Stop(2, x.Message);
            }
            var pcm = new short[f.Length];
            for (int i = 0; i < pcm.Length; i++)
                pcm[i] = (short)(f[i] * 32768f);
            return pcm;
        }
    }
}
