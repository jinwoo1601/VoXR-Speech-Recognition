// #124, REFUSE-TO-COMPETE variant. Verify3 measured refuse-to-FIRE (suppress the round
// winner after selection picked it); the 2026-08-27 ruling is refuse-to-COMPETE — exclude
// leading-missed candidates from SELECTION so a legitimate rival can win the round instead.
// The bar lives in CompareCandidate beside the DR-7 admission rule; IsAdmissibleStart runs
// its own count on a forStartProbe match and never calls the comparator, so orphan-run
// termination (#82) is untouched by construction.
//
// Three arms, because the slot-leading fork is deferred to these numbers:
//   0 = off, 1 = uniform (any first required element), 2 = literal-only.
using System;
using System.Globalization;
using System.Text;
using VoXR.Commands;

namespace AbRig
{
    static class Verify4
    {
        static VoxrSlotDefinition[] DemoSlots() =>
            new[]
            {
                new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two", "alpha one" }),
                new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                new VoxrSlotDefinition("burn_level", new[] { "hard burn", "soft burn" }),
            };

        static VoxrCommandDefinition[] DemoCommands() =>
            new[]
            {
                new VoxrCommandDefinition("cease_fire", new[] { new[] { "cease", "fire" } }),
                new VoxrCommandDefinition("approach_target",
                    new[] { new[] { "approach", "target", "{target}" } }),
                new VoxrCommandDefinition("launch_weapon",
                    new[] { new[] { "launch", "{weapon}", "target", "{target}" } }),
                new VoxrCommandDefinition("decelerate",
                    new[] { new[] { "decelerate" }, new[] { "decelerate", "by", "{burn_level}" } }),
            };

        static VoxrSlotDefinition[] IssueSlots() =>
            new[] { VoxrSlotDefinition.NumberSequence("track", minWords: 1, maxWords: 4) };

        static VoxrCommandDefinition[] IssueCommands() =>
            new[]
            {
                new VoxrCommandDefinition("query_time_to_target", new[] { new[] { "time", "to", "target" } }),
                new VoxrCommandDefinition("intercept_target", new[] { new[] { "intercept", "track", "{track}" } }),
            };

        // The refuse-to-COMPETE discriminator. Under refuse-to-FIRE the phantom still WINS
        // round 2 (startIdx outranks score), consumes its tokens, and is then suppressed —
        // so the legitimate rival starting one token later never fires. Under the bar the
        // phantom never competes, and the rival wins the round.
        static VoxrSlotDefinition[] RivalSlots() =>
            new[] { VoxrSlotDefinition.NumberSequence("track", minWords: 1, maxWords: 4) };

        static VoxrCommandDefinition[] RivalCommands() =>
            new[]
            {
                new VoxrCommandDefinition("query_time_to_target", new[] { new[] { "time", "to", "target" } }),
                new VoxrCommandDefinition("intercept_target", new[] { new[] { "intercept", "track", "{track}" } }),
                // Starts one token LATER than the phantom, and scores 1.0.
                new VoxrCommandDefinition("designate_track", new[] { new[] { "{track}", "designate" } }),
            };

        // Slot-leading patterns, for the deferred fork. Arm 2 must leave these firing.
        static VoxrSlotDefinition[] SlotLeadSlots() =>
            new[]
            {
                new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two" }),
                VoxrSlotDefinition.NumberSequence("track", minWords: 1, maxWords: 4),
            };

        static VoxrCommandDefinition[] SlotLeadCommands() =>
            new[]
            {
                new VoxrCommandDefinition("cease_fire", new[] { new[] { "cease", "fire" } }),
                // First required element is a SLOT.
                new VoxrCommandDefinition("track_hold", new[] { new[] { "{track}", "hold", "steady" } }),
            };

        static string Row(VoxrCommandParser p, string line)
        {
            var r = p.Parse(line);
            var sb = new StringBuilder();
            sb.Append("n=").Append(r.Length).Append("  ");
            for (int i = 0; i < r.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                var c = r[i].Command;
                sb.Append(c.Intent).Append('=').Append(c.Score.ToString("F4", CultureInfo.InvariantCulture));
                for (int s = 0; s < c.Slots.Length; s++)
                    sb.Append(" [").Append(c.Slots[s].Name).Append('=').Append(c.Slots[s].Value).Append(']');
            }
            return sb.ToString();
        }

        public static void Go()
        {
            var cases = new (string grammar, string text, string note)[]
            {
                ("issue", "time to target track one two four four", "#124 merged - MUST NOT fire intercept"),
                ("issue", "track one two four four",                "#124 standalone - MUST NOT fire"),
                ("issue", "intercept track one two four four",      "genuine - MUST still fire 1.0"),
                ("issue", "time to target",                         "clean query"),
                ("demo",  "cease fire target hotel one",            "s7 D - #82's protected shape"),
                ("demo",  "cease fire approach target hotel one",   "s7 D clean - both must fire"),
                ("demo",  "cease fire launch missiles target hotel one", "s4 two clean commands"),
                ("demo",  "decelerate hard burn",                   "interior 'by' drop - MUST still fire"),
                ("demo",  "cease fire",                             "single clean"),
                ("rival", "time to target track one two four four designate", "COMPETE test - rival must win rd 2"),
                ("slot",  "cease fire one two hold steady",         "slot-leading clean - fires either arm"),
                ("slot",  "cease fire hold steady",                 "slot-leading MISS - arm1 bars, arm2 does not"),
            };

            foreach (var arm in new[] { 0, 4, 1, 3, 2 })
            {
                VoxrCommandParser.BarMode = arm;
                string label = arm == 0 ? "OFF" : arm == 4 ? "refuse-to-FIRE" : arm == 1 ? "refuse-to-COMPETE, uniform" : arm == 3 ? "refuse-to-COMPETE + leading excusal" : "refuse-to-COMPETE, literal-only";
                Console.Out.WriteLine($"===== BarMode = {arm} ({label}) =====");
                var w = VoxrCommandParser.DefaultCoverageWeight;
                var none = Array.Empty<string>();
                var pi = new VoxrCommandParser(IssueSlots(), IssueCommands(), w, none);
                var pd = new VoxrCommandParser(DemoSlots(), DemoCommands(), w, none);
                var pr = new VoxrCommandParser(RivalSlots(), RivalCommands(), w, none);
                var ps = new VoxrCommandParser(SlotLeadSlots(), SlotLeadCommands(), w, none);
                foreach (var (g, t, note) in cases)
                {
                    var p = g == "issue" ? pi : g == "demo" ? pd : g == "rival" ? pr : ps;
                    Console.Out.WriteLine($"  '{t}'".PadRight(60) + Row(p, t).PadRight(62) + "// " + note);
                }
                Console.Out.WriteLine();
            }
            VoxrCommandParser.BarMode = 0;
        }
    }
}
