// #124: does barring a LEADING-required miss from FIRING actually revert issue #82?
// #82's win is orphan-run TERMINATION (the first command keeps its 1.0). That lives in
// BuildCoverageTables/IsAdmissibleStart and is untouched here. This isolates the two.
using System;
using System.Globalization;
using System.Text;
using VoXR.Commands;

namespace AbRig
{
    static class Verify3
    {
        static VoxrSlotDefinition[] DemoSlots() =>
            new[]
            {
                new VoxrSlotDefinition("target", new[] { "hotel one", "hotel two", "alpha one" }),
                new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                new VoxrSlotDefinition("burn_level", new[] { "hard burn", "soft burn" }),
            };

        // scoring.md §7 D grammar + the decelerate/by worked example.
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
                ("issue", "time to target track one two four four", "#124 merged — MUST NOT fire intercept"),
                ("issue", "track one two four four",                "#124 standalone — MUST NOT fire"),
                ("issue", "intercept track one two four four",      "genuine — MUST still fire 1.0"),
                ("issue", "time to target",                         "clean query"),
                ("demo",  "cease fire target hotel one",            "§7 D — #82's protected shape"),
                ("demo",  "cease fire approach target hotel one",   "§7 D clean — both must fire"),
                ("demo",  "cease fire launch missiles target hotel one", "§4 two clean commands"),
                ("demo",  "decelerate hard burn",                   "interior 'by' drop — MUST still fire"),
                ("demo",  "cease fire",                             "single clean"),
            };

            foreach (var arm in new[] { false, true })
            {
                VoxrCommandParser.BarLeadingMissFromFiring = arm;
                Console.Out.WriteLine($"===== BarLeadingMissFromFiring = {arm} =====");
                var pi = new VoxrCommandParser(IssueSlots(), IssueCommands(), VoxrCommandParser.DefaultCoverageWeight, Array.Empty<string>());
                var pd = new VoxrCommandParser(DemoSlots(), DemoCommands(), VoxrCommandParser.DefaultCoverageWeight, Array.Empty<string>());
                foreach (var (g, t, note) in cases)
                {
                    var p = g == "issue" ? pi : pd;
                    Console.Out.WriteLine($"  '{t}'".PadRight(52) + Row(p, t).PadRight(58) + "// " + note);
                }
                Console.Out.WriteLine();
            }
            VoxrCommandParser.BarLeadingMissFromFiring = false;
        }
    }
}
