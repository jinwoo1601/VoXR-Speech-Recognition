// ============================================================================
// Purpose:  The rig's --bench verbs: the hook set from a module initializer, verb dispatch, the metric toy checks
// Layer:    Planning.AbRig
// Owns:     Bench (internal static class)
// Depends:  Program, BenchMetrics, JoinedInput, VoxrSlotDefinition, VoxrCommandDefinition
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using VoXR.Commands;

namespace AbRig
{
    static class Bench
    {
        static int failures;
        static int checks;

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
                throw new JoinedInput.Stop(
                    2,
                    verb
                        + " is not built yet (bench-fast-replay Phase "
                        + (verb == "truth-check" ? "2" : "3")
                        + ")"
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
                Console.Error.WriteLine("bench: " + e.GetType().Name + ": " + e.Message);
                return 2;
            }
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
    }
}
