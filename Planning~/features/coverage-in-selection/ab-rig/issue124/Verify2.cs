// Does the reporter's own 2026-08-19 grammar shape ("the query intent claims its own tail")
// prevent the #124 phantom on a STOCK parser? Field logs say it did, over 8 utterances.
using System;
using System.Globalization;
using System.Text;
using VoXR.Commands;

namespace AbRig
{
    static class Verify2
    {
        static VoxrSlotDefinition[] Slots() =>
            new[] { VoxrSlotDefinition.NumberSequence("track", minWords: 1, maxWords: 4) };

        static readonly string[] Cases =
        {
            "time to target track one two four four",
            "track one two four four",
            "time to target one two four four",   // VOSK dropped 'track' — field entry [4]
            "time to target track",
            "time to target",
            "intercept track one two four four",
            "intercept track one two",
        };

        static void Run(string label, VoxrCommandDefinition[] cmds)
        {
            Console.Out.WriteLine("== " + label + " ==");
            var p = new VoxrCommandParser(Slots(), cmds, VoxrCommandParser.DefaultCoverageWeight, Array.Empty<string>());
            foreach (var line in Cases)
            {
                var r = p.Parse(line);
                var sb = new StringBuilder();
                sb.Append('\'').Append(line).Append('\'').Append(new string(' ', Math.Max(1, 42 - line.Length)))
                  .Append("n=").Append(r.Length).Append("  ");
                for (int i = 0; i < r.Length; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    var c = r[i].Command;
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
            Run("AUG-26 repro grammar (as filed on #124)", new[]
            {
                new VoxrCommandDefinition("query_time_to_target", new[] { new[] { "time", "to", "target" } }),
                new VoxrCommandDefinition("intercept_target", new[] { new[] { "intercept", "track", "{track}" } }),
            });

            Run("AUG-19 field grammar — query claims its own tail", new[]
            {
                new VoxrCommandDefinition("query_time_to_target", new[]
                {
                    new[] { "time", "to", "target" },
                    new[] { "time", "to", "target", "track", "{track}" },
                }),
                new VoxrCommandDefinition("intercept_target", new[] { new[] { "intercept", "track", "{track}" } }),
            });
        }
    }
}
