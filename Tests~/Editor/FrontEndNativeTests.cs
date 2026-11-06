// ============================================================================
// Purpose:  EditMode tests of the voxr-frontend unit through its C ABI: layout, int16 bounds, pause and burst gain, lifecycle, target step, chunk invariance
// Layer:    Tests.Editor
// Owns:     FrontEndNativeTests (public class)
// Depends:  FrontEndNative, voxr_fe_stats, VoxrSpeechRecogniser, VoxrWavReader
// ============================================================================
#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using VoXR.Native;
using VoXR.Testing;

namespace VoXR.Tests.Editor
{
    public class FrontEndNativeTests
    {
        const int InputRate = 48000;
        const int OutputRate = 16000;
        const int CallSamples = 480;
        const float TargetDb = -18f;
        const float InitialGainDb = 15f;
        const double MaxGainDb = 50.0;

        const double F5SineHz = 1000.0;
        const int F5SineSamples = 2 * InputRate;
        const int F5SettleSamples = OutputRate / 100;
        const int F5MinRun = 4;

        const string LeadFile = "cease_fire.wav";
        const int LeadRepeats = 4;
        const int WindowFrames = 50;
        const int PauseSamples = 3 * InputRate;
        const int BurstSamples = InputRate / 5;
        const int NoiseOnlySamples = 10 * InputRate;
        const double NoiseFloorDbfs = -60.0;
        const int PauseNoiseSeed = 0x564f5852;
        const int BurstSeed = 0x42555253;
        const int NoiseOnlySeed = 0x4e4f4953;

        const string LifecycleFile = "launch_one_missiles_target_hotel_one.wav";

        const float F11Scale = 0.5f;
        const int F11MinSamples = 30 * InputRate;
        const int F11TailSamples = 10 * OutputRate;
        const double F11StepMin = 5.5;
        const double F11StepMax = 6.5;

        static readonly int[] Fixed480 = { CallSamples };
        static readonly int[] Fixed1237 = { 1237 };
        static readonly int[] Cycling = { 1, 7, 479, 481, 2003 };

        struct Frame
        {
            public int InputStart;
            public int InputEnd;
            public float GainDb;
            public float PreRms;
            public float PostRms;
        }

        readonly List<IntPtr> _units = new List<IntPtr>();

        [TearDown]
        public void TearDown()
        {
            foreach (IntPtr unit in _units)
                FrontEndNative.voxr_fe_destroy(unit);
            _units.Clear();
        }

        IntPtr Create(float targetDb)
        {
            IntPtr unit = FrontEndNative.voxr_fe_create(targetDb);
            Assert.AreNotEqual(IntPtr.Zero, unit, $"voxr_fe_create({targetDb}) returned null");
            _units.Add(unit);
            return unit;
        }

        static string TtsPath(string file)
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(VoxrSpeechRecogniser).Assembly
            );
            Assert.IsNotNull(pkg, "PackageInfo not found for the VoXR runtime assembly");
            return Path.Combine(pkg.resolvedPath, "Tests", "Fixtures", "audio", "tts", file);
        }

        static float[] Wav(string file) => VoxrWavReader.ReadFile(TtsPath(file));

        static float[] Repeat(float[] samples, int times)
        {
            var result = new float[samples.Length * times];
            for (int i = 0; i < times; i++)
                Array.Copy(samples, 0, result, i * samples.Length, samples.Length);
            return result;
        }

        static float[] Concat(params float[][] parts)
        {
            int length = 0;
            foreach (float[] part in parts)
                length += part.Length;
            var result = new float[length];
            int offset = 0;
            foreach (float[] part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

        static float[] Sine(int count, double hz, double amplitude)
        {
            var sine = new float[count];
            for (int i = 0; i < count; i++)
                sine[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * hz * i / InputRate));
            return sine;
        }

        static float[] WhiteNoise(int count, double rms, int seed)
        {
            var random = new System.Random(seed);
            var raw = new double[count];
            double energy = 0.0;
            for (int i = 0; i < count; i++)
            {
                raw[i] = random.NextDouble() * 2.0 - 1.0;
                energy += raw[i] * raw[i];
            }
            double scale = rms / Math.Sqrt(energy / count);
            var noise = new float[count];
            for (int i = 0; i < count; i++)
                noise[i] = (float)(raw[i] * scale);
            return noise;
        }

        static double Db(double ratio) => 20.0 * Math.Log10(ratio);

        static double Centi(double db) => Math.Round(db * 100.0, MidpointRounding.AwayFromZero) / 100.0;

        static voxr_fe_stats Stats(IntPtr unit)
        {
            Assert.AreEqual(0, FrontEndNative.voxr_fe_get_stats(unit, out voxr_fe_stats stats));
            return stats;
        }

        static List<Frame> Run(IntPtr unit, float[] signal, int[] callSizes, List<short> output)
        {
            int maxCall = 0;
            foreach (int size in callSizes)
                maxCall = Math.Max(maxCall, size);
            var chunk = new float[maxCall];
            var out16 = new short[maxCall / FrontEndNative.DecimationFactor + FrontEndNative.FrameSamples];
            var frames = new List<Frame>();
            int position = 0;
            int call = 0;
            while (position < signal.Length)
            {
                int n = Math.Min(callSizes[call++ % callSizes.Length], signal.Length - position);
                Array.Copy(signal, position, chunk, 0, n);
                int cap = n / FrontEndNative.DecimationFactor + FrontEndNative.FrameSamples;
                int written = FrontEndNative.voxr_fe_process(unit, chunk, n, out16, cap);
                if (written < 0)
                    Assert.Fail($"voxr_fe_process refused {n} samples with cap {cap}");
                if (written > 0)
                {
                    voxr_fe_stats stats = Stats(unit);
                    frames.Add(new Frame
                    {
                        InputStart = position,
                        InputEnd = position + n,
                        GainDb = stats.gain_db,
                        PreRms = stats.pre_rms,
                        PostRms = stats.post_rms,
                    });
                    if (output != null)
                        for (int i = 0; i < written; i++)
                            output.Add(out16[i]);
                }
                position += n;
            }
            return frames;
        }

        static double RmsOver(List<Frame> frames, List<int> indices, Func<Frame, float> field)
        {
            double energy = 0.0;
            foreach (int i in indices)
            {
                double value = field(frames[i]);
                energy += value * value;
            }
            return Math.Sqrt(energy / indices.Count);
        }

        static double MaxGainOver(List<Frame> frames, List<int> indices)
        {
            double max = double.NegativeInfinity;
            foreach (int i in indices)
                max = Math.Max(max, Centi(frames[i].GainDb));
            return max;
        }

        static double OutputTailDbfs(List<short> output, int tail)
        {
            Assert.GreaterOrEqual(output.Count, tail, "output shorter than the measured tail");
            double energy = 0.0;
            for (int i = output.Count - tail; i < output.Count; i++)
            {
                double value = output[i] / 32768.0;
                energy += value * value;
            }
            return Centi(Db(Math.Sqrt(energy / tail)));
        }

        static List<int> AllIndices(List<Frame> frames)
        {
            var indices = new List<int>();
            for (int i = 0; i < frames.Count; i++)
                indices.Add(i);
            return indices;
        }

        [Test]
        public void Abi_VersionAndStatsLayout()
        {
            Assert.AreEqual(1, FrontEndNative.voxr_fe_abi_version());
            Assert.AreEqual(40, Marshal.SizeOf<voxr_fe_stats>());
        }

        [Test]
        public void F5_FullScaleAndOverRange_NoWrap()
        {
            foreach (double amplitude in new[] { 1.0, 4.0 })
            {
                IntPtr unit = Create(TargetDb);
                var output = new List<short>();
                Run(unit, Sine(F5SineSamples, F5SineHz, amplitude), Fixed480, output);

                Assert.IsNotEmpty(output, $"amplitude {amplitude}: no output");
                Assert.Greater(
                    output.Count,
                    F5SettleSamples,
                    $"amplitude {amplitude}: output ends within the first {F5SettleSamples} samples"
                );

                var starts = new List<int> { F5SettleSamples };
                int sign = 0;
                for (int i = F5SettleSamples; i < output.Count; i++)
                {
                    int s = Math.Sign(output[i]);
                    if (s == 0)
                        continue;
                    if (sign != 0 && s != sign)
                        starts.Add(i);
                    sign = s;
                }
                starts.Add(output.Count);

                if (starts.Count < 4)
                    Assert.Fail($"amplitude {amplitude}: no complete same-sign run was checked");

                int shortest = int.MaxValue;
                for (int r = 1; r < starts.Count - 2; r++)
                {
                    int start = starts[r];
                    int length = starts[r + 1] - start;
                    if (length < F5MinRun)
                        Assert.Fail($"amplitude {amplitude}: same-sign run at output index {start} is {length} samples, under {F5MinRun}");
                    shortest = Math.Min(shortest, length);
                }
                TestContext.WriteLine(
                    $"amplitude {amplitude}: {starts.Count - 3} complete runs from output index {F5SettleSamples}, shortest {shortest} samples"
                );
            }
        }

        void AssertPauseThenBurst(bool noisePause)
        {
            float[] lead = Repeat(Wav(LeadFile), LeadRepeats);
            IntPtr unit = Create(TargetDb);
            List<Frame> leadFrames = Run(unit, lead, Fixed480, null);

            var window = new List<int>();
            for (int f = leadFrames.Count - 1; f >= 0 && window.Count < WindowFrames; f--)
                if (leadFrames[f].PreRms > 0f)
                    window.Insert(0, f);
            Assert.IsNotEmpty(window, "lead B has no frame with pre_rms > 0");
            double windowEndGain = Centi(leadFrames[window[window.Count - 1]].GainDb);
            Assert.AreNotEqual(
                (double)InitialGainDb,
                windowEndGain,
                "lead B left the gain at 15 dB: its window shows no adaptation"
            );

            double windowInputRms = RmsOver(leadFrames, window, r => r.PreRms);
            float[] burst = WhiteNoise(BurstSamples, 1.0, BurstSeed);
            IntPtr meter = Create(TargetDb);
            List<Frame> meterFrames = Run(meter, burst, Fixed480, null);
            meterFrames.RemoveAt(0);
            double burstScale = windowInputRms / RmsOver(meterFrames, AllIndices(meterFrames), r => r.PreRms);
            for (int i = 0; i < burst.Length; i++)
                burst[i] = (float)(burst[i] * burstScale);

            float[] pause = noisePause
                ? WhiteNoise(PauseSamples, Math.Pow(10.0, NoiseFloorDbfs / 20.0), PauseNoiseSeed)
                : new float[PauseSamples];
            int burstStart = lead.Length + pause.Length;
            int burstEnd = burstStart + burst.Length;

            IntPtr composite = Create(TargetDb);
            List<Frame> frames = Run(composite, Concat(lead, pause, burst), Fixed480, null);

            var afterLead = new List<int>();
            var burstFrames = new List<int>();
            for (int f = leadFrames.Count; f < frames.Count; f++)
            {
                afterLead.Add(f);
                if (frames[f].InputStart >= burstStart && frames[f].InputEnd <= burstEnd)
                    burstFrames.Add(f);
            }
            Assert.IsNotEmpty(afterLead, "no frame after the lead");
            Assert.IsNotEmpty(burstFrames, "no frame inside the burst");

            double windowMaxGain = MaxGainOver(frames, window);
            double afterMaxGain = MaxGainOver(frames, afterLead);
            double windowLift = Centi(
                Centi(Db(RmsOver(frames, window, r => r.PostRms)))
                    - Centi(Db(RmsOver(frames, window, r => r.PreRms)))
            );
            double burstLift = Centi(
                Centi(Db(RmsOver(frames, burstFrames, r => r.PostRms)))
                    - Centi(Db(RmsOver(frames, burstFrames, r => r.PreRms)))
            );

            TestContext.WriteLine(
                $"pause {(noisePause ? "noise -60 dBFS" : "digital silence")}: window frames {window.Count} "
                    + $"({window[0]}..{window[window.Count - 1]}), window input {Centi(Db(windowInputRms))} dBFS, "
                    + $"window max gain {windowMaxGain} dB, pause and burst max gain {afterMaxGain} dB, "
                    + $"window lift {windowLift} dB, burst lift {burstLift} dB, burst frames {burstFrames.Count}"
            );

            Assert.LessOrEqual(afterMaxGain, windowMaxGain, "gain over pause and burst exceeds the window's");
            Assert.LessOrEqual(burstLift, windowLift, "burst lift exceeds the window's");
        }

        [Test]
        public void F6_SilenceThenBurst() => AssertPauseThenBurst(false);

        [Test]
        public void F6_NoiseThenBurst() => AssertPauseThenBurst(true);

        [Test]
        public void F6_NoiseOnly()
        {
            IntPtr unit = Create(TargetDb);
            List<Frame> frames = Run(
                unit,
                WhiteNoise(NoiseOnlySamples, Math.Pow(10.0, NoiseFloorDbfs / 20.0), NoiseOnlySeed),
                Fixed480,
                null
            );

            Assert.IsNotEmpty(frames);
            double first = Centi(frames[0].GainDb);
            int above = 0;
            foreach (Frame frame in frames)
                if (Centi(frame.GainDb) > first)
                    above++;
            Assert.AreEqual(0, above, $"noise raised the gain above its first reading {first} dB");
        }

        [Test]
        public void F10_Lifecycle()
        {
            IntPtr unit = Create(TargetDb);
            Run(unit, Wav(LifecycleFile), Fixed480, null);
            float g1 = Stats(unit).gain_db;
            Assert.AreNotEqual(InitialGainDb, g1, "the fixture left the gain at 15 dB");

            FrontEndNative.voxr_fe_begin_stream(unit);
            Assert.AreEqual(g1, Stats(unit).gain_db, "begin_stream must keep the gain");

            FrontEndNative.voxr_fe_reset(unit);
            Assert.AreEqual(InitialGainDb, Stats(unit).gain_db, "reset must restore 15 dB");

            FrontEndNative.voxr_fe_destroy(unit);
            _units.Remove(unit);
            IntPtr fresh = Create(TargetDb);
            Assert.AreEqual(InitialGainDb, Stats(fresh).gain_db, "a fresh unit must start at 15 dB");
        }

        [Test]
        public void F11_FieldUnchanged()
        {
            FieldInfo field = typeof(VoxrSpeechRecogniser).GetField(
                "micGainTargetDb",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(field, "micGainTargetDb is gone");
            Assert.IsTrue(field.IsPrivate, "micGainTargetDb must stay private");
            Assert.AreEqual(typeof(float), field.FieldType);
            Assert.IsNotNull(field.GetCustomAttribute<SerializeField>(), "micGainTargetDb must stay [SerializeField]");
            Assert.IsNull(
                field.GetCustomAttribute<FormerlySerializedAsAttribute>(),
                "micGainTargetDb must carry no [FormerlySerializedAs]"
            );
        }

        [Test]
        public void F11_SixDbHigherTarget_AboutSixDbLouder()
        {
            float[] one = Wav(LeadFile);
            for (int i = 0; i < one.Length; i++)
                one[i] *= F11Scale;
            float[] signal = Repeat(one, (F11MinSamples + one.Length - 1) / one.Length);

            var levels = new Dictionary<float, double>();
            var finalGains = new Dictionary<float, double>();
            foreach (float target in new[] { -24f, -18f, -12f })
            {
                IntPtr unit = Create(target);
                var output = new List<short>();
                Run(unit, signal, Fixed480, output);
                levels[target] = OutputTailDbfs(output, F11TailSamples);
                finalGains[target] = Centi(Stats(unit).gain_db);
            }

            double step = Centi(levels[-18f] - levels[-24f]);
            double nextStep = Centi(levels[-12f] - levels[-18f]);
            TestContext.WriteLine(
                $"input {signal.Length} samples; L(-24) {levels[-24f]} dBFS, L(-18) {levels[-18f]} dBFS, "
                    + $"L(-18) - L(-24) {step} dB; final gain -24: {finalGains[-24f]} dB, -18: {finalGains[-18f]} dB; "
                    + $"L(-12) {levels[-12f]} dBFS, final gain -12: {finalGains[-12f]} dB, L(-12) - L(-18) {nextStep} dB"
            );

            foreach (float target in new[] { -24f, -18f })
            {
                double gain = finalGains[target];
                Assert.IsTrue(
                    gain > 0.0 && gain < MaxGainDb && gain != InitialGainDb,
                    $"final gain at {target} dB is {gain} dB: outside (0, 50) or still 15"
                );
            }
            Assert.IsTrue(
                step >= F11StepMin && step <= F11StepMax,
                $"L(-18) - L(-24) is {step} dB, outside [{F11StepMin}, {F11StepMax}]"
            );
        }

        [Test]
        public void ChunkInvariance()
        {
            float[] wav = Wav(LifecycleFile);
            var results = new List<voxr_fe_stats>();
            foreach (int[] schedule in new[] { Fixed480, Fixed1237, Cycling })
            {
                IntPtr unit = Create(TargetDb);
                Run(unit, wav, schedule, null);
                results.Add(Stats(unit));
            }

            for (int i = 1; i < results.Count; i++)
            {
                Assert.AreEqual(results[0].output_samples, results[i].output_samples, $"schedule {i}: output count");
                Assert.AreEqual(results[0].digest, results[i].digest, $"schedule {i}: digest");
            }
        }
    }
}
#endif
