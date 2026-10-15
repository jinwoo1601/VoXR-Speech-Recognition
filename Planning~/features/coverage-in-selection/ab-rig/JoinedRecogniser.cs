// ============================================================================
// Purpose:  The recogniser layer's staged world: player loop, frame clock, horizon, error-log guard, the drive-check verb, a case scored and traced, the configuration read
// Layer:    Planning.AbRig
// Owns:     JoinedRecogniser (internal static class), JoinedRecogniser.Observation (internal sealed class)
// Depends:  Joined, JoinedInput, JoinedScorer, BridgeNative, VoxrSpeechRecogniser, VoxrCommandRecogniser, VoxrResult, VoxrCommand, VoxrSlotMatch, VoxrSlotDefinition, VoxrCommandDefinition
// ============================================================================
// feat-joined-rig-recogniser Phases 1 and 3. Recogniser stage only (stage.sh's `recogniser`
// argument): a module initializer hands DriveCheck, ScoreCase and Config to Joined, so a parser
// stage compiles Joined unchanged and stops `drive-check` and `score --layer recogniser` by name
// (*Stage layering*). One world per run of RunWorld: both staged
// components built fresh, driven through Unity's messages by name, frame by frame on a fixed
// clock, results served by the stand-in bridge on the first frame at or after their audio time
// (*The player loop*, *The clock*, *Delivery*). `drive-check` runs five scripted worlds:
//
//   --joined drive-check
//
// Exit 0 all five passed; 1 a world FAILed; 2 a stop (a staged error log or failed assert, a
// world that did not come up, a command still pending at the horizon), named on stderr.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using UnityEngine;
using VoXR;
using VoXR.Commands;
using VoXR.Native;

namespace AbRig
{
    static class JoinedRecogniser
    {
        // *Tuning record*: provisional, Phase 4's tuning pass.
        internal const float TickRate = 90f;

        static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // The frame the player loop is on, for the observers.
        static int frame;

        // One observation, in the *Trace* shapes: a result through the speech recogniser's own
        // events, or a command recogniser event. Frame is the rig's, for drive-check's checks.
        internal sealed class Observation
        {
            internal string Kind; // "result" or "event"
            internal string Case;
            internal int Frame;
            internal float FrameTime;

            internal int Seq;
            internal bool Final;
            internal double AudioTime;
            internal string Text;

            internal string Event; // "fire", "pending", "confirmed" or "cancelled"
            internal string Intent;
            internal VoxrSlotMatch[] Slots;
            internal VoxrCommand Command; // the event's command, whole
        }

#pragma warning disable CA2255
        [ModuleInitializer]
        internal static void Register()
        {
            Joined.DriveCheck = DriveCheck;
            Joined.ScoreRecogniser = ScoreCase;
            Joined.RecogniserConfig = Config;
        }
#pragma warning restore CA2255

        // The least frame k with k / TickRate at or after the audio time.
        internal static int DeliveryFrame(double audioTime)
        {
            int k = 0;
            while ((double)k / TickRate < audioTime)
                k++;
            return k;
        }

        // The least frame f after d at which `span` has elapsed since d, in the float arithmetic
        // Time.time takes (UtteranceBuffer.ShouldFlush; the pending timeout).
        static int AfterFrame(int d, float span)
        {
            int f = d + 1;
            while ((float)f / TickRate - (float)d / TickRate < span)
                f++;
            return f;
        }

        // A float field of the staged command recogniser, read by name (*Drift watch*).
        static float ReadFloat(VoxrCommandRecogniser recogniser, string field)
        {
            FieldInfo f = typeof(VoxrCommandRecogniser).GetField(
                field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (f == null || f.FieldType != typeof(float))
                throw new JoinedInput.Stop(
                    2,
                    "the staged VoxrCommandRecogniser has no float field \"" + field + "\""
                );
            return (float)f.GetValue(recogniser);
        }

        // One fresh world driven from frame 0 to its horizon K; returns what it observed.
        internal static List<Observation> RunWorld(
            string name,
            IReadOnlyList<(double AudioTime, bool Final, string Payload)> timeline,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            Debug.RigErrors.Clear();
            BridgeNative.RigLoad(timeline);
            Clock(0);
            var observed = new List<Observation>();

            var speech = new VoxrSpeechRecogniser();
            var command = new VoxrCommandRecogniser();

            // Horizon: the last result's audio time (0 for an empty timeline) plus the staged
            // timers, then 3 frames (*The clock*, ruled 2026-10-15).
            double h =
                (timeline.Count == 0 ? 0 : timeline[timeline.Count - 1].AudioTime)
                + ReadFloat(command, "bufferWindow")
                + ReadFloat(command, "pendingTimeout");
            int horizon = DeliveryFrame(h) + 3;

            // The serialized reference, wired while disabled, as a scene loads it.
            command.SpeechRecogniser = speech;
            Send(name, speech, "Awake", "frame 0 (setup)");
            Send(name, command, "Awake", "frame 0 (setup)");
            speech.isActiveAndEnabled = true;
            command.isActiveAndEnabled = true;
            // The rig's observers first: a result is traced before any fire it raises synchronously (*Trace*, timeline order).
            speech.OnResult += r => observed.Add(Result(name, true, r.Text, timeline));
            speech.OnPartialResult += text => observed.Add(Result(name, false, text, timeline));
            command.OnCommandRecognised += c => observed.Add(Event(name, "fire", c));
            command.OnCommandPending += c => observed.Add(Event(name, "pending", c));
            command.OnCommandConfirmed += c => observed.Add(Event(name, "confirmed", c));
            command.OnCommandCancelled += c => observed.Add(Event(name, "cancelled", c));

            Send(name, speech, "OnEnable", "frame 0 (setup)");
            Send(name, command, "OnEnable", "frame 0 (setup)");

            command.Configure(slots, commands);
            Guard(name, "frame 0 (setup), Configure");
            speech.StartRecognition();
            Guard(name, "frame 0 (setup), StartRecognition");
            if (
                !speech.IsModelReady
                || !speech.IsRecognising
                || BridgeNative.RigAppliedGrammar == null
            )
                throw new JoinedInput.Stop(
                    2,
                    "world "
                        + name
                        + ": did not come up after StartRecognition: IsModelReady "
                        + speech.IsModelReady
                        + ", IsRecognising "
                        + speech.IsRecognising
                        + ", grammar applied (HandleModelReady) "
                        + (BridgeNative.RigAppliedGrammar != null)
                );

            for (int k = 0; k <= horizon; k++)
            {
                Clock(k);
                Send(name, speech, "Update", "frame " + k);
                Send(name, command, "Update", "frame " + k);
            }

            bool pending = command.HasPendingCommand;
            string at = "frame " + horizon + " (teardown)";
            speech.isActiveAndEnabled = false;
            command.isActiveAndEnabled = false;
            Send(name, speech, "OnDisable", at);
            Send(name, command, "OnDisable", at);
            Send(name, speech, "OnDestroy", at);
            Send(name, command, "OnDestroy", at);
            if (pending)
                throw new JoinedInput.Stop(
                    2,
                    "world "
                        + name
                        + ": a command is still pending at the horizon, frame "
                        + horizon
                );
            return observed;
        }

        // Frame k: the clock and the stand-in bridge's frame.
        static void Clock(int k)
        {
            frame = k;
            Time.time = (float)k / TickRate;
            BridgeNative.RigFrame = k;
        }

        static void Send(string world, MonoBehaviour target, string message, string at)
        {
            target.SendMessage(message, SendMessageOptions.DontRequireReceiver);
            Guard(world, at + ", " + target.GetType().Name + "." + message);
        }

        // *Guards at this layer*, the error-log part: a LogError or failed Assert from the staged
        // sources means the drive left the component's contract.
        static void Guard(string world, string at)
        {
            if (Debug.RigErrors.Count > 0)
                throw new JoinedInput.Stop(
                    2,
                    "world " + world + ", " + at + ": " + Debug.RigErrors[0]
                );
        }

        static Observation Result(
            string world,
            bool final,
            string text,
            IReadOnlyList<(double AudioTime, bool Final, string Payload)> timeline
        ) =>
            new Observation
            {
                Kind = "result",
                Case = world,
                Frame = frame,
                FrameTime = Time.time,
                Seq = BridgeNative.RigLastServed,
                Final = final,
                AudioTime = timeline[BridgeNative.RigLastServed].AudioTime,
                Text = text,
            };

        static Observation Event(string world, string name, VoxrCommand c) =>
            new Observation
            {
                Kind = "event",
                Case = world,
                Frame = frame,
                FrameTime = Time.time,
                Event = name,
                Intent = c.Intent,
                Slots = c.Slots,
                Command = c,
            };

        // -------- score --layer recogniser --------

        // One case through a fresh world (feat-joined-rig-recogniser, *Recogniser-layer
        // scoring*, *Timing measures*, *Trace*): the dump's results in order at `consumed` /
        // `sampleRate`; the first fire is the case's fire, the rest extra fires; the class is
        // phase 1's (JoinedScorer.Classify). Stop, exit 2: no sample rate, or a `raw` result.
        static JoinedScorer.Outcome ScoreCase(
            JoinedInput.ManifestCase c,
            JoinedInput.DumpCase d,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            if (d.SampleRate <= 0)
                throw new JoinedInput.Stop(
                    2,
                    "case \"" + c.Name + "\": `sampleRate` " + d.SampleRate + ", expected > 0"
                );
            var timeline = new List<(double AudioTime, bool Final, string Payload)>();
            foreach (JoinedInput.DumpResult r in d.Results)
            {
                if (r.Raw != null)
                    throw new JoinedInput.Stop(
                        2,
                        "case \"" + c.Name + "\": a `raw` result at seq " + r.Seq + ", no payload"
                    );
                timeline.Add(((double)r.Consumed.Value / d.SampleRate, r.Final, r.Payload));
            }

            List<Observation> observed = RunWorld(c.Name, timeline, slots, commands);

            List<Observation> fires = Events(observed, "fire");
            var o = new JoinedScorer.Outcome
            {
                HasPrimary = fires.Count > 0,
                ExtraFires = Math.Max(0, fires.Count - 1),
                FinalCount = d.Finals.Count,
                FireTimes = new double[fires.Count],
                AppliedGrammar = BridgeNative.RigAppliedGrammar,
            };
            if (o.HasPrimary)
                o.Primary = fires[0].Command;
            o.Class = JoinedScorer.Classify(o.HasPrimary, o.Primary, c, out o.SlotCredit);
            for (int i = 0; i < fires.Count; i++)
                o.FireTimes[i] = fires[i].FrameTime;
            o.Delay = o.HasPrimary
                ? o.FireTimes[0] - (double)d.Samples / d.SampleRate
                : (double?)null;

            // Pending: a `pending` event with no `confirmed` after it.
            var trace = new StringBuilder();
            foreach (Observation x in observed)
            {
                if (x.Kind == "event" && x.Event == "pending")
                    o.Pending = true;
                else if (x.Kind == "event" && x.Event == "confirmed")
                    o.Pending = false;
                trace.Append(Line(x)).Append('\n');
            }
            o.Trace = trace.ToString();
            return o;
        }

        // The configuration a fresh staged recogniser carries (*Configuration*): every instance
        // field it declares with [SerializeField], in source (metadata) order, as (name, text);
        // and the tick rate.
        static (float, KeyValuePair<string, string>[]) Config()
        {
            var fresh = new VoxrCommandRecogniser();
            var fields = new List<FieldInfo>();
            foreach (
                FieldInfo f in typeof(VoxrCommandRecogniser).GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                )
            )
                if (f.GetCustomAttribute<SerializeFieldAttribute>() != null)
                    fields.Add(f);
            fields.Sort((x, y) => x.MetadataToken.CompareTo(y.MetadataToken));

            var config = new KeyValuePair<string, string>[fields.Count];
            for (int i = 0; i < fields.Count; i++)
                config[i] = new KeyValuePair<string, string>(
                    fields[i].Name,
                    ConfigText(fields[i].GetValue(fresh))
                );
            return (TickRate, config);
        }

        // Invariant text: null `null`, a float round-trip, a bool lower case, an array `[a, b]`.
        static string ConfigText(object value)
        {
            if (value == null)
                return "null";
            if (value is float f)
                return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is bool b)
                return b ? "true" : "false";
            if (value is Array array)
            {
                var parts = new List<string>();
                foreach (object e in array)
                    parts.Add(ConfigText(e));
                return "[" + string.Join(", ", parts) + "]";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        // -------- drive-check --------

        static int DriveCheck(
            string[] args,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            int at = Array.IndexOf(args, "--joined");
            for (int i = 0; i < args.Length; i++)
                if (i != at && i != at + 1)
                    throw new JoinedInput.Stop(2, "unknown argument \"" + args[i] + "\"");

            var fresh = new VoxrCommandRecogniser();
            float w = ReadFloat(fresh, "bufferWindow");
            float timeout = ReadFloat(fresh, "pendingTimeout");

            VoxrCommandDefinition[] confirming = Array.ConvertAll(
                commands,
                d =>
                    d.Intent == "cease_fire"
                        ? new VoxrCommandDefinition(d.Intent, d.Patterns, d.AllowPartialMatch, true)
                        : d
            );

            int passed = 0;
            int worlds = 0;

            void World(
                string name,
                (double AudioTime, bool Final, string Payload)[] timeline,
                VoxrCommandDefinition[] defs,
                Func<List<Observation>, string> check
            )
            {
                List<Observation> observed = RunWorld(name, timeline, slots, defs);
                foreach (Observation o in observed)
                    Print(o);
                string why = check(observed);
                worlds++;
                if (why == null)
                {
                    passed++;
                    Console.Out.WriteLine("drive-check: " + name + " ok");
                }
                else
                    Console.Out.WriteLine("drive-check: " + name + " FAIL " + why);
            }

            // 1. A fire on the frame grid: delivered on the first frame at or after 1.005 s.
            World(
                "grid",
                new[] { (1.005, true, FinalPayload("cease fire")) },
                commands,
                o => OneFire(o, "cease_fire", AfterFrame(DeliveryFrame(1.005), w))
            );

            // 2. Two finals inside the buffer window merge into one command.
            World(
                "merge",
                new[]
                {
                    (1.0, true, FinalPayload("cease")),
                    (1.0 + 0.6 * w, true, FinalPayload("fire")),
                },
                commands,
                o => OneFire(o, "cease_fire", AfterFrame(DeliveryFrame(1.0 + 0.6 * w), w))
            );

            // 3. The same two finals a window apart do not.
            World(
                "apart",
                new[]
                {
                    (1.0, true, FinalPayload("cease")),
                    (1.0 + 2 * w, true, FinalPayload("fire")),
                },
                commands,
                o =>
                {
                    List<Observation> fires = Events(o, "fire")
                        .FindAll(e => e.Intent == "cease_fire");
                    return fires.Count == 0
                        ? null
                        : "fires " + Describe(Events(o, "fire")) + ", want no cease_fire";
                }
            );

            // 4. A partial reaches OnPartialResult on its delivery frame; then world 1's fire.
            World(
                "partial",
                new[]
                {
                    (0.8, false, PartialPayload("cease")),
                    (1.0, true, FinalPayload("cease fire")),
                },
                commands,
                o =>
                {
                    List<Observation> partials = o.FindAll(x => x.Kind == "result" && !x.Final);
                    int d = DeliveryFrame(0.8);
                    if (
                        partials.Count != 1
                        || partials[0].Text != "cease"
                        || partials[0].Frame != d
                    )
                        return "partials "
                            + Describe(partials)
                            + ", want one \"cease\" at frame "
                            + d;
                    return OneFire(o, "cease_fire", AfterFrame(DeliveryFrame(1.0), w));
                }
            );

            // 5. A command needing confirmation pends on the flush frame and times out, unfired.
            World(
                "pending",
                new[] { (1.0, true, FinalPayload("cease fire")) },
                confirming,
                o =>
                {
                    int p = AfterFrame(DeliveryFrame(1.0), w);
                    int t = AfterFrame(p, timeout);
                    List<Observation> pendings = Events(o, "pending");
                    List<Observation> cancels = Events(o, "cancelled");
                    List<Observation> fires = Events(o, "fire");
                    if (!OneAt(pendings, "cease_fire", p))
                        return "pending "
                            + Describe(pendings)
                            + ", want one cease_fire at frame "
                            + p;
                    if (!OneAt(cancels, "cease_fire", t))
                        return "cancelled "
                            + Describe(cancels)
                            + ", want one cease_fire at frame "
                            + t;
                    if (fires.Count != 0)
                        return "fires " + Describe(fires) + ", want none";
                    return null;
                }
            );

            Console.Out.WriteLine("drive-check: " + passed + "/" + worlds + " worlds passed");
            return passed == worlds ? 0 : 1;
        }

        static List<Observation> Events(List<Observation> o, string name) =>
            o.FindAll(x => x.Kind == "event" && x.Event == name);

        static bool OneAt(List<Observation> events, string intent, int at) =>
            events.Count == 1 && events[0].Intent == intent && events[0].Frame == at;

        static string OneFire(List<Observation> o, string intent, int at)
        {
            List<Observation> fires = Events(o, "fire");
            return OneAt(fires, intent, at)
                ? null
                : "fires " + Describe(fires) + ", want one " + intent + " at frame " + at;
        }

        static string Describe(List<Observation> observed)
        {
            var parts = new List<string>();
            foreach (Observation x in observed)
                parts.Add((x.Kind == "event" ? x.Intent : "\"" + x.Text + "\"") + "@" + x.Frame);
            return "[" + string.Join(", ", parts) + "]";
        }

        // Vosk's shapes, conf 1.0: a final's words with their times, then its text; a partial.
        static string FinalPayload(string text)
        {
            var sb = new StringBuilder("{\"result\":[");
            string[] words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append("{\"conf\":1.0,\"end\":")
                    .Append(((i + 1) * 0.3).ToString("0.0#", CultureInfo.InvariantCulture))
                    .Append(",\"start\":")
                    .Append((i * 0.3).ToString("0.0#", CultureInfo.InvariantCulture))
                    .Append(",\"word\":\"")
                    .Append(words[i])
                    .Append("\"}");
            }
            return sb.Append("],\"text\":\"").Append(text).Append("\"}").ToString();
        }

        static string PartialPayload(string text) => "{\"partial\":\"" + text + "\"}";

        static void Print(Observation o) => Console.Out.WriteLine(Line(o));

        // One observation as a *Trace* line, no line end; null slots as [].
        static string Line(Observation o)
        {
            var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteString("kind", o.Kind);
                w.WriteString("case", o.Case);
                if (o.Kind == "result")
                {
                    w.WriteNumber("seq", o.Seq);
                    w.WriteBoolean("final", o.Final);
                    w.WriteNumber("audioTime", o.AudioTime);
                    w.WriteNumber("frameTime", o.FrameTime);
                    w.WriteString("text", o.Text);
                }
                else
                {
                    w.WriteString("event", o.Event);
                    w.WriteNumber("frameTime", o.FrameTime);
                    w.WriteString("intent", o.Intent);
                    w.WriteStartArray("slots");
                    foreach (VoxrSlotMatch slot in o.Slots ?? Array.Empty<VoxrSlotMatch>())
                    {
                        w.WriteStartObject();
                        w.WriteString("name", slot.Name);
                        w.WriteString("value", slot.Value);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
