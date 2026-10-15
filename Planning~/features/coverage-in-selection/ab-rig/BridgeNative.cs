// ============================================================================
// Purpose:  Stand-in for the shipped P/Invoke bridge: lifecycle calls succeed, results served from a scripted timeline by frame
// Layer:    Planning.AbRig
// Owns:     BridgeNative (internal static class)
// Depends:  JoinedRecogniser
// ============================================================================
// Recogniser stage only (stage.sh's `recogniser` argument), in place of
// Runtime/Native/BridgeNative.cs, under its shipped name and with only the members the staged
// sources call (feat-joined-rig-recogniser, *Staging*, *Delivery*, *Drift watch*). No `unsafe`:
// a result's pointer is an opaque token, its index + 1, which SpanFromPtr maps back to the
// result's managed bytes.
using System;
using System.Collections.Generic;
using System.Text;

namespace VoXR.Native
{
    internal static class BridgeNative
    {
        // The rig's side: the frame the player loop is on, the timeline index last served (the
        // trace's `seq`) and the grammar the staged recogniser last applied.
        internal static int RigFrame;
        internal static int RigLastServed;
        internal static string RigAppliedGrammar;

        static readonly List<byte[]> Payloads = new List<byte[]>();
        static readonly List<bool> Finals = new List<bool>();
        static readonly List<int> DeliveryFrames = new List<int>();
        static int next;
        static bool initialised;
        static bool running;

        // Loads one world's timeline, in order, and resets everything else.
        internal static void RigLoad(
            IReadOnlyList<(double AudioTime, bool Final, string Payload)> timeline
        )
        {
            Payloads.Clear();
            Finals.Clear();
            DeliveryFrames.Clear();
            foreach (var entry in timeline)
            {
                Payloads.Add(Encoding.UTF8.GetBytes(entry.Payload));
                Finals.Add(entry.Final);
                DeliveryFrames.Add(AbRig.JoinedRecogniser.DeliveryFrame(entry.AudioTime));
            }
            next = 0;
            initialised = false;
            running = false;
            RigFrame = 0;
            RigLastServed = -1;
            RigAppliedGrammar = null;
        }

        internal static int vosk_bridge_init(
            string modelPath,
            float sampleRate,
            float micGainTargetDb
        )
        {
            initialised = true;
            return 0;
        }

        internal static void vosk_bridge_destroy() => initialised = false;

        internal static int vosk_bridge_start()
        {
            running = true;
            return 0;
        }

        internal static void vosk_bridge_stop() => running = false;

        internal static int vosk_bridge_reset() => 0;

        // The next queued result whose delivery frame the player loop has reached, in order.
        internal static IntPtr vosk_bridge_get_result(out int isFinal, out int length)
        {
            if (next >= Payloads.Count || DeliveryFrames[next] > RigFrame)
            {
                isFinal = 0;
                length = 0;
                return IntPtr.Zero;
            }
            int index = next++;
            RigLastServed = index;
            isFinal = Finals[index] ? 1 : 0;
            length = Payloads[index].Length;
            return (IntPtr)(index + 1);
        }

        internal static int vosk_bridge_is_running() => running ? 1 : 0;

        internal static int vosk_bridge_is_initialised() => initialised ? 1 : 0;

        internal static int vosk_bridge_set_grammar(string grammarJson)
        {
            RigAppliedGrammar = grammarJson;
            return 0;
        }

        internal static float vosk_bridge_get_input_level() => 0f;

        internal static ReadOnlySpan<byte> SpanFromPtr(IntPtr ptr, int length) =>
            ptr == IntPtr.Zero
                ? default
                : new ReadOnlySpan<byte>(Payloads[(int)ptr - 1], 0, length);

        internal static string GetLastError() => string.Empty;
    }
}
