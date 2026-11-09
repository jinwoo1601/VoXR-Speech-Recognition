// ============================================================================
// Purpose:  The bench's condition pipeline: keyed streams, speed resampling, the synthetic room response and its RT60, pink noise, the mic filter, SNR and level, scaled truth and the grid; the condition and condition-check verbs
// Layer:    Planning.AbRig
// Owns:     BenchConditions (internal static class), BenchConditions.Rng (internal sealed class), BenchConditions.Step (internal sealed class)
// Depends:  Bench, JoinedInput
// ============================================================================
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Clip = (
    string File,
    string Source,
    string Condition,
    short[] Y,
    (long Start, long End)[] Speech,
    double T,
    int Draws,
    double Level,
    double Snr,
    double Peak,
    double Rt60,
    double NoiseRms,
    string Partner,
    double Rms
);

namespace AbRig
{
    static class BenchConditions
    {
        internal sealed class Rng
        {
            ulong s0;
            ulong s1;
            ulong s2;
            ulong s3;

            internal Rng(ulong seed, string clip, string step, string tag)
            {
                ulong x = seed ^ Fnv(clip + "|" + step + "|" + tag);
                s0 = SplitMix(ref x);
                s1 = SplitMix(ref x);
                s2 = SplitMix(ref x);
                s3 = SplitMix(ref x);
            }

            static ulong SplitMix(ref ulong x)
            {
                x += 0x9E3779B97F4A7C15UL;
                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            internal ulong Next()
            {
                ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;
                ulong t = s1 << 17;
                s2 ^= s0;
                s3 ^= s1;
                s1 ^= s2;
                s0 ^= s3;
                s2 ^= t;
                s3 = BitOperations.RotateLeft(s3, 45);
                return result;
            }

            internal double Uniform() => (Next() >> 11) * (1.0 / 9007199254740992.0);

            internal double Gaussian()
            {
                double u1 = Uniform();
                double u2 = Uniform();
                return Math.Sqrt(-2 * Math.Log(1 - u1)) * Math.Cos(2 * Math.PI * u2);
            }
        }

        internal sealed class Step
        {
            internal string Id;
            internal int Speed;
            internal int SnrDb;
        }

        const int Rate = 48000;
        const double HighPassHz = 100;
        const double PeakHz = 3000;
        const double PeakGainDb = 4;
        const double PeakQ = 1;
        const double LowPassHz = 7000;
        const double DrrDb = 10;
        const double LevelMin = 0.04;
        const double LevelMax = 0.4;
        const double Rt60Min = 0.2;
        const double Rt60Max = 0.5;
        const double SnrTolerance = 0.5;
        const int MaxDraws = 100;
        static readonly double ButterworthQ = Math.Sqrt(0.5);
        static readonly int[] Speeds = { 9, 10, 11 };
        static readonly int[] SnrSteps = { 20, 15, 10, 5 };
        static readonly List<Step> Grid = MakeGrid();

        static int failures;
        static int checks;

        static List<Step> MakeGrid()
        {
            var grid = new List<Step>();
            foreach (int speed in Speeds)
                foreach (int snr in SnrSteps)
                    grid.Add(
                        new Step
                        {
                            Id = "s" + speed / 10 + "." + speed % 10 + "-snr" + snr,
                            Speed = speed,
                            SnrDb = snr,
                        }
                    );
            return grid;
        }

        static List<Step> Steps(string name)
        {
            if (name == "grid")
                return new List<Step>(Grid);
            foreach (Step s in Grid)
                if (s.Id == name)
                    return new List<Step> { s };
            var ids = new List<string>();
            foreach (Step s in Grid)
                ids.Add(s.Id);
            throw new JoinedInput.Stop(
                2,
                "condition: unknown condition \"" + name + "\": valid: grid, " + string.Join(", ", ids)
            );
        }

        static ulong Fnv(string text)
        {
            ulong h = 0xCBF29CE484222325UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                h ^= b;
                h *= 0x100000001B3UL;
            }
            return h;
        }

        static string ClipName(JoinedInput.ManifestCase c, Step step) =>
            Path.GetFileNameWithoutExtension(c.Name) + "__" + step.Id + ".wav";

        static long Scale(long x, int f) => (20 * x + f) / (2 * f);

        static (long Start, long End)[] ScaleSpeech((long Start, long End)[] speech, int f)
        {
            var scaled = new (long Start, long End)[speech.Length];
            for (int i = 0; i < speech.Length; i++)
                scaled[i] = (Scale(speech[i].Start, f), Scale(speech[i].End, f));
            return scaled;
        }

        static int Pow2(long n)
        {
            int p = 1;
            while (p < n)
                p <<= 1;
            return p;
        }

        static void Fft(double[] re, double[] im, bool inverse)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            double sign = inverse ? 1 : -1;
            for (int len = 2; len <= n; len <<= 1)
            {
                int half = len >> 1;
                for (int k = 0; k < half; k++)
                {
                    double angle = sign * 2 * Math.PI * k / len;
                    double wr = Math.Cos(angle);
                    double wi = Math.Sin(angle);
                    for (int i = k; i < n; i += len)
                    {
                        int j = i + half;
                        double xr = re[j] * wr - im[j] * wi;
                        double xi = re[j] * wi + im[j] * wr;
                        re[j] = re[i] - xr;
                        im[j] = im[i] - xi;
                        re[i] += xr;
                        im[i] += xi;
                    }
                }
            }
            if (inverse)
                for (int i = 0; i < n; i++)
                {
                    re[i] /= n;
                    im[i] /= n;
                }
        }

        static double[] Convolve(double[] x, double[] h)
        {
            int n = Pow2(x.Length + h.Length - 1);
            var xr = new double[n];
            var xi = new double[n];
            var hr = new double[n];
            var hi = new double[n];
            Array.Copy(x, xr, x.Length);
            Array.Copy(h, hr, h.Length);
            Fft(xr, xi, false);
            Fft(hr, hi, false);
            for (int k = 0; k < n; k++)
            {
                double r = xr[k] * hr[k] - xi[k] * hi[k];
                double i = xr[k] * hi[k] + xi[k] * hr[k];
                xr[k] = r;
                xi[k] = i;
            }
            Fft(xr, xi, true);
            var y = new double[x.Length];
            Array.Copy(xr, y, x.Length);
            return y;
        }

        static double[] Resample(double[] input, int f)
        {
            if (f == 10)
                return (double[])input.Clone();
            var output = new double[Scale(input.Length, f)];
            double c = Math.Min(1.0, 10.0 / f);
            double w = 32 / c;
            for (long k = 0; k < output.Length; k++)
            {
                double x = (double)(k * f) / 10;
                long lo = Math.Max(0, (long)Math.Ceiling(x - w));
                long hi = Math.Min(input.Length - 1, (long)Math.Floor(x + w));
                double sum = 0;
                for (long j = lo; j <= hi; j++)
                {
                    double t = x - j;
                    double z = c * t;
                    double sinc = z == 0 ? 1 : Math.Sin(Math.PI * z) / (Math.PI * z);
                    double cw = Math.Cos(Math.PI * t / w);
                    double window = 0.42 + 0.5 * cw + 0.08 * (2 * cw * cw - 1);
                    sum += input[j] * c * sinc * window;
                }
                output[k] = sum;
            }
            return output;
        }

        static (double[] H, double T, int Draws, double Rt60) Room(Rng rng, string clip)
        {
            for (int draw = 1; draw <= MaxDraws; draw++)
            {
                double t = 0.2 + 0.3 * rng.Uniform();
                var h = new double[(int)Math.Round(2 * t * Rate)];
                h[0] = 1;
                double tail = 0;
                for (int k = 1; k < h.Length; k++)
                {
                    h[k] = rng.Gaussian() * Math.Pow(10, -3.0 * k / (Rate * t));
                    tail += h[k] * h[k];
                }
                double gain = Math.Sqrt(Math.Pow(10, -DrrDb / 10) / tail);
                for (int k = 1; k < h.Length; k++)
                    h[k] *= gain;
                double rt60 = Rt60(h);
                if (rt60 >= Rt60Min && rt60 <= Rt60Max)
                    return (h, t, draw, rt60);
            }
            throw new JoinedInput.Stop(
                2,
                "condition: clip \"" + clip + "\": no RT60 in 0.2–0.5 s in 100 draws"
            );
        }

        static double Rt60(double[] h)
        {
            var edc = new double[h.Length];
            double acc = 0;
            for (int k = h.Length - 1; k >= 0; k--)
            {
                acc += h[k] * h[k];
                edc[k] = acc;
            }
            var xs = new List<double>();
            var ys = new List<double>();
            for (int k = 0; k < h.Length; k++)
            {
                double db = 10 * Math.Log10(edc[k] / edc[0]);
                if (db <= -5 && db >= -25)
                {
                    xs.Add(k);
                    ys.Add(db);
                }
            }
            double mx = 0;
            double my = 0;
            for (int i = 0; i < xs.Count; i++)
            {
                mx += xs[i];
                my += ys[i];
            }
            mx /= xs.Count;
            my /= xs.Count;
            double sxy = 0;
            double sxx = 0;
            for (int i = 0; i < xs.Count; i++)
            {
                sxy += (xs[i] - mx) * (ys[i] - my);
                sxx += (xs[i] - mx) * (xs[i] - mx);
            }
            return -60 / (sxy / sxx) / Rate;
        }

        static double[] Pink(Rng rng, int len)
        {
            int n = Pow2(len);
            var re = new double[n];
            var im = new double[n];
            for (int i = 0; i < n; i++)
                re[i] = rng.Gaussian();
            Fft(re, im, false);
            for (int k = 0; k < n; k++)
            {
                int m = k <= n / 2 ? k : n - k;
                double hz = (double)Rate * m / n;
                double s = hz < 20 ? 0 : 1 / Math.Sqrt(hz);
                re[k] *= s;
                im[k] *= s;
            }
            Fft(re, im, true);
            var noise = new double[len];
            Array.Copy(re, noise, len);
            return noise;
        }

        static (double B0, double B1, double B2, double A1, double A2) HighPass(double hz, double q)
        {
            double w = 2 * Math.PI * hz / Rate;
            double cos = Math.Cos(w);
            double alpha = Math.Sin(w) / (2 * q);
            double a0 = 1 + alpha;
            return (
                (1 + cos) / 2 / a0,
                -(1 + cos) / a0,
                (1 + cos) / 2 / a0,
                -2 * cos / a0,
                (1 - alpha) / a0
            );
        }

        static (double B0, double B1, double B2, double A1, double A2) LowPass(double hz, double q)
        {
            double w = 2 * Math.PI * hz / Rate;
            double cos = Math.Cos(w);
            double alpha = Math.Sin(w) / (2 * q);
            double a0 = 1 + alpha;
            return (
                (1 - cos) / 2 / a0,
                (1 - cos) / a0,
                (1 - cos) / 2 / a0,
                -2 * cos / a0,
                (1 - alpha) / a0
            );
        }

        static (double B0, double B1, double B2, double A1, double A2) Peaking(
            double hz,
            double gainDb,
            double q
        )
        {
            double w = 2 * Math.PI * hz / Rate;
            double cos = Math.Cos(w);
            double alpha = Math.Sin(w) / (2 * q);
            double a = Math.Pow(10, gainDb / 40);
            double a0 = 1 + alpha / a;
            return (
                (1 + alpha * a) / a0,
                -2 * cos / a0,
                (1 - alpha * a) / a0,
                -2 * cos / a0,
                (1 - alpha / a) / a0
            );
        }

        static double[] Biquad(
            double[] x,
            (double B0, double B1, double B2, double A1, double A2) q
        )
        {
            var y = new double[x.Length];
            double x1 = 0;
            double x2 = 0;
            double y1 = 0;
            double y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double v = q.B0 * x[i] + q.B1 * x1 + q.B2 * x2 - q.A1 * y1 - q.A2 * y2;
                x2 = x1;
                x1 = x[i];
                y2 = y1;
                y1 = v;
                y[i] = v;
            }
            return y;
        }

        static double[] Filter(double[] x) =>
            Biquad(
                Biquad(
                    Biquad(x, HighPass(HighPassHz, ButterworthQ)),
                    Peaking(PeakHz, PeakGainDb, PeakQ)
                ),
                LowPass(LowPassHz, ButterworthQ)
            );

        static (double A, double[] M) Mix(
            double[] s,
            double[] n,
            int snrDb,
            (long Start, long End)[] segments
        )
        {
            double ss = 0;
            double nn = 0;
            foreach (var seg in segments)
                for (long i = seg.Start; i < seg.End; i++)
                {
                    ss += s[i] * s[i];
                    nn += n[i] * n[i];
                }
            double a = Math.Sqrt(ss / (nn * Math.Pow(10, snrDb / 10.0)));
            var m = new double[s.Length];
            for (int i = 0; i < s.Length; i++)
                m[i] = s[i] + a * n[i];
            return (a, m);
        }

        static double DrawLevel(Rng rng) => LevelMin * Math.Pow(10, rng.Uniform());

        static short[] Level(double[] m, double level, out double g)
        {
            double peak = 0;
            foreach (double v in m)
                peak = Math.Max(peak, Math.Abs(v));
            g = level / peak;
            var y = new short[m.Length];
            for (int i = 0; i < m.Length; i++)
                y[i] = (short)Math.Round(g * m[i] * 32768);
            return y;
        }

        static double Realized(
            short[] y,
            double[] n,
            double ga,
            (long Start, long End)[] segments
        )
        {
            double signal = 0;
            double noise = 0;
            foreach (var seg in segments)
                for (long i = seg.Start; i < seg.End; i++)
                {
                    double v = ga * n[i];
                    double d = y[i] / 32768.0 - v;
                    signal += d * d;
                    noise += v * v;
                }
            return 10 * Math.Log10(signal / noise);
        }

        static double Peak(short[] y)
        {
            int peak = 0;
            foreach (short v in y)
                peak = Math.Max(peak, Math.Abs((int)v));
            return peak / 32768.0;
        }

        static double Rms(double[] v)
        {
            double sum = 0;
            foreach (double x in v)
                sum += x * x;
            return Math.Sqrt(sum / v.Length);
        }

        static string Shown(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

        static string Outside(string clip, int step, double snr, double peak, double rt60)
        {
            string at = "condition: clip \"" + clip + "\": ";
            if (!(Math.Abs(snr - step) <= SnrTolerance))
                return at + "realized SNR " + Shown(snr) + " dB at step " + step + ": outside F2";
            if (!(peak >= LevelMin && peak <= LevelMax))
                return at + "peak " + Shown(peak) + ": outside F2";
            if (!(rt60 >= Rt60Min && rt60 <= Rt60Max))
                return at + "RT60 " + Shown(rt60) + " s: outside F2";
            return null;
        }

        static short[] ToPcm(double[] s)
        {
            var pcm = new short[s.Length];
            for (int i = 0; i < s.Length; i++)
                pcm[i] = (short)(s[i] * 32768);
            return pcm;
        }

        static void SpeedTruth(
            string clip,
            double[] s1,
            JoinedInput.ManifestCase c,
            (long Start, long End)[] speech,
            int f
        )
        {
            if (!Bench.TruthRule(ToPcm(s1), c, speech, Scale(Rate, f), out string detail))
                throw new JoinedInput.Stop(
                    2,
                    "condition: clip \""
                        + clip
                        + "\": truth rule failed on the speed stage: "
                        + detail
                );
        }

        static Clip SpeechClip(ulong seed, JoinedInput.ManifestCase c, Step step, double[] source)
        {
            string clip = ClipName(c, step);
            double[] s1 = Resample(source, step.Speed);
            (long Start, long End)[] speech = ScaleSpeech(c.Speech, step.Speed);
            SpeedTruth(clip, s1, c, speech, step.Speed);
            var room = Room(new Rng(seed, clip, step.Id, "room"), clip);
            double[] s = Filter(Convolve(s1, room.H));
            double[] n = Filter(Pink(new Rng(seed, clip, step.Id, "noise"), s1.Length));
            var mix = Mix(s, n, step.SnrDb, speech);
            double level = DrawLevel(new Rng(seed, clip, step.Id, "level"));
            short[] y = Level(mix.M, level, out double g);
            double ga = g * mix.A;
            double snr = Realized(y, n, ga, speech);
            double peak = Peak(y);
            string outside = Outside(clip, step.SnrDb, snr, peak, room.Rt60);
            if (outside != null)
                throw new JoinedInput.Stop(2, outside);
            var carried = new double[n.Length];
            for (int i = 0; i < n.Length; i++)
                carried[i] = ga * n[i];
            return (
                clip,
                c.Name,
                step.Id,
                y,
                speech,
                room.T,
                room.Draws,
                level,
                snr,
                peak,
                room.Rt60,
                Rms(carried),
                null,
                0
            );
        }

        static Clip FreeClip(
            ulong seed,
            JoinedInput.ManifestCase c,
            Step step,
            double[] source,
            List<Clip> speech
        )
        {
            string clip = ClipName(c, step);
            double[] s1 = Resample(source, step.Speed);
            (long Start, long End)[] scaled = ScaleSpeech(c.Speech, step.Speed);
            SpeedTruth(clip, s1, c, scaled, step.Speed);
            Clip partner = speech[
                (int)Math.Floor(new Rng(seed, clip, step.Id, "partner").Uniform() * speech.Count)
            ];
            double[] n = Filter(Pink(new Rng(seed, clip, step.Id, "noise"), partner.Y.Length));
            double k = partner.NoiseRms / Rms(n);
            var y = new short[n.Length];
            for (int i = 0; i < n.Length; i++)
            {
                double v = Math.Round(k * n[i] * 32768);
                if (v < short.MinValue || v > short.MaxValue)
                    throw new JoinedInput.Stop(
                        2,
                        "condition: clip \"" + clip + "\": a sample past 16 bits"
                    );
                y[i] = (short)v;
            }
            return (
                clip,
                c.Name,
                step.Id,
                y,
                scaled,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                partner.File,
                partner.NoiseRms
            );
        }

        static void WriteWav(string path, short[] y)
        {
            var b = new byte[44 + 2 * y.Length];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(b, 0);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), 36 + 2 * y.Length);
            Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(b, 8);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16), 16);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(20), 1);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(22), 1);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24), Rate);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(28), Rate * 2);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(32), 2);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(34), 16);
            Encoding.ASCII.GetBytes("data").CopyTo(b, 36);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(40), 2 * y.Length);
            for (int i = 0; i < y.Length; i++)
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(44 + 2 * i), y[i]);
            File.WriteAllBytes(path, b);
        }

        static void WriteJson(string path, Action<Utf8JsonWriter> body)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew))
            {
                using (
                    var w = new Utf8JsonWriter(
                        stream,
                        new JsonWriterOptions
                        {
                            Indented = true,
                            NewLine = "\n",
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        }
                    )
                )
                    body(w);
                stream.WriteByte((byte)'\n');
            }
        }

        internal static int Condition(
            string dumpPath,
            string manifestPath,
            string pinPath,
            string root,
            string harnessPin,
            string outDir,
            string name,
            string seedText
        )
        {
            if (
                !ulong.TryParse(
                    seedText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong seed
                )
            )
                throw new JoinedInput.Stop(
                    2,
                    "condition: --seed \"" + seedText + "\" is not an unsigned 64-bit decimal"
                );
            List<Step> steps = Steps(name);
            Bench.Prelude prelude = Bench.FastPrelude(dumpPath, null, manifestPath, pinPath, root);
            JsonElement harness = JoinedInput.ParseObject(
                File.ReadAllText(harnessPin, JoinedInput.StrictUtf8),
                harnessPin
            );
            var cases = new List<JoinedInput.ManifestCase>();
            foreach (JoinedInput.ManifestCase c in prelude.Manifest.Cases)
                if (c.Category != "pause-lead" && c.Category != "ladder")
                    cases.Add(c);
            Console.Out.WriteLine(
                "condition: "
                    + cases.Count
                    + " cases conditioned, "
                    + (prelude.Manifest.Cases.Count - cases.Count)
                    + " clean only"
            );

            var sources = new double[cases.Count][];
            for (int i = 0; i < cases.Count; i++)
            {
                short[] pcm = Bench.ReadPcm(Path.Combine(root, cases[i].File));
                sources[i] = new double[pcm.Length];
                for (int k = 0; k < pcm.Length; k++)
                    sources[i][k] = pcm[k] / 32768.0;
            }
            var clips = new Clip[cases.Count, steps.Count];
            for (int j = 0; j < steps.Count; j++)
            {
                var speech = new List<Clip>();
                for (int i = 0; i < cases.Count; i++)
                    if (cases[i].Speech.Length > 0)
                    {
                        clips[i, j] = SpeechClip(seed, cases[i], steps[j], sources[i]);
                        speech.Add(clips[i, j]);
                    }
                for (int i = 0; i < cases.Count; i++)
                    if (cases[i].Speech.Length == 0)
                        clips[i, j] = FreeClip(seed, cases[i], steps[j], sources[i], speech);
            }

            string cond = outDir + "/cond";
            if (Path.Exists(cond))
                throw new JoinedInput.Stop(2, "condition: " + cond + " exists: empty it first");
            Directory.CreateDirectory(cond);
            foreach (Clip k in clips)
                WriteWav(cond + "/" + k.File, k.Y);

            WriteJson(
                cond + "/pin.json",
                w =>
                {
                    w.WriteStartObject();
                    foreach (JsonProperty p in harness.EnumerateObject())
                    {
                        if (p.Name != "cases")
                        {
                            p.WriteTo(w);
                            continue;
                        }
                        w.WriteStartArray("cases");
                        foreach (Clip k in clips)
                        {
                            w.WriteStartObject();
                            w.WriteString("file", k.File);
                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                    w.WriteEndObject();
                }
            );

            WriteJson(
                cond + "/manifest.json",
                w =>
                {
                    w.WriteStartObject();
                    w.WriteString("voice", prelude.Voice.GetString());
                    w.WriteString("provenance", prelude.Manifest.Provenance);
                    w.WriteStartArray("cases");
                    for (int i = 0; i < cases.Count; i++)
                        for (int j = 0; j < steps.Count; j++)
                        {
                            JoinedInput.ManifestCase c = cases[i];
                            Clip k = clips[i, j];
                            w.WriteStartObject();
                            w.WriteString("file", "cond/" + k.File);
                            w.WriteStartArray("speech");
                            foreach (var span in k.Speech)
                            {
                                w.WriteStartObject();
                                w.WriteNumber("start", span.Start);
                                w.WriteNumber("end", span.End);
                                w.WriteEndObject();
                            }
                            w.WriteEndArray();
                            w.WriteString("category", c.Category);
                            if (c.Phrase != null)
                                w.WriteString("phrase", c.Phrase);
                            else if (c.Segments != null)
                            {
                                w.WriteStartArray("segments");
                                foreach (string s in c.Segments)
                                    w.WriteStringValue(s);
                                w.WriteEndArray();
                            }
                            w.WriteString("expectedIntent", c.ExpectedIntent);
                            w.WriteStartArray("expectedSlots");
                            foreach (KeyValuePair<string, string> slot in c.ExpectedSlots)
                            {
                                w.WriteStartObject();
                                w.WriteString("name", slot.Key);
                                w.WriteString("value", slot.Value);
                                w.WriteEndObject();
                            }
                            w.WriteEndArray();
                            w.WriteEndObject();
                        }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
            );

            WriteJson(
                cond + "/conditions.json",
                w =>
                {
                    w.WriteStartObject();
                    w.WriteString("seed", seed.ToString(CultureInfo.InvariantCulture));
                    w.WriteStartArray("grid");
                    foreach (Step step in steps)
                        w.WriteStringValue(step.Id);
                    w.WriteEndArray();
                    w.WriteString("noise", "generated (pink Gaussian)");
                    w.WriteString("room", "synthetic (exponential decay)");
                    w.WriteNumber("highPassHz", HighPassHz);
                    w.WriteNumber("peakHz", PeakHz);
                    w.WriteNumber("peakGainDb", PeakGainDb);
                    w.WriteNumber("peakQ", PeakQ);
                    w.WriteNumber("lowPassHz", LowPassHz);
                    w.WriteNumber("drrDb", DrrDb);
                    w.WriteStartArray("levelRange");
                    w.WriteNumberValue(LevelMin);
                    w.WriteNumberValue(LevelMax);
                    w.WriteEndArray();
                    w.WriteString("levelDistribution", "log-uniform");
                    w.WriteStartArray("clips");
                    foreach (Clip k in clips)
                    {
                        w.WriteStartObject();
                        w.WriteString("file", k.File);
                        w.WriteString("source", k.Source);
                        w.WriteString("condition", k.Condition);
                        if (k.Partner == null)
                        {
                            w.WriteNumber("drawnRt60", k.T);
                            w.WriteNumber("draws", k.Draws);
                            w.WriteNumber("level", k.Level);
                            w.WriteNumber("snr", k.Snr);
                            w.WriteNumber("peak", k.Peak);
                            w.WriteNumber("rt60", k.Rt60);
                        }
                        else
                        {
                            w.WriteString("partner", k.Partner);
                            w.WriteNumber("rms", k.Rms);
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
            );

            JoinedInput.ReadManifest(cond + "/manifest.json");
            Console.Out.WriteLine(
                "condition: wrote "
                    + clips.Length
                    + " clips: "
                    + cases.Count
                    + " cases x "
                    + steps.Count
                    + " conditions, seed "
                    + seed.ToString(CultureInfo.InvariantCulture)
            );
            return 0;
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

        static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        static string First4(Rng r)
        {
            var parts = new string[4];
            for (int i = 0; i < 4; i++)
                parts[i] = r.Next().ToString("x16", CultureInfo.InvariantCulture);
            return string.Join(" ", parts);
        }

        static Rng Toy(string tag) => new Rng(7, "toy", "s1.0-snr20", tag);

        internal static int Check()
        {
            const string want6 = "daac876762908855 9b71c328362baba3 1abf92c5592540b9 67c22ae9cf9d190f";

            Expect("1", "FNV-1a 64: the empty string", Fnv("").ToString("x16", CultureInfo.InvariantCulture), "cbf29ce484222325");

            Expect("2", "streams: one key twice, first four outputs", First4(Toy("noise")) == First4(Toy("noise")) ? "same" : "differ", "same");

            ulong first = Toy("noise").Next();
            string Changed(Rng r) => r.Next() != first ? "differs" : "same";
            Expect(
                "3",
                "streams: seed, clip, step, tag each changed",
                string.Join(
                    " ",
                    Changed(new Rng(8, "toy", "s1.0-snr20", "noise")),
                    Changed(new Rng(7, "toy2", "s1.0-snr20", "noise")),
                    Changed(new Rng(7, "toy", "s1.0-snr15", "noise")),
                    Changed(new Rng(7, "toy", "s1.0-snr20", "room"))
                ),
                "differs differs differs differs"
            );

            Rng u4 = Toy("level");
            double sum4 = 0;
            bool in4 = true;
            for (int i = 0; i < 100000; i++)
            {
                double u = u4.Uniform();
                in4 &= u >= 0 && u < 1;
                sum4 += u;
            }
            double mean4 = sum4 / 100000;
            Expect("4", "uniforms: 10^5 in [0,1), mean within 0.005", in4 && Math.Abs(mean4 - 0.5) <= 0.005 ? "within" : "in=" + in4 + " mean=" + R(mean4), "within");

            Rng g5 = Toy("room");
            var x5 = new double[100000];
            double mean5 = 0;
            for (int i = 0; i < x5.Length; i++)
            {
                x5[i] = g5.Gaussian();
                mean5 += x5[i];
            }
            mean5 /= x5.Length;
            double var5 = 0;
            foreach (double v in x5)
                var5 += (v - mean5) * (v - mean5);
            var5 /= x5.Length;
            Expect("5", "Gaussians: 10^5, mean 0.01, variance 0.02", Math.Abs(mean5) <= 0.01 && Math.Abs(var5 - 1) <= 0.02 ? "within" : "mean=" + R(mean5) + " variance=" + R(var5), "within");

            Expect("6", "streams: (7, toy, s1.0-snr20, noise) pinned", First4(Toy("noise")), want6);

            Rng u7 = Toy("partner");
            var x7 = new double[1024];
            for (int i = 0; i < x7.Length; i++)
                x7[i] = u7.Uniform();
            var re7 = (double[])x7.Clone();
            var im7 = new double[x7.Length];
            Fft(re7, im7, false);
            Fft(re7, im7, true);
            double e7 = 0;
            for (int i = 0; i < x7.Length; i++)
                e7 = Math.Max(e7, Math.Max(Math.Abs(re7[i] - x7[i]), Math.Abs(im7[i])));
            Expect("7", "FFT then inverse: 1024 uniforms within 1e-9", e7 <= 1e-9 ? "within" : "error " + R(e7), "within");

            var a8 = new double[300];
            var b8 = new double[50];
            for (int i = 0; i < a8.Length; i++)
                a8[i] = u7.Uniform();
            for (int i = 0; i < b8.Length; i++)
                b8[i] = u7.Uniform();
            double[] c8 = Convolve(a8, b8);
            double e8 = c8.Length == a8.Length ? 0 : double.PositiveInfinity;
            for (int k = 0; k < Math.Min(c8.Length, a8.Length); k++)
            {
                double direct = 0;
                for (int j = 0; j <= Math.Min(k, b8.Length - 1); j++)
                    direct += a8[k - j] * b8[j];
                e8 = Math.Max(e8, Math.Abs(c8[k] - direct));
            }
            Expect("8", "FFT convolution 300 x 50 = direct within 1e-9", e8 <= 1e-9 ? "within" : "error " + R(e8), "within");

            double[] r9 = Resample(x7, 10);
            bool same9 = r9.Length == x7.Length;
            for (int i = 0; same9 && i < x7.Length; i++)
                same9 = r9[i] == x7[i];
            Expect("9", "resample at 1.0 is the input", same9 ? "equal" : "differs", "equal");

            var bad10 = new List<string>();
            foreach (long n in new long[] { 48000, 48001, 96001 })
                foreach (int f in new[] { 9, 11 })
                {
                    long len = Resample(new double[n], f).Length;
                    if (2 * Math.Abs(10 * n - f * len) > f)
                        bad10.Add(n + "@" + f + "=" + len);
                }
            Expect("10", "resample length: |10n - f len| <= f/2", bad10.Count == 0 ? "within" : string.Join(" ", bad10), "within");

            var sine = new double[48000];
            for (int k = 0; k < sine.Length; k++)
                sine[k] = 0.5 * Math.Sin(2 * Math.PI * 1000 * k / Rate);
            var bad11 = new List<string>();
            foreach (int f in new[] { 9, 11 })
            {
                double[] r = Resample(sine, f);
                double e = 0;
                for (int k = 1000; k <= r.Length - 1000; k++)
                    e = Math.Max(e, Math.Abs(r[k] - 0.5 * Math.Sin(2 * Math.PI * 100 * f * k / Rate)));
                if (e > 0.01)
                    bad11.Add(f + ": " + R(e));
            }
            Expect("11", "resample: a 1 kHz sine at 0.9 and 1.1 within 0.01", bad11.Count == 0 ? "within" : string.Join(" ", bad11), "within");

            var bad12 = new List<string>();
            foreach (int f in new[] { 9, 10, 11 })
                foreach (long x in new long[] { 0, 1, 24000, 114849 })
                {
                    long s = Scale(x, f);
                    if (2 * Math.Abs(10 * x - f * s) > f)
                        bad12.Add(x + "@" + f + "=" + s);
                }
            Expect("12", "Scale: |10x - f Scale| <= f/2", bad12.Count == 0 ? "within" : string.Join(" ", bad12), "within");

            var parts13 = new List<string>();
            foreach (int f in new[] { 9, 10, 11 })
            {
                var x13 = new double[120000];
                for (int k = 24000; k < 48000; k++)
                    x13[k] = 0.5 * Math.Sin(2 * Math.PI * 1000 * k / Rate);
                short[] pcm13 = ToPcm(Resample(x13, f));
                var c13 = new JoinedInput.ManifestCase { Phrase = "toy" };
                long tail13 = Scale(Rate, f);
                bool kept = Bench.TruthRule(pcm13, c13, new[] { (Scale(24000, f), Scale(48000, f)) }, tail13, out _);
                bool moved = Bench.TruthRule(pcm13, c13, new[] { (Scale(24000, f), Scale(48000, f) + 1920) }, tail13, out _);
                parts13.Add(f + ": " + (kept ? "pass" : "fail") + " " + (moved ? "pass" : "fail"));
            }
            Expect("13", "truth rule on the speed stage; end moved 1920", string.Join(", ", parts13), "9: pass fail, 10: pass fail, 11: pass fail");

            var h14 = new double[(int)Math.Round(2 * 0.3 * Rate)];
            for (int k = 0; k < h14.Length; k++)
                h14[k] = Math.Pow(10, -3.0 * k / (Rate * 0.3));
            double rt14 = Rt60(h14);
            Expect("14", "RT60 of a 0.3 s exponential decay within 1%", Math.Abs(rt14 - 0.3) <= 0.003 ? "within" : "rt60 " + R(rt14), "within");

            var bad15 = new List<string>();
            for (int i = 0; i < 50; i++)
            {
                string clip = "toy" + i;
                var room = Room(new Rng(7, clip, "s1.0-snr20", "room"), clip);
                double rt = Rt60(room.H);
                if (!(rt >= Rt60Min && rt <= Rt60Max) || room.H[0] != 1)
                    bad15.Add(clip + "=" + R(rt));
            }
            Expect("15", "Room: 50 keys, RT60 in [0.2, 0.5], h[0] 1", bad15.Count == 0 ? "within" : string.Join(" ", bad15), "within");

            int n16 = 1 << 18;
            var re16 = Pink(Toy("noise"), n16);
            var im16 = new double[n16];
            Fft(re16, im16, false);
            double Band(double lo, double hi)
            {
                double sum = 0;
                for (int k = 1; k < n16 / 2; k++)
                {
                    double hz = (double)Rate * k / n16;
                    if (hz >= lo && hz < hi)
                        sum += re16[k] * re16[k] + im16[k] * im16[k];
                }
                return sum;
            }
            double d16 = 10 * Math.Log10(Band(500, 1000) / Band(2000, 4000));
            Expect("16", "pink: [500,1000) and [2000,4000) Hz within 1 dB", Math.Abs(d16) <= 1 ? "within" : "difference " + R(d16) + " dB", "within");

            var hp = HighPass(HighPassHz, ButterworthQ);
            var lp = LowPass(LowPassHz, ButterworthQ);
            var pk = Peaking(PeakHz, PeakGainDb, PeakQ);
            Complex z1 = Complex.Exp(new Complex(0, -2 * Math.PI * PeakHz / Rate));
            Complex z2 = z1 * z1;
            double mag17 = Complex.Abs((pk.B0 + pk.B1 * z1 + pk.B2 * z2) / (1 + pk.A1 * z1 + pk.A2 * z2));
            double dc17 = hp.B0 + hp.B1 + hp.B2;
            double ny17 = lp.B0 - lp.B1 + lp.B2;
            bool ok17 = Math.Abs(dc17) <= 1e-12 && Math.Abs(ny17) <= 1e-12 && Math.Abs(mag17 - Math.Pow(10, PeakGainDb / 20)) <= 1e-9;
            Expect("17", "filter: high-pass DC, low-pass Nyquist, peak gain", ok17 ? "within" : "dc=" + R(dc17) + " nyquist=" + R(ny17) + " peak=" + R(mag17), "within");

            var i0 = new double[2048];
            var i100 = new double[2048];
            i0[0] = 1;
            i100[100] = 1;
            double[] y0 = Filter(i0);
            double[] y100 = Filter(i100);
            bool shifted = true;
            for (int k = 0; k < y100.Length; k++)
                shifted &= y100[k] == (k < 100 ? 0 : y0[k - 100]);
            Rng g18 = Toy("noise");
            var a18 = new double[4800];
            var b18 = new double[4800];
            var s18 = new double[4800];
            for (int k = 0; k < s18.Length; k++)
            {
                a18[k] = g18.Gaussian();
                b18[k] = g18.Gaussian();
                s18[k] = a18[k] + b18[k];
            }
            double[] fa = Filter(a18);
            double[] fb = Filter(b18);
            double[] fs = Filter(s18);
            double e18 = 0;
            for (int k = 0; k < fs.Length; k++)
                e18 = Math.Max(e18, Math.Abs(fs[k] - (fa[k] + fb[k])));
            Expect("18", "filter: shift exact, linear within 1e-12", shifted && e18 <= 1e-12 ? "within" : "shifted=" + shifted + " error=" + R(e18), "within");

            var s19 = new double[4800];
            for (int k = 0; k < s19.Length; k++)
                s19[k] = 0.5 * Math.Sin(2 * Math.PI * 1000 * k / Rate);
            double[] n19 = Pink(Toy("noise"), s19.Length);
            var seg19 = new (long Start, long End)[] { (0, s19.Length) };
            var bad19 = new List<string>();
            foreach (Step step in Grid)
            {
                var mix = Mix(s19, n19, step.SnrDb, seg19);
                double ss = 0;
                double an = 0;
                for (int k = 0; k < s19.Length; k++)
                {
                    ss += s19[k] * s19[k];
                    an += mix.A * n19[k] * (mix.A * n19[k]);
                }
                double mixed = 10 * Math.Log10(ss / an);
                short[] y = Level(mix.M, 0.1, out double g);
                double realized = Realized(y, n19, g * mix.A, seg19);
                if (Math.Abs(mixed - step.SnrDb) > 1e-9 || Math.Abs(realized - step.SnrDb) > 0.5)
                    bad19.Add(step.Id + " mix=" + R(mixed) + " realized=" + R(realized));
            }
            Expect("19", "mix: SNR within 1e-9, realized within 0.5 dB", bad19.Count == 0 ? "within" : string.Join(" ", bad19), "within");

            short[] y20 = Level(Mix(s19, n19, 20, seg19).M, 0.1, out _);
            double peak20 = Peak(y20);
            Rng l20 = Toy("level");
            int below = 0;
            bool in20 = true;
            for (int i = 0; i < 100000; i++)
            {
                double level = DrawLevel(l20);
                in20 &= level >= LevelMin && level <= LevelMax;
                if (level < LevelMin * Math.Sqrt(10))
                    below++;
            }
            double share = below / 100000.0;
            Expect("20", "level: peak at 0.1, 10^5 draws log-uniform", Math.Abs(peak20 - 0.1) <= 1 / 32768.0 && in20 && Math.Abs(share - 0.5) <= 0.01 ? "within" : "peak=" + R(peak20) + " in=" + in20 + " share=" + R(share), "within");

            var y21 = new short[y20.Length + 4];
            Array.Copy(y20, y21, y20.Length);
            y21[^4] = short.MinValue;
            y21[^3] = -1;
            y21[^2] = 1;
            y21[^1] = short.MaxValue;
            string tmp = Path.GetTempFileName();
            short[] back;
            try
            {
                WriteWav(tmp, y21);
                back = Bench.ReadPcm(tmp);
            }
            finally
            {
                File.Delete(tmp);
            }
            bool same21 = back.Length == y21.Length;
            for (int i = 0; same21 && i < y21.Length; i++)
                same21 = back[i] == y21[i];
            Expect("21", "WAV written, ReadPcm from a temp file", same21 ? "same" : "differs", "same");

            Expect(
                "22",
                "Outside at step 20: SNR, peak, RT60, all inside",
                string.Join(
                    " | ",
                    Outside("toy", 20, 19.4, 0.2, 0.3),
                    Outside("toy", 20, 20, 0.41, 0.3),
                    Outside("toy", 20, 20, 0.2, 0.51),
                    Outside("toy", 20, 20.4, 0.4, 0.5) ?? "null"
                ),
                "condition: clip \"toy\": realized SNR 19.400 dB at step 20: outside F2 | condition: clip \"toy\": peak 0.410: outside F2 | condition: clip \"toy\": RT60 0.510 s: outside F2 | null"
            );

            var ids = new List<string>();
            foreach (Step s in Grid)
                ids.Add(s.Id);
            string unknown;
            try
            {
                unknown = "found " + Steps("s0.8-snr20").Count;
            }
            catch (JoinedInput.Stop stop)
            {
                unknown = stop.Lines[0];
            }
            Expect(
                "23",
                "grid: ids, grid, one id, an unknown id",
                string.Join(" ", ids) + " | " + Steps("grid").Count + " | " + Steps("s1.0-snr10").Count + " | " + unknown,
                "s0.9-snr20 s0.9-snr15 s0.9-snr10 s0.9-snr5 s1.0-snr20 s1.0-snr15 s1.0-snr10 s1.0-snr5 s1.1-snr20 s1.1-snr15 s1.1-snr10 s1.1-snr5 | 12 | 1 | condition: unknown condition \"s0.8-snr20\": valid: grid, s0.9-snr20, s0.9-snr15, s0.9-snr10, s0.9-snr5, s1.0-snr20, s1.0-snr15, s1.0-snr10, s1.0-snr5, s1.1-snr20, s1.1-snr15, s1.1-snr10, s1.1-snr5"
            );

            Console.WriteLine();
            Console.WriteLine($"{checks - failures}/{checks} condition checks verified against known-value toys.");
            return failures == 0 ? 0 : 1;
        }
    }
}
