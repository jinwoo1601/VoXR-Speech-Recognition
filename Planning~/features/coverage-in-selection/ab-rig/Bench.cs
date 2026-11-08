// ============================================================================
// Purpose:  The rig's --bench verbs: the hook set from a module initializer, verb dispatch, the metric toy checks, the speech-truth check, the fast run
// Layer:    Planning.AbRig
// Owns:     Bench (internal static class)
// Depends:  Program, BenchMetrics, BenchReport, Joined, JoinedInput, JoinedRecogniser, JoinedScorer, VoxrSlotDefinition, VoxrCommandDefinition
// ============================================================================
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
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
                    return TruthCheck(args[3], args[5]);
                }
                if (
                    args.Length != 14
                    || args[2] != "--dump"
                    || args[4] != "--manifest"
                    || args[6] != "--pin"
                    || args[8] != "--fixtures-root"
                    || args[10] != "--harness-exit"
                    || args[12] != "--report"
                    || (args[11] != "0" && args[11] != "1")
                )
                    throw new JoinedInput.Stop(
                        2,
                        "usage: --bench run --dump <dump.jsonl> --manifest <manifest.json> --pin <expectations.json> --fixtures-root <dir> --harness-exit 0|1 --report <report.md>"
                    );
                return FastRun(
                    args[3],
                    args[5],
                    args[7],
                    args[9],
                    int.Parse(args[11]),
                    args[13],
                    grammarJson,
                    slots,
                    commands
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
            string grammarJson,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            reading = reportPath;
            if (File.Exists(reportPath))
                File.Delete(reportPath);
            reading = "./STAGED_FROM";
            string[] stagedFrom = JoinedInput.SplitLines(File.ReadAllText("STAGED_FROM"));
            reading = dumpPath;
            JoinedInput.Dump dump = JoinedInput.ReadDump(dumpPath);
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
            TruthCheck(manifestPath, root);
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
                bool same = expected.Length == actual.Length;
                for (int i = 0; same && i < expected.Length; i++)
                    same = string.Equals(expected[i], actual[i], StringComparison.Ordinal);
                if (!same)
                {
                    failing.Add(
                        "self-check: case \""
                            + c.Name
                            + "\": finals expected "
                            + BenchReport.JsonArray(expected)
                            + ", actual "
                            + BenchReport.JsonArray(actual)
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

            var header = new BenchReport.Header
            {
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
            string text = BenchReport.Build(header, rows, command, scored);
            reading = reportPath;
            BenchReport.Write(reportPath, text);
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
            string firstPartial = null;
            var finals = new List<string>();
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
                            firstPartial = BenchReport.Offset(x.Consumed.Value - e, r);
                    }
                    else if (p.GetProperty("text").GetString().Length > 0)
                        finals.Add(BenchReport.Offset(x.Consumed.Value - e, r));
                }
            }
            row.FirstPartial = firstPartial ?? "none";
            row.Finals = finals.Count > 0 ? string.Join(", ", finals) : "none";
            if (s.FireTimes.Length > 0)
            {
                long k = (long)Math.Round(s.FireTimes[0] * t);
                row.CommandEvent = BenchReport.Offset(k * r - t * e, t * r);
            }
            else
                row.CommandEvent = "none";
            return row;
        }

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

            Console.WriteLine();
            Console.WriteLine(
                $"{checks - failures}/{checks} metric checks verified against hand-computed toy cases."
            );
            return failures == 0 ? 0 : 1;
        }

        static int TruthCheck(string manifestPath, string root)
        {
            JoinedInput.Manifest manifest = JoinedInput.ReadManifest(manifestPath);
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
                        && last.HasValue
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
            byte[] b = File.ReadAllBytes(path);
            var bad = new JoinedInput.Stop(2, path + ": not a 16-bit mono 48000 Hz PCM WAV");
            if (
                b.Length < 12
                || Encoding.ASCII.GetString(b, 0, 4) != "RIFF"
                || Encoding.ASCII.GetString(b, 8, 4) != "WAVE"
            )
                throw bad;
            bool format = false;
            long at = 12;
            while (at + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, (int)at, 4);
                long size = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((int)at + 4));
                int body = (int)at + 8;
                if (body + size > b.Length)
                    throw bad;
                if (id == "fmt ")
                {
                    if (
                        size < 16
                        || BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body)) != 1
                        || BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body + 2)) != 1
                        || BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(body + 4)) != 48000
                        || BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body + 14)) != 16
                    )
                        throw bad;
                    format = true;
                }
                else if (id == "data")
                {
                    if (!format)
                        throw bad;
                    var pcm = new short[size / 2];
                    for (int i = 0; i < pcm.Length; i++)
                        pcm[i] = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(body + 2 * i));
                    return pcm;
                }
                at = body + size + (size & 1);
            }
            throw bad;
        }
    }
}
