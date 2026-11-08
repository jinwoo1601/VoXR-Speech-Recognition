// ============================================================================
// Purpose:  The bench's metric definitions: reference and hypothesis words, the alignment, WER, ECE, NCE and the command rates
// Layer:    Planning.AbRig
// Owns:     BenchMetrics (internal static class), BenchMetrics.Value (internal readonly struct), BenchMetrics.Alignment (internal sealed class), BenchMetrics.Scored (internal sealed class), BenchMetrics.CommandScore (internal sealed class)
// Depends:  JoinedInput, JoinedScorer
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AbRig
{
    static class BenchMetrics
    {
        internal const int EceBins = 10;

        internal readonly struct Value
        {
            internal readonly double Number;
            internal readonly string Reason;

            Value(double number, string reason)
            {
                Number = number;
                Reason = reason;
            }

            internal static Value Of(double number) => new Value(number, null);

            internal static Value Undefined(string reason) => new Value(0, reason);

            public override string ToString() =>
                Reason == null
                    ? Number.ToString("F4", CultureInfo.InvariantCulture)
                    : "undefined (" + Reason + ")";
        }

        internal sealed class Alignment
        {
            internal int S;
            internal int D;
            internal int I;
            internal int N;
            internal bool[] Correct;
            internal Value Wer;
        }

        internal sealed class Scored
        {
            internal int S;
            internal int D;
            internal int I;
            internal int N;
            internal int Stray;
            internal Value Wer;
            internal Value Ece;
            internal Value Nce;
        }

        internal sealed class CommandScore
        {
            internal int[] Counts;
            internal int Positives;
            internal Value Intent;
            internal Value Semantic;
            internal Value Phantom;
        }

        static readonly string[] PositiveClasses =
        {
            "correct-fire",
            "wrong-slots",
            "wrong-intent",
            "missed",
        };

        internal static string[] Reference(string phrase, string[] segments)
        {
            string text = phrase ?? string.Join(" ", segments ?? Array.Empty<string>());
            return text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        internal static JoinedInput.Word[] Hypothesis(List<JoinedInput.Word[]> finals)
        {
            var words = new List<JoinedInput.Word>();
            foreach (var final in finals)
                words.AddRange(final);
            return words.ToArray();
        }

        static Value WordErrorRate(int errors, int n) =>
            n == 0 ? Value.Undefined("no reference words") : Value.Of((double)errors / n);

        internal static Alignment Align(string[] reference, JoinedInput.Word[] hypothesis)
        {
            int n = reference.Length;
            int m = hypothesis.Length;
            var t = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++)
                t[i, 0] = i;
            for (int j = 0; j <= m; j++)
                t[0, j] = j;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int differ = reference[i - 1] == hypothesis[j - 1].Text ? 0 : 1;
                    t[i, j] = Math.Min(
                        t[i - 1, j - 1] + differ,
                        Math.Min(t[i - 1, j] + 1, t[i, j - 1] + 1)
                    );
                }
            }

            var a = new Alignment { N = n, Correct = new bool[m] };
            int x = n;
            int y = m;
            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0)
                {
                    int differ = reference[x - 1] == hypothesis[y - 1].Text ? 0 : 1;
                    if (t[x - 1, y - 1] + differ == t[x, y])
                    {
                        if (differ == 0)
                            a.Correct[y - 1] = true;
                        else
                            a.S++;
                        x--;
                        y--;
                        continue;
                    }
                }
                if (x > 0 && t[x - 1, y] + 1 == t[x, y])
                {
                    a.D++;
                    x--;
                }
                else
                {
                    a.I++;
                    y--;
                }
            }
            a.Wer = WordErrorRate(a.S + a.D + a.I, n);
            return a;
        }

        internal static Scored Score(IReadOnlyList<(string[] Ref, JoinedInput.Word[] Hyp)> cases)
        {
            var s = new Scored();
            var words = new List<(double Conf, bool Correct)>();
            foreach (var (reference, hypothesis) in cases)
            {
                if (reference.Length == 0)
                {
                    s.Stray += hypothesis.Length;
                    continue;
                }
                var a = Align(reference, hypothesis);
                s.S += a.S;
                s.D += a.D;
                s.I += a.I;
                s.N += a.N;
                for (int k = 0; k < hypothesis.Length; k++)
                    words.Add((hypothesis[k].Conf, a.Correct[k]));
            }
            s.Wer = WordErrorRate(s.S + s.D + s.I, s.N);
            s.Ece = Ece(words);
            s.Nce = Nce(words);
            return s;
        }

        static void CheckConfidences(IReadOnlyList<(double Conf, bool Correct)> words)
        {
            foreach (var (conf, _) in words)
                if (!(conf >= 0 && conf <= 1))
                    throw new JoinedInput.Stop(
                        2,
                        "confidence "
                            + conf.ToString("R", CultureInfo.InvariantCulture)
                            + " outside [0, 1]"
                    );
        }

        internal static Value Ece(IReadOnlyList<(double Conf, bool Correct)> words)
        {
            CheckConfidences(words);
            if (words.Count == 0)
                return Value.Undefined("no words");
            var count = new int[EceBins];
            var right = new int[EceBins];
            var confSum = new double[EceBins];
            foreach (var (conf, correct) in words)
            {
                int bin = Math.Min((int)Math.Floor(Math.Round(conf * EceBins, 6)), EceBins - 1);
                count[bin]++;
                if (correct)
                    right[bin]++;
                confSum[bin] += conf;
            }
            double ece = 0;
            for (int b = 0; b < EceBins; b++)
            {
                if (count[b] == 0)
                    continue;
                ece +=
                    (double)count[b]
                    / words.Count
                    * Math.Abs((double)right[b] / count[b] - confSum[b] / count[b]);
            }
            return Value.Of(ece);
        }

        internal static Value Nce(IReadOnlyList<(double Conf, bool Correct)> words)
        {
            CheckConfidences(words);
            if (words.Count == 0)
                return Value.Undefined("no words");
            int right = 0;
            bool wrongAtOne = false;
            bool rightAtZero = false;
            foreach (var (conf, correct) in words)
            {
                if (correct)
                {
                    right++;
                    if (conf == 0)
                        rightAtZero = true;
                }
                else if (conf == 1)
                {
                    wrongAtOne = true;
                }
            }
            if (right == words.Count)
                return Value.Undefined("all words right");
            if (right == 0)
                return Value.Undefined("all words wrong");
            if (wrongAtOne)
                return Value.Undefined("a wrong word at confidence 1");
            if (rightAtZero)
                return Value.Undefined("a right word at confidence 0");

            double p = (double)right / words.Count;
            double hBase = -(p * Math.Log2(p) + (1 - p) * Math.Log2(1 - p));
            double cross = 0;
            foreach (var (conf, correct) in words)
                cross += correct ? -Math.Log2(conf) : -Math.Log2(1 - conf);
            double hConf = cross / words.Count;
            return Value.Of((hBase - hConf) / hBase);
        }

        internal static CommandScore Command(IReadOnlyList<(string Class, int ExtraFires)> cases)
        {
            var counts = new int[JoinedScorer.Classes.Length];
            int extra = 0;
            foreach (var (cls, extraFires) in cases)
            {
                int at = Array.IndexOf(JoinedScorer.Classes, cls);
                if (at < 0)
                    throw new JoinedInput.Stop(2, "unknown class `" + cls + "`");
                counts[at]++;
                if (Array.IndexOf(PositiveClasses, cls) >= 0)
                    extra += extraFires;
            }
            int Count(string cls) => counts[Array.IndexOf(JoinedScorer.Classes, cls)];

            int positives = 0;
            foreach (string cls in PositiveClasses)
                positives += Count(cls);
            var score = new CommandScore { Counts = counts, Positives = positives };
            if (positives == 0)
            {
                var none = Value.Undefined("no cases with an expected intent");
                score.Intent = none;
                score.Semantic = none;
                score.Phantom = none;
                return score;
            }
            score.Intent = Value.Of(
                (double)(Count("correct-fire") + Count("wrong-slots")) / positives
            );
            score.Semantic = Value.Of((double)Count("correct-fire") / positives);
            score.Phantom = Value.Of((double)extra / positives);
            return score;
        }
    }
}
