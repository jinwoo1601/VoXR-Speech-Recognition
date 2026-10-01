// Independent verification of the Fable memo's Battery B (benign suffix-shadow intent),
// on a STOCK parser staged clean from the worktree. No parser instrumentation.
using System;
using System.Globalization;
using System.Text;
using VoXR.Commands;

namespace AbRig
{
    static class Verify
    {
        static VoxrSlotDefinition[] Slots() =>
            new[] { VoxrSlotDefinition.NumberSequence("track", minWords: 1, maxWords: 4) };

        static VoxrCommandDefinition Query() =>
            new VoxrCommandDefinition("query_time_to_target", new[] { new[] { "time", "to", "target" } });

        static VoxrCommandDefinition Intercept() =>
            new VoxrCommandDefinition("intercept_target", new[] { new[] { "intercept", "track", "{track}" } });

        static VoxrCommandDefinition Shadow() =>
            new VoxrCommandDefinition("designate_track", new[] { new[] { "track", "{track}" } });

        static readonly string[] Cases =
        {
            "time to target track one two four four",
            "track one two four four",
            "track one two",
            "intercept track one two four four",
            "time to target track",
            "time to target",
            "intercept track one two",
        };

        static void Run(string label, VoxrCommandDefinition[] cmds)
        {
            Console.Out.WriteLine("== " + label + " ==");
            var parser = new VoxrCommandParser(Slots(), cmds, VoxrCommandParser.DefaultCoverageWeight, Array.Empty<string>());
            var sb = new StringBuilder();
            foreach (var line in Cases)
            {
                var results = parser.Parse(line);
                sb.Clear();
                sb.Append('\'').Append(line).Append('\'').Append(new string(' ', Math.Max(1, 42 - line.Length)))
                  .Append("n=").Append(results.Length).Append("  ");
                for (int i = 0; i < results.Length; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    var c = results[i].Command;
                    sb.Append(c.Intent).Append('=').Append(c.Score.ToString("F4", CultureInfo.InvariantCulture));
                    for (int s = 0; s < c.Slots.Length; s++)
                        sb.Append(" [").Append(c.Slots[s].Name).Append('=').Append(c.Slots[s].Value).Append(']');
                }
                Console.Out.WriteLine(sb.ToString());
            }
            Console.Out.WriteLine();
        }

        public static void Go()
        {
            Run("BASELINE — reporter's grammar as filed", new[] { Query(), Intercept() });
            Run("SHADOW LAST — designate_track registered last", new[] { Query(), Intercept(), Shadow() });
            Run("SHADOW FIRST — designate_track registered first", new[] { Shadow(), Query(), Intercept() });
        }
    }
}
