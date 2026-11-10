// ============================================================================
// Purpose:  The bench's condition pipeline: keyed streams, speed resampling, the synthetic room response and its RT60, pink noise, the mic filter, SNR and level, scaled truth and the grid; the condition and condition-check verbs; the negative set's clips and the noise-only twins; the add-on reader, MUSAN and SLR28 selection, the game-audio conditions and negatives
// Layer:    Planning.AbRig
// Owns:     BenchConditions (internal static class), BenchConditions.Rng (internal sealed class), BenchConditions.Step (internal sealed class), BenchConditions.AddOn, BenchConditions.AddOns (internal sealed classes)
// Depends:  Bench, JoinedInput, Joined, JoinedScorer, VoxrSlotDefinition, VoxrCommandDefinition
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
using VoXR.Commands;
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
    double Rms,
    string Response,
    string Interferer,
    long Offset
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
            internal bool Game;
        }

        internal sealed class AddOn
        {
            internal string Path;
            internal string Name;
            internal int Rate;
            internal int Channels;
            internal bool Float;
            internal long Data;
            internal long Frames;
        }

        internal sealed class AddOns
        {
            internal string Root;
            internal List<AddOn> Musan;
            internal List<AddOn> Slr28;
            internal List<AddOn> Game;
            internal Dictionary<string, (double[] H, double Rt60)> Responses;
        }

        const string Accepted = "RIFF WAV, PCM 16-bit or float 32-bit, 1 or 2 channels, any rate";
        internal const string NotSupplied = "not supplied";
        internal const string OutOfSet = "negative-out-of-set";
        internal const string NearMiss = "negative-near-miss";
        internal const string NoiseOnly = "noise-only";
        internal const string GameAudioOnly = "game-audio-only";
        internal const string Silence = "silence";
        const string Smallroom = "slr28/RIRS_NOISES/simulated_rirs/smallroom";
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
        static readonly int[] SirSteps = { 20, 10, 5, 0 };
        static readonly List<Step> GameGrid = MakeGameGrid();
        static readonly byte[] SubFormatTail =
        {
            0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71,
        };

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

        static List<Step> MakeGameGrid()
        {
            var grid = new List<Step>();
            foreach (int sir in SirSteps)
                grid.Add(
                    new Step
                    {
                        Id = "game-sir" + sir,
                        Speed = 10,
                        SnrDb = sir,
                        Game = true,
                    }
                );
            return grid;
        }

        internal static ulong ParseSeed(string seedText)
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
            return seed;
        }

        internal static List<Step> Steps(string name, bool game)
        {
            if (name == "grid")
            {
                var grid = new List<Step>(Grid);
                if (game)
                    grid.AddRange(GameGrid);
                return grid;
            }
            foreach (Step s in Grid)
                if (s.Id == name)
                    return new List<Step> { s };
            foreach (Step s in GameGrid)
                if (s.Id == name)
                {
                    if (!game)
                        throw new JoinedInput.Stop(
                            2,
                            "condition: \""
                                + name
                                + "\" needs game audio (game-audio/ under the add-on root): not supplied"
                        );
                    return new List<Step> { s };
                }
            var ids = new List<string>();
            foreach (Step s in Grid)
                ids.Add(s.Id);
            if (game)
                foreach (Step s in GameGrid)
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

        static long Scale(long x, int f) => Scale(x, f, 10);

        static long Scale(long x, int num, int den) => (2L * den * x + num) / (2L * num);

        static long Need(int len, int rate) => ((long)len * rate + 47999) / 48000;

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

        static double[] Resample(double[] input, int f) => Resample(input, f, 10);

        static double[] Resample(double[] input, int num, int den)
        {
            if (num == den)
                return (double[])input.Clone();
            var output = new double[Scale(input.Length, num, den)];
            double c = Math.Min(1.0, (double)den / num);
            double w = 32 / c;
            for (long k = 0; k < output.Length; k++)
            {
                double x = (double)((long)k * num) / den;
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
                if (Eligible(rt60))
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

        static bool Eligible(double rt60) => rt60 >= Rt60Min && rt60 <= Rt60Max;

        internal static double[] Trim(double[] h)
        {
            int p = 0;
            for (int k = 1; k < h.Length; k++)
                if (Math.Abs(h[k]) > Math.Abs(h[p]))
                    p = k;
            var t = new double[h.Length - p];
            for (int k = 0; k < t.Length; k++)
                t[k] = h[p + k] / h[p];
            return t;
        }

        static (double[] H, double T, int Draws, double Rt60, string Response) RoomFor(
            ulong seed,
            string clip,
            Step step,
            AddOns a
        )
        {
            if (a.Slr28 == null)
            {
                var room = Room(new Rng(seed, clip, step.Id, "room"), clip);
                return (room.H, room.T, room.Draws, room.Rt60, null);
            }
            var rng = new Rng(seed, clip, step.Id, "response");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int draw = 1; ; draw++)
            {
                AddOn file = a.Slr28[(int)Math.Floor(rng.Uniform() * a.Slr28.Count)];
                if (!a.Responses.TryGetValue(file.Name, out var measured))
                {
                    double[] h = Trim(Resample(Frames(file, 0, file.Frames), file.Rate, Rate));
                    measured = (h, Rt60(h));
                    a.Responses.Add(file.Name, measured);
                }
                if (Eligible(measured.Rt60))
                    return (measured.H, 0, draw, measured.Rt60, file.Name);
                seen.Add(file.Name);
                if (seen.Count == a.Slr28.Count)
                    throw new JoinedInput.Stop(
                        2,
                        "condition: "
                            + a.Root
                            + "/"
                            + Smallroom
                            + ": no response measuring 0.2–0.5 s ("
                            + a.Slr28.Count
                            + " files); accepted: "
                            + Accepted
                    );
            }
        }

        static byte[] Bytes(FileStream f, long at, int n)
        {
            f.Position = at;
            var b = new byte[n];
            int got = f.ReadAtLeast(b, n, false);
            return got == n ? b : b[..got];
        }

        static string Chunks(FileStream f, AddOn a)
        {
            byte[] riff = Bytes(f, 0, 12);
            if (
                riff.Length < 12
                || Encoding.ASCII.GetString(riff, 0, 4) != "RIFF"
                || Encoding.ASCII.GetString(riff, 8, 4) != "WAVE"
            )
                return "not a RIFF WAVE file";
            int align = 0;
            for (long at = 12; at + 8 <= f.Length; )
            {
                byte[] h = Bytes(f, at, 8);
                string id = Encoding.ASCII.GetString(h, 0, 4);
                long size = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(4));
                if (id == "fmt " && size >= 16 && at + 8 + size <= f.Length)
                {
                    byte[] b = Bytes(f, at + 8, (int)Math.Min(size, 40));
                    int tag = BinaryPrimitives.ReadUInt16LittleEndian(b);
                    int channels = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(2));
                    uint rate = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4));
                    int blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(12));
                    int bits = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(14));
                    if (
                        tag == 0xFFFE
                        && size >= 40
                        && (b[24] == 1 || b[24] == 3)
                        && b[25] == 0
                        && b.AsSpan(26, 14).SequenceEqual(SubFormatTail)
                    )
                        tag = b[24];
                    if (tag != 1 && tag != 3)
                        return "format tag 0x" + tag.ToString("x4", CultureInfo.InvariantCulture);
                    if (tag == 1 && bits != 16)
                        return "PCM " + bits + "-bit";
                    if (tag == 3 && bits != 32)
                        return "float " + bits + "-bit";
                    if (channels != 1 && channels != 2)
                        return channels + " channels";
                    if (rate == 0 || rate > int.MaxValue)
                        return "rate " + rate;
                    if (blockAlign != channels * bits / 8)
                        return "block align " + blockAlign;
                    a.Rate = (int)rate;
                    a.Channels = channels;
                    a.Float = tag == 3;
                    align = blockAlign;
                }
                else if (id == "data")
                {
                    if (align == 0)
                        return "no fmt chunk before the data chunk";
                    if (at + 8 + size > f.Length)
                        return "data chunk past the end of the file";
                    a.Data = at + 8;
                    a.Frames = size / align;
                    return a.Frames == 0 ? "no samples" : null;
                }
                at += 8 + size + (size & 1);
            }
            return "no data chunk";
        }

        static AddOn Header(string root, string name)
        {
            var a = new AddOn { Path = root + "/" + name, Name = name };
            string reason;
            using (FileStream f = File.OpenRead(a.Path))
                reason = Chunks(f, a);
            if (reason != null)
                throw new JoinedInput.Stop(
                    2,
                    "condition: " + a.Path + ": cannot be read: " + reason + "; accepted: " + Accepted
                );
            return a;
        }

        static double[] Frames(AddOn a, long start, long count)
        {
            int width = a.Float ? 4 : 2;
            int align = width * a.Channels;
            byte[] b;
            using (FileStream f = File.OpenRead(a.Path))
                b = Bytes(f, a.Data + start * align, (int)(count * align));
            double Value(long at) =>
                a.Float
                    ? BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan((int)at))
                    : BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan((int)at)) / 32768.0;
            var x = new double[count];
            for (long i = 0; i < count; i++)
            {
                long at = i * align;
                x[i] = a.Channels == 1 ? Value(at) : 0.5 * (Value(at) + Value(at + width));
            }
            return x;
        }

        static List<AddOn> Folder(string root, string top, string sub, bool wavOnly)
        {
            if (!Directory.Exists(root + "/" + top))
                return null;
            string dir = root + "/" + sub;
            var names = new List<string>();
            if (Directory.Exists(dir))
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    if (!wavOnly || file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        names.Add(sub + "/" + Path.GetRelativePath(dir, file).Replace('\\', '/'));
            if (names.Count == 0)
                throw new JoinedInput.Stop(
                    2,
                    "condition: " + dir + ": no file to read; accepted: " + Accepted
                );
            names.Sort(StringComparer.Ordinal);
            var files = new List<AddOn>();
            foreach (string name in names)
                files.Add(Header(root, name));
            return files;
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

        static string Outside(string clip, Step step, double snr, double peak, double rt60)
        {
            string at = "condition: clip \"" + clip + "\": ";
            if (!(Math.Abs(snr - step.SnrDb) <= SnrTolerance))
                return at
                    + "realized "
                    + (step.Game ? "SIR " : "SNR ")
                    + Shown(snr)
                    + " dB at step "
                    + step.Id
                    + ": outside F2";
            if (!(peak >= LevelMin && peak <= LevelMax))
                return at + "peak " + Shown(peak) + ": outside F2";
            if (!Eligible(rt60))
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

        static double[] ToDouble(short[] pcm)
        {
            var s = new double[pcm.Length];
            for (int k = 0; k < pcm.Length; k++)
                s[k] = pcm[k] / 32768.0;
            return s;
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

        static (double[] S1, (long Start, long End)[] Speech) Speeded(
            JoinedInput.ManifestCase c,
            Step step,
            double[] source
        )
        {
            double[] s1 = Resample(source, step.Speed);
            (long Start, long End)[] speech = ScaleSpeech(c.Speech, step.Speed);
            SpeedTruth(ClipName(c, step), s1, c, speech, step.Speed);
            return (s1, speech);
        }

        static double[] Window(
            Rng pick,
            Rng at,
            List<AddOn> files,
            int len,
            (long Start, long End)[] spans,
            string clip,
            string folder,
            out string name,
            out long offset
        )
        {
            for (int draw = 1; draw <= MaxDraws; draw++)
            {
                AddOn file = files[(int)Math.Floor(pick.Uniform() * files.Count)];
                long m = Need(len, file.Rate);
                offset = (long)Math.Floor(
                    at.Uniform() * (file.Frames >= m ? file.Frames - m + 1 : file.Frames)
                );
                var x = new double[m];
                for (long i = 0; i < m; )
                {
                    long start = (offset + i) % file.Frames;
                    long count = Math.Min(m - i, file.Frames - start);
                    Array.Copy(Frames(file, start, count), 0, x, i, count);
                    i += count;
                }
                var cut = new double[len];
                Array.Copy(Resample(x, file.Rate, Rate), cut, len);
                double[] n = Filter(cut);
                double e = 0;
                if (spans == null)
                    foreach (double v in n)
                        e += v * v;
                else
                    foreach (var seg in spans)
                        for (long i = seg.Start; i < seg.End; i++)
                            e += n[i] * n[i];
                name = file.Name;
                if (double.IsFinite(len / e))
                    return n;
            }
            throw new JoinedInput.Stop(
                2,
                "condition: clip \""
                    + clip
                    + "\": no window with signal under "
                    + folder
                    + " in 100 draws"
            );
        }

        static double[] Interferer(
            ulong seed,
            string clip,
            Step step,
            int len,
            (long Start, long End)[] spans,
            AddOns a,
            out string file,
            out long offset
        )
        {
            if (step.Game)
                return Window(
                    new Rng(seed, clip, step.Id, "game-file"),
                    new Rng(seed, clip, step.Id, "game-offset"),
                    a.Game,
                    len,
                    spans,
                    clip,
                    a.Root + "/game-audio",
                    out file,
                    out offset
                );
            if (a.Musan != null)
                return Window(
                    new Rng(seed, clip, step.Id, "musan-file"),
                    new Rng(seed, clip, step.Id, "musan-offset"),
                    a.Musan,
                    len,
                    spans,
                    clip,
                    a.Root + "/musan/noise",
                    out file,
                    out offset
                );
            file = null;
            offset = 0;
            return Filter(Pink(new Rng(seed, clip, step.Id, "noise"), len));
        }

        static Clip SpeechClip(
            ulong seed,
            JoinedInput.ManifestCase c,
            Step step,
            (double[] S1, (long Start, long End)[] Speech) speed,
            AddOns a
        )
        {
            string clip = ClipName(c, step);
            double[] s1 = speed.S1;
            (long Start, long End)[] speech = speed.Speech;
            var room = RoomFor(seed, clip, step, a);
            double[] s = Filter(Convolve(s1, room.H));
            double[] n = Interferer(seed, clip, step, s1.Length, speech, a, out string file, out long offset);
            var mix = Mix(s, n, step.SnrDb, speech);
            double level = DrawLevel(new Rng(seed, clip, step.Id, "level"));
            short[] y = Level(mix.M, level, out double g);
            double ga = g * mix.A;
            double snr = Realized(y, n, ga, speech);
            double peak = Peak(y);
            string outside = Outside(clip, step, snr, peak, room.Rt60);
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
                0,
                room.Response,
                file,
                offset
            );
        }

        static Clip FreeClip(
            ulong seed,
            JoinedInput.ManifestCase c,
            Step step,
            (long Start, long End)[] scaled,
            List<Clip> speech,
            AddOns a
        )
        {
            string clip = ClipName(c, step);
            Clip partner = speech[
                (int)Math.Floor(new Rng(seed, clip, step.Id, "partner").Uniform() * speech.Count)
            ];
            return Noise(seed, clip, c.Name, step, scaled, partner, a);
        }

        static Clip Noise(
            ulong seed,
            string clip,
            string source,
            Step step,
            (long Start, long End)[] speech,
            Clip partner,
            AddOns a
        )
        {
            double[] n = Interferer(
                seed,
                clip,
                step,
                partner.Y.Length,
                null,
                a,
                out string file,
                out long offset
            );
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
                source,
                step.Id,
                y,
                speech,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                partner.File,
                partner.NoiseRms,
                null,
                file,
                offset
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
            string seedText,
            string negativesPath,
            string audioRoot
        )
        {
            if (
                Array.IndexOf(Path.GetFullPath(outDir).Split('/', '\\'), ".scratch") < 0
                || Array.IndexOf(outDir.Split('/', '\\'), "..") >= 0
            )
                throw new JoinedInput.Stop(
                    2,
                    "condition: --out must lie under .scratch/ (the conditioned audio is written there): "
                        + outDir
                );
            ulong seed = ParseSeed(seedText);
            List<Step> steps = Steps(name, Directory.Exists(audioRoot + "/game-audio"));
            Bench.Prelude prelude = Bench.FastPrelude(dumpPath, null, manifestPath, pinPath, root);
            JsonElement harness = JoinedInput.ParseObject(
                File.ReadAllText(harnessPin, JoinedInput.StrictUtf8),
                harnessPin
            );
            JoinedInput.Manifest negatives = JoinedInput.ReadManifest(negativesPath);
            Bench.TruthCheck(negatives, root);
            var a = new AddOns
            {
                Root = audioRoot,
                Musan = Folder(audioRoot, "musan", "musan/noise", true),
                Slr28 = Folder(audioRoot, "slr28", Smallroom, true),
                Game = Folder(audioRoot, "game-audio", "game-audio", false),
                Responses = new Dictionary<string, (double[] H, double Rt60)>(StringComparer.Ordinal),
            };
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
                sources[i] = ToDouble(Bench.ReadPcm(Path.Combine(root, cases[i].File)));
            var speeds = new Dictionary<
                (JoinedInput.ManifestCase C, int Speed),
                (double[] S1, (long Start, long End)[] Speech)
            >();
            (double[] S1, (long Start, long End)[] Speech) Speed(
                JoinedInput.ManifestCase c,
                Step step,
                double[] source
            )
            {
                if (!speeds.TryGetValue((c, step.Speed), out var s))
                {
                    s = Speeded(c, step, source);
                    speeds.Add((c, step.Speed), s);
                }
                return s;
            }
            var clips = new Clip[cases.Count, steps.Count];
            for (int j = 0; j < steps.Count; j++)
            {
                var speech = new List<Clip>();
                for (int i = 0; i < cases.Count; i++)
                    if (cases[i].Speech.Length > 0)
                    {
                        clips[i, j] = SpeechClip(seed, cases[i], steps[j], Speed(cases[i], steps[j], sources[i]), a);
                        speech.Add(clips[i, j]);
                    }
                for (int i = 0; i < cases.Count; i++)
                    if (cases[i].Speech.Length == 0)
                        clips[i, j] = FreeClip(seed, cases[i], steps[j], Speed(cases[i], steps[j], sources[i]).Speech, speech, a);
            }

            var written = new List<(Clip K, JoinedInput.ManifestCase C)>();
            for (int i = 0; i < cases.Count; i++)
                for (int j = 0; j < steps.Count; j++)
                    written.Add((clips[i, j], cases[i]));
            foreach (JoinedInput.ManifestCase n in negatives.Cases)
            {
                short[] pcm = Bench.ReadPcm(Path.Combine(root, n.File));
                double[] source = ToDouble(pcm);
                Clip clean = (n.Name, n.Name, "clean", pcm, n.Speech, 0, 0, 0, 0, 0, 0, 0, null, 0, null, null, 0);
                written.Add((clean, n));
                foreach (Step step in steps)
                    written.Add((SpeechClip(seed, n, step, Speed(n, step, source), a), n));
            }
            var noiseOnly = new JoinedInput.ManifestCase
            {
                Category = NoiseOnly,
                ExpectedIntent = "",
                ExpectedSlots = [],
            };
            var gameOnly = new JoinedInput.ManifestCase
            {
                Category = GameAudioOnly,
                ExpectedIntent = "",
                ExpectedSlots = [],
            };
            int twins = 0;
            for (int i = 0; i < cases.Count; i++)
            {
                if (cases[i].Speech.Length == 0)
                    continue;
                twins++;
                string stem = Path.GetFileNameWithoutExtension(cases[i].Name);
                for (int j = 0; j < steps.Count; j++)
                    written.Add(
                        (
                            Noise(
                                seed,
                                stem
                                    + (steps[j].Game ? "__game__" : "__noise__")
                                    + steps[j].Id
                                    + ".wav",
                                cases[i].Name,
                                steps[j],
                                Array.Empty<(long Start, long End)>(),
                                clips[i, j],
                                a
                            ),
                            steps[j].Game ? gameOnly : noiseOnly
                        )
                    );
            }
            int game = 0;
            foreach (Step step in steps)
                if (step.Game)
                    game++;

            string cond = outDir + "/cond";
            if (Path.Exists(cond))
                throw new JoinedInput.Stop(2, "condition: " + cond + " exists: empty it first");
            Directory.CreateDirectory(cond);
            foreach (var (k, _) in written)
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
                        foreach (var (k, _) in written)
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
                    foreach (var (k, c) in written)
                    {
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
                    w.WriteString(
                        "noise",
                        a.Musan != null
                            ? "MUSAN (musan/noise, " + a.Musan.Count + " files)"
                            : "generated (pink Gaussian)"
                    );
                    w.WriteString(
                        "room",
                        a.Slr28 != null
                            ? "SLR28 ("
                                + Smallroom
                                + ", "
                                + a.Slr28.Count
                                + " files, RT60 0.2–0.5 s, direct path at lag 0)"
                            : "synthetic (exponential decay)"
                    );
                    w.WriteString(
                        "gameAudio",
                        a.Game != null
                            ? "supplied (game-audio, " + a.Game.Count + " files)"
                            : NotSupplied
                    );
                    w.WriteNumber("highPassHz", HighPassHz);
                    w.WriteNumber("peakHz", PeakHz);
                    w.WriteNumber("peakGainDb", PeakGainDb);
                    w.WriteNumber("peakQ", PeakQ);
                    w.WriteNumber("lowPassHz", LowPassHz);
                    if (a.Slr28 == null)
                        w.WriteNumber("drrDb", DrrDb);
                    w.WriteStartArray("levelRange");
                    w.WriteNumberValue(LevelMin);
                    w.WriteNumberValue(LevelMax);
                    w.WriteEndArray();
                    w.WriteString("levelDistribution", "log-uniform");
                    w.WriteStartArray("clips");
                    foreach (var (k, _) in written)
                    {
                        w.WriteStartObject();
                        w.WriteString("file", k.File);
                        w.WriteString("source", k.Source);
                        w.WriteString("condition", k.Condition);
                        if (k.Partner != null)
                        {
                            w.WriteString("partner", k.Partner);
                            w.WriteNumber("rms", k.Rms);
                        }
                        else if (k.Condition != "clean")
                        {
                            if (k.Response != null)
                                w.WriteString("response", k.Response);
                            else
                                w.WriteNumber("drawnRt60", k.T);
                            w.WriteNumber("draws", k.Draws);
                            w.WriteNumber("level", k.Level);
                            w.WriteNumber("snr", k.Snr);
                            w.WriteNumber("peak", k.Peak);
                            w.WriteNumber("rt60", k.Rt60);
                        }
                        if (k.Interferer != null)
                        {
                            w.WriteString("interferer", k.Interferer);
                            w.WriteNumber("offset", k.Offset);
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
                    + written.Count
                    + " clips: "
                    + cases.Count
                    + " cases x "
                    + steps.Count
                    + " conditions, "
                    + negatives.Cases.Count
                    + " negatives clean and x "
                    + steps.Count
                    + ", "
                    + twins
                    + " noise-only twins x "
                    + (steps.Count - game)
                    + (game > 0 ? ", " + twins + " game-audio-only x " + game : "")
                    + ", seed "
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

        static byte[] Pcm16(params short[] v)
        {
            var b = new byte[2 * v.Length];
            for (int i = 0; i < v.Length; i++)
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(2 * i), v[i]);
            return b;
        }

        static byte[] Float32(params float[] v)
        {
            var b = new byte[4 * v.Length];
            for (int i = 0; i < v.Length; i++)
                BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(4 * i), v[i]);
            return b;
        }

        static byte[] ToyWav(
            string riff,
            int tag,
            int channels,
            int rate,
            int bits,
            byte[] data,
            int dataSize
        )
        {
            int fmt = tag == 0xFFFE ? 40 : 16;
            int align = channels * bits / 8;
            var b = new byte[28 + fmt + data.Length];
            Encoding.ASCII.GetBytes(riff).CopyTo(b, 0);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), b.Length - 8);
            Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(b, 8);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16), fmt);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(20), (ushort)tag);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(22), (short)channels);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24), rate);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(28), rate * align);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(32), (short)align);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(34), (short)bits);
            if (fmt == 40)
            {
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(36), 22);
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(38), (short)bits);
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(44), 3);
                SubFormatTail.CopyTo(b, 46);
            }
            Encoding.ASCII.GetBytes("data").CopyTo(b, 20 + fmt);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24 + fmt), dataSize);
            data.CopyTo(b, 28 + fmt);
            return b;
        }

        internal static int Check(
            string negatives,
            string pin,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
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

            Step s22 = Steps("s1.0-snr20", false)[0];
            Expect(
                "22",
                "Outside at step 20: SNR, peak, RT60, all inside",
                string.Join(
                    " | ",
                    Outside("toy", s22, 19.4, 0.2, 0.3),
                    Outside("toy", s22, 20, 0.41, 0.3),
                    Outside("toy", s22, 20, 0.2, 0.51),
                    Outside("toy", s22, 20.4, 0.4, 0.5) ?? "null"
                ),
                "condition: clip \"toy\": realized SNR 19.400 dB at step s1.0-snr20: outside F2 | condition: clip \"toy\": peak 0.410: outside F2 | condition: clip \"toy\": RT60 0.510 s: outside F2 | null"
            );

            var ids = new List<string>();
            foreach (Step s in Grid)
                ids.Add(s.Id);
            string unknown;
            try
            {
                unknown = "found " + Steps("s0.8-snr20", false).Count;
            }
            catch (JoinedInput.Stop stop)
            {
                unknown = stop.Lines[0];
            }
            Expect(
                "23",
                "grid: ids, grid, one id, an unknown id",
                string.Join(" ", ids) + " | " + Steps("grid", false).Count + " | " + Steps("s1.0-snr10", false).Count + " | " + unknown,
                "s0.9-snr20 s0.9-snr15 s0.9-snr10 s0.9-snr5 s1.0-snr20 s1.0-snr15 s1.0-snr10 s1.0-snr5 s1.1-snr20 s1.1-snr15 s1.1-snr10 s1.1-snr5 | 12 | 1 | condition: unknown condition \"s0.8-snr20\": valid: grid, s0.9-snr20, s0.9-snr15, s0.9-snr10, s0.9-snr5, s1.0-snr20, s1.0-snr15, s1.0-snr10, s1.0-snr5, s1.1-snr20, s1.1-snr15, s1.1-snr10, s1.1-snr5"
            );

            string dir = Directory.CreateTempSubdirectory("abrig-").FullName;
            try
            {
                string Shape(AddOn a) =>
                    a.Rate + " " + a.Channels + " " + a.Float + " " + a.Frames + " | " + string.Join(" ", Array.ConvertAll(Frames(a, 0, a.Frames), R));
                bool Same(double[] p, double[] q)
                {
                    if (p.Length != q.Length)
                        return false;
                    for (int i = 0; i < p.Length; i++)
                        if (p[i] != q[i])
                            return false;
                    return true;
                }

                File.WriteAllBytes(dir + "/t42.wav", ToyWav("RIFF", 1, 1, 16000, 16, Pcm16(short.MinValue, -1, 1, short.MaxValue), 8));
                Expect("42", "reader: PCM16 mono 16 kHz", Shape(Header(dir, "t42.wav")), "16000 1 False 4 | " + string.Join(" ", R(-32768 / 32768.0), R(-1 / 32768.0), R(1 / 32768.0), R(32767 / 32768.0)));

                File.WriteAllBytes(dir + "/t43.wav", ToyWav("RIFF", 3, 2, 44100, 32, Float32(0.25f, 0f, 1f, 1f), 16));
                Expect("43", "reader: float stereo, averaged", Shape(Header(dir, "t43.wav")), "44100 2 True 2 | 0.125 1");

                File.WriteAllBytes(dir + "/t44.wav", ToyWav("RIFF", 0xFFFE, 2, 44100, 32, Float32(0.25f, 0f, 1f, 1f), 16));
                Expect("44", "reader: EXTENSIBLE float stereo, averaged", Shape(Header(dir, "t44.wav")), "44100 2 True 2 | 0.125 1");

                const string tail45 = "; accepted: RIFF WAV, PCM 16-bit or float 32-bit, 1 or 2 channels, any rate";
                var cases45 = new (string Name, byte[] Wav)[]
                {
                    ("t45a.wav", ToyWav("RIFF", 1, 1, 16000, 8, new byte[4], 4)),
                    ("t45b.wav", ToyWav("RIFF", 1, 3, 16000, 16, new byte[12], 12)),
                    ("t45c.wav", ToyWav("RIFF", 1, 1, 16000, 16, new byte[4], 8)),
                    ("t45d.wav", ToyWav("RIFX", 1, 1, 16000, 16, new byte[4], 4)),
                    ("t45e.wav", ToyWav("RIFF", 3, 1, 16000, 64, new byte[16], 16)),
                };
                var reasons45 = new List<string>();
                foreach (var (name45, wav45) in cases45)
                {
                    File.WriteAllBytes(dir + "/" + name45, wav45);
                    string at45 = "condition: " + dir + "/" + name45 + ": cannot be read: ";
                    try
                    {
                        Header(dir, name45);
                        reasons45.Add("read");
                    }
                    catch (JoinedInput.Stop stop)
                    {
                        reasons45.Add(stop.Lines[0].StartsWith(at45, StringComparison.Ordinal) ? stop.Lines[0][at45.Length..] : stop.Lines[0]);
                    }
                }
                Expect(
                    "45",
                    "reader: 8-bit, 3 channels, short, RIFX, float 64",
                    string.Join(" | ", reasons45),
                    "PCM 8-bit" + tail45 + " | 3 channels" + tail45 + " | data chunk past the end of the file" + tail45 + " | not a RIFF WAVE file" + tail45 + " | float 64-bit" + tail45
                );

                double[] x46 = { 0.1, -0.2, 0.3 };
                double[] r46 = Resample(x46, Rate, Rate);
                bool copied46 = r46 != x46 && Same(r46, x46);
                Expect(
                    "46",
                    "Scale, Need: 16 and 44.1 kHz to 48, past int",
                    Scale(16000, 16000, Rate) + " " + Scale(44100, 44100, Rate) + " " + (copied46 ? "copied" : "differs") + " | " + Scale(882000, 44100, Rate) + " " + Need(960000, 44100) + " " + Need(960000, 16000),
                    "48000 48000 copied | 960000 882000 320000"
                );

                var bad47 = new List<string>();
                foreach (int rate47 in new[] { 16000, 44100 })
                {
                    var x47 = new double[rate47];
                    for (int k = 0; k < x47.Length; k++)
                        x47[k] = 0.5 * Math.Sin(2 * Math.PI * 1000 * k / rate47);
                    double[] r47 = Resample(x47, rate47, Rate);
                    double e47 = 0;
                    for (int k = 1000; k <= r47.Length - 1000; k++)
                        e47 = Math.Max(e47, Math.Abs(r47[k] - 0.5 * Math.Sin(2 * Math.PI * 1000 * k / Rate)));
                    if (e47 > 0.01)
                        bad47.Add(rate47 + ": " + R(e47));
                }
                var long47 = new double[882000];
                for (int k = 0; k < long47.Length; k++)
                    long47[k] = 0.5 * Math.Sin(2 * Math.PI * 1000 * k / 44100);
                double[] out47 = Resample(long47, 44100, Rate);
                if (out47.Length != 960000)
                    bad47.Add("length " + out47.Length);
                else
                {
                    double e47 = 0;
                    for (int k = 958000; k <= 958999; k++)
                        e47 = Math.Max(e47, Math.Abs(out47[k] - 0.5 * Math.Sin(2 * Math.PI * 1000 * k / Rate)));
                    if (e47 > 0.01)
                        bad47.Add("20 s: " + R(e47));
                }
                Expect("47", "resample: 1 kHz from 16, 44.1 kHz; 20 s", bad47.Count == 0 ? "within" : string.Join(" ", bad47), "within");

                var x48 = new double[16000];
                x48[40] = 0.9;
                double[] h48 = Trim(Resample(x48, 16000, Rate));
                double[] g48 = Trim(new double[] { 0, 0, -0.5, 0.2 });
                Expect("48", "SLR28 trim: from the peak, H[0] 1", R(h48[0]) + " " + h48.Length + " | " + string.Join(" ", Array.ConvertAll(g48, R)), "1 47880 | 1 -0.4");

                double[] Decay49(double t)
                {
                    var h = new double[(int)Math.Round(2 * t * Rate)];
                    for (int k = 0; k < h.Length; k++)
                        h[k] = Math.Pow(10, -3.0 * k / (Rate * t));
                    return h;
                }
                var d49 = new double[Rate];
                d49[0] = 1;
                double rt49 = Rt60(d49);
                Expect("49", "Eligible(Rt60): 0.3, 0.8, direct-only; NaN", Eligible(Rt60(Decay49(0.3))) + " " + Eligible(Rt60(Decay49(0.8))) + " " + Eligible(rt49) + " " + double.IsNaN(rt49), "True False False True");

                var ramp50 = new float[300];
                for (int i = 0; i < ramp50.Length; i++)
                    ramp50[i] = (float)((i - 150) / 150.0);
                Directory.CreateDirectory(dir + "/f");
                Directory.CreateDirectory(dir + "/z");
                File.WriteAllBytes(dir + "/f/a.wav", ToyWav("RIFF", 3, 1, Rate, 32, Float32(ramp50), 4 * ramp50.Length));
                File.WriteAllBytes(dir + "/f/b.wav", ToyWav("RIFF", 1, 1, Rate, 16, new byte[600], 600));
                File.WriteAllBytes(dir + "/z/b.wav", ToyWav("RIFF", 1, 1, Rate, 16, new byte[600], 600));
                List<AddOn> files50 = Folder(dir, "f", "f", true);
                bool exact50 = true;
                bool never50 = true;
                for (int i = 0; i < 20; i++)
                {
                    string clip50 = "toy" + i;
                    double[] w50 = Window(new Rng(7, clip50, "game-sir0", "game-file"), new Rng(7, clip50, "game-sir0", "game-offset"), files50, 1000, null, clip50, dir + "/f", out string name50, out long offset50);
                    never50 &= name50 == "f/a.wav";
                    var tiled50 = new double[1000];
                    for (int k = 0; k < tiled50.Length; k++)
                        tiled50[k] = ramp50[(offset50 + k) % ramp50.Length];
                    exact50 &= Same(w50, Filter(tiled50));
                }
                double[] once50 = Window(Toy("game-file"), Toy("game-offset"), files50, 1000, null, "toy", dir + "/f", out string name50a, out long offset50a);
                double[] twice50 = Window(Toy("game-file"), Toy("game-offset"), files50, 1000, null, "toy", dir + "/f", out string name50b, out long offset50b);
                bool repeat50 = Same(once50, twice50) && name50a == name50b && offset50a == offset50b;
                string stop50;
                try
                {
                    Window(Toy("game-file"), Toy("game-offset"), Folder(dir, "z", "z", true), 1000, null, "toy", dir + "/z", out _, out _);
                    stop50 = "returned";
                }
                catch (JoinedInput.Stop stop)
                {
                    stop50 = stop.Lines[0];
                }
                Expect(
                    "50",
                    "Window: tiled ramp, repeat, zero file, stop",
                    (exact50 ? "exact" : "differs") + " " + (repeat50 ? "same" : "differs") + " " + (never50 ? "never" : "returned") + " | " + stop50,
                    "exact same never | condition: clip \"toy\": no window with signal under " + dir + "/z in 100 draws"
                );

                Rng g51 = Toy("partner");
                var game51 = new float[2 * 8820];
                for (int i = 0; i < game51.Length; i++)
                    game51[i] = (float)(0.1 * g51.Gaussian());
                Directory.CreateDirectory(dir + "/g");
                File.WriteAllBytes(dir + "/g/x.wav", ToyWav("RIFF", 3, 2, 44100, 32, Float32(game51), 4 * game51.Length));
                var a51 = new AddOns { Root = dir, Game = Folder(dir, "g", "g", false) };
                var bad51 = new List<string>();
                foreach (Step step51 in GameGrid)
                {
                    double[] n51 = Interferer(7, "toy", step51, s19.Length, seg19, a51, out string file51, out _);
                    var mix51 = Mix(s19, n51, step51.SnrDb, seg19);
                    short[] y51 = Level(mix51.M, 0.1, out double gain51);
                    double realized51 = Realized(y51, n51, gain51 * mix51.A, seg19);
                    if (Math.Abs(realized51 - step51.SnrDb) > 0.5 || file51 != "g/x.wav")
                        bad51.Add(step51.Id + " realized=" + R(realized51) + " file=" + file51);
                }
                Expect("51", "SIR realized within 0.5 dB per game step", bad51.Count == 0 ? "within" : string.Join(" ", bad51), "within");
            }
            finally
            {
                Directory.Delete(dir, true);
            }

            var ids52 = new List<string>();
            foreach (Step s in Steps("grid", true))
                ids52.Add(s.Id);
            string stop52;
            try
            {
                stop52 = "found " + Steps("game-sir5", false).Count;
            }
            catch (JoinedInput.Stop stop)
            {
                stop52 = stop.Lines[0];
            }
            Expect(
                "52",
                "grid with game audio: 16 ids; a game id without",
                string.Join(" ", ids52) + " | " + ids52.Count + " | " + stop52,
                "s0.9-snr20 s0.9-snr15 s0.9-snr10 s0.9-snr5 s1.0-snr20 s1.0-snr15 s1.0-snr10 s1.0-snr5 s1.1-snr20 s1.1-snr15 s1.1-snr10 s1.1-snr5 game-sir20 game-sir10 game-sir5 game-sir0 | 16 | condition: \"game-sir5\" needs game audio (game-audio/ under the add-on root): not supplied"
            );

            var a53 = new AddOns { Root = "toy" };
            var x53 = new double[57600];
            for (int k = 2400; k < 7200; k++)
                x53[k] = 0.5 * Math.Sin(2 * Math.PI * 1000 * k / Rate);
            JoinedInput.ManifestCase Spoken53(string name) =>
                new JoinedInput.ManifestCase { Name = name, Phrase = "toy", Speech = new[] { (2400L, 7200L) } };
            JoinedInput.ManifestCase caseA = Spoken53("a.wav");
            JoinedInput.ManifestCase caseB = Spoken53("b.wav");
            Step s53 = Steps("s1.0-snr20", false)[0];
            Clip alone53 = SpeechClip(7, caseA, s53, Speeded(caseA, s53, x53), a53);

            var c53 = new JoinedInput.ManifestCase { Name = "c.wav", Speech = Array.Empty<(long Start, long End)>() };
            Clip twin53 = FreeClip(7, c53, s53, Speeded(c53, s53, new double[300]).Speech, new List<Clip> { alone53 }, a53);
            double[] n53 = Interferer(7, twin53.File, s53, alone53.Y.Length, null, a53, out _, out _);
            double k53 = alone53.NoiseRms / Rms(n53);
            var scaled53 = new double[n53.Length];
            bool same53 = twin53.Y.Length == n53.Length;
            for (int i = 0; i < n53.Length; i++)
            {
                scaled53[i] = k53 * n53[i];
                same53 &= i < twin53.Y.Length && twin53.Y[i] == Math.Round(scaled53[i] * 32768);
            }
            double e53 = Math.Abs(Rms(scaled53) - alone53.NoiseRms);
            Expect("53", "FreeClip twin: k n samples, RMS = partner's 1e-9", (same53 ? "same" : "differs") + " " + (e53 <= 1e-9 ? "within" : "error " + R(e53)) + " " + twin53.Partner, "same within a__s1.0-snr20.wav");

            SpeechClip(7, caseB, s53, Speeded(caseB, s53, x53), a53);
            Clip after54 = SpeechClip(7, caseA, s53, Speeded(caseA, s53, x53), a53);
            Clip listed54 = default;
            foreach (JoinedInput.ManifestCase c54 in new[] { caseA, caseB })
            {
                Clip k54 = SpeechClip(7, c54, s53, Speeded(c54, s53, x53), a53);
                if (c54 == caseA)
                    listed54 = k54;
            }
            Expect(
                "54",
                "SpeechClip A: alone, after B, with B in the list",
                (alone53.Y.AsSpan().SequenceEqual(after54.Y) ? "same" : "differs") + " " + (alone53.Y.AsSpan().SequenceEqual(listed54.Y) ? "same" : "differs"),
                "same same"
            );

            var parts55 = new List<string>();
            foreach (string id55 in new[] { "s0.9-snr20", "s1.1-snr20" })
            {
                Step s55 = Steps(id55, false)[0];
                Clip k55 = SpeechClip(7, caseA, s55, Speeded(caseA, s55, x53), a53);
                parts55.Add(id55 + ": " + k55.Speech[0].Start + "-" + k55.Speech[0].End);
            }
            Expect("55", "SpeechClip at 0.9, 1.1: Scale of each bound", string.Join(", ", parts55), "s0.9-snr20: 2667-8000, s1.1-snr20: 2182-6545");

            Expect("56", "Outside at a game step: SIR", Outside("toy", Steps("game-sir10", true)[0], 9.2, 0.2, 0.3), "condition: clip \"toy\": realized SIR 9.200 dB at step game-sir10: outside F2");

            if (negatives != null)
                PhraseList(negatives, pin, slots, commands);

            Console.WriteLine();
            Console.WriteLine($"{checks - failures}/{checks} condition checks verified against known-value toys.");
            return failures == 0 ? 0 : 1;
        }

        static void PhraseList(
            string negatives,
            string pin,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            JoinedInput.Manifest m = JoinedInput.ReadManifest(negatives);
            JoinedInput.Pin p = JoinedInput.ReadPin(pin);
            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in p.Grammar)
                if (entry != "[unk]")
                    foreach (string w in entry.Split(' '))
                        words.Add(w);

            int outOfSet = 0;
            int nearMiss = 0;
            int other = 0;
            int noCommand = 0;
            int lowercase = 0;
            foreach (JoinedInput.ManifestCase c in m.Cases)
            {
                if (c.Category == OutOfSet)
                    outOfSet++;
                else if (c.Category == NearMiss)
                    nearMiss++;
                else
                    other++;
                if (c.ExpectedIntent == "" && c.ExpectedSlots.Length == 0)
                    noCommand++;
                if (
                    c.Phrase != null
                    && c.Segments == null
                    && System.Text.RegularExpressions.Regex.IsMatch(c.Phrase, "^[a-z]+( [a-z]+)*$")
                )
                    lowercase++;
            }
            Expect(
                "24",
                "the list",
                m.Cases.Count + " cases: " + outOfSet + " negative-out-of-set, " + nearMiss + " negative-near-miss, " + other + " other; no-command " + noCommand + "; lowercase " + lowercase,
                "16 cases: 8 negative-out-of-set, 8 negative-near-miss, 0 other; no-command 16; lowercase 16"
            );

            JoinedScorer.Outcome control = OneFinal(
                new JoinedInput.ManifestCase
                {
                    Name = "control",
                    File = "control",
                    Phrase = "cease fire",
                    ExpectedIntent = "cease_fire",
                    ExpectedSlots = [],
                },
                p,
                slots,
                commands
            );
            Expect("25", "control: cease fire", control.Class + " fires=" + control.FireTimes.Length, "correct-fire fires=1");

            for (int i = 0; i < m.Cases.Count; i++)
            {
                JoinedInput.ManifestCase c = m.Cases[i];
                var shared = new List<string>();
                foreach (string w in c.Phrase.Split(' '))
                    if (words.Contains(w))
                        shared.Add(w);
                string g = shared.Count > 0 ? string.Join(" ", shared) : "none";
                string where = (26 + i).ToString(CultureInfo.InvariantCulture);
                string what = c.Name + " [" + g + "]";
                if (c.Category == OutOfSet)
                {
                    Expect(where, what, g, "none");
                    continue;
                }
                JoinedScorer.Outcome s = OneFinal(c, p, slots, commands);
                Expect(
                    where,
                    what,
                    "shares " + shared.Count + " | " + s.Class + " fires=" + s.FireTimes.Length + " pending=" + s.Pending.ToString(),
                    "shares 1 | correct-no-fire fires=0 pending=False"
                );
            }
        }

        static JoinedScorer.Outcome OneFinal(
            JoinedInput.ManifestCase c,
            JoinedInput.Pin p,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            string[] w = c.Phrase.Split(' ');
            var payload = new StringBuilder("{\"result\":[");
            for (int i = 0; i < w.Length; i++)
            {
                if (i > 0)
                    payload.Append(',');
                string at = i.ToString(CultureInfo.InvariantCulture);
                payload.Append("{\"conf\":1.0,\"end\":" + at + ".5,\"start\":" + at + ",\"word\":\"" + w[i] + "\"}");
            }
            payload.Append("],\"text\":\"" + c.Phrase + "\"}");
            var d = new JoinedInput.DumpCase
            {
                Name = c.Name,
                SampleRate = 48000,
                Samples = 48000L * w.Length,
            };
            d.Results.Add(
                new JoinedInput.DumpResult
                {
                    Seq = 0,
                    Final = true,
                    Consumed = 48000L * w.Length,
                    Payload = payload.ToString(),
                }
            );
            d.Finals.Add(c.Phrase);
            JoinedScorer.Outcome s = Joined.ScoreRecogniser(c, d, slots, commands);
            Joined.AppliedGrammarGuard(c.Name, s.AppliedGrammar, p);
            return s;
        }
    }
}
