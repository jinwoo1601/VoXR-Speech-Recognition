// A/B rig for feat-fidelity-miss-cost (issue #65 §5.1), per project memory `grammar-ab-rig`.
//
// Compiles the REAL parser sources staged from a given git revision and replays utterances
// through them, so the before/after comparison is between two builds of shipped code rather
// than between a model and a re-implementation. Two modes:
//
//   --grammar   print GenerateGrammarJson over the demo grammar. Used to validate the rig
//               against the committed `grammar` pin in NativeBridge~/harness/expectations.json
//               BEFORE any delta it reports is trusted.
//   (default)   read one utterance per line from stdin, emit one TSV row per utterance:
//                   <utterance> \t <resultCount> \t <intent>:<score>:<slots> | ...
//
// The grammar below is a transcription of Tests~/Runtime/DemoGrammar.cs, in set order
// (weapons, navigation, common) with every set active — which is what
// VoxrCommandRecogniser.Configure(slots, sets) + SetActiveSets(all) hands the parser.
// Registration order is load-bearing: it is the final selection tie-break.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using VoXR.Commands;

namespace AbRig
{
    static class Program
    {
        static VoxrSlotDefinition[] Slots() =>
            new[]
            {
                new VoxrSlotDefinition(
                    "target",
                    new[] { "hotel one", "hotel two", "alpha one", "alpha three", "bravo two" }
                ),
                new VoxrSlotDefinition(
                    "weapon",
                    new[] { "missiles", "torpedoes", "jackal" },
                    aliases: new Dictionary<string, string> { { "jackals", "jackal" } }
                ),
                new VoxrSlotDefinition(
                    "quantity",
                    new[] { "all", "one", "two", "three" },
                    aliases: new Dictionary<string, string> { { "a", "one" } }
                ),
                new VoxrSlotDefinition(
                    "range",
                    new[] { "cqb", "safe range", "torpedo range", "pdc range", "railgun range" }
                ),
                VoxrSlotDefinition.NumberSequence("heading", minWords: 1, maxWords: 3),
                VoxrSlotDefinition.NumberSequence("elevation", minWords: 1, maxWords: 2),
            };

        static VoxrCommandDefinition[] Commands() =>
            new[]
            {
                // --- weapons set ---
                new VoxrCommandDefinition(
                    "launch_weapon",
                    new[]
                    {
                        new[] { "launch", "{?quantity}", "{weapon}", "target", "{target}" },
                        new[] { "fire", "{?quantity}", "{weapon}", "at", "{target}" },
                        new[] { "shoot", "{weapon}" },
                    }
                ),
                new VoxrCommandDefinition(
                    "cease_fire",
                    new[]
                    {
                        new[] { "cease", "fire" },
                        new[] { "stop", "firing" },
                        new[] { "disengage" },
                    }
                ),
                new VoxrCommandDefinition(
                    "resume_fire",
                    new[]
                    {
                        new[] { "resume", "fire" },
                        new[] { "resume", "firing" },
                        new[] { "reengage" },
                    }
                ),
                // --- navigation set ---
                new VoxrCommandDefinition(
                    "set_distance_named",
                    new[]
                    {
                        new[] { "close", "distance", "{range}", "target", "{target}" },
                        new[] { "set", "distance", "{range}", "target", "{target}" },
                        new[] { "make", "distance", "{range}", "target", "{target}" },
                        new[] { "open", "distance", "{range}", "target", "{target}" },
                    }
                ),
                new VoxrCommandDefinition(
                    "approach_target",
                    new[]
                    {
                        new[] { "close", "on", "target", "{target}" },
                        new[] { "close", "in", "on", "target", "{target}" },
                        new[] { "approach", "target", "{target}" },
                    }
                ),
                new VoxrCommandDefinition(
                    "retreat_from_target",
                    new[]
                    {
                        new[] { "fall", "back", "from", "target", "{target}" },
                        new[] { "pull", "back", "from", "target", "{target}" },
                        new[] { "get", "away", "from", "target", "{target}" },
                        new[] { "move", "away", "from", "target", "{target}" },
                        new[] { "open", "distance", "from", "target", "{target}" },
                    }
                ),
                new VoxrCommandDefinition(
                    "set_heading",
                    new[]
                    {
                        new[] { "orient", "heading", "{heading}" },
                        new[] { "orient", "heading", "{heading}", "?mark", "{?elevation}" },
                        new[] { "set", "heading", "{heading}" },
                    }
                ),
                // --- common set ---
                new VoxrCommandDefinition(
                    "mode_weapons",
                    new[] { new[] { "weapons", "mode" }, new[] { "switch", "to", "weapons" } }
                ),
                new VoxrCommandDefinition(
                    "mode_navigation",
                    new[] { new[] { "navigation", "mode" }, new[] { "switch", "to", "navigation" } }
                ),
                new VoxrCommandDefinition(
                    "mode_all",
                    new[] { new[] { "all", "modes" }, new[] { "enable", "all" } }
                ),
                new VoxrCommandDefinition(
                    "mode_disable",
                    new[] { new[] { "disable", "all" }, new[] { "disable", "commands" } }
                ),
            };

        // Mirrors VoxrCommandRecogniser.GetFollowUpGrammarWords() with no custom vocabularies
        // configured — the state the fixture corpus was captured under.
        static string[] FollowUpWords()
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            VoxrFollowUpVocabulary.AddPhraseWords(words, VoxrFollowUpVocabulary.DefaultConfirm);
            VoxrFollowUpVocabulary.AddPhraseWords(words, VoxrFollowUpVocabulary.DefaultCancel);
            var result = new string[words.Count];
            words.CopyTo(result);
            return result;
        }

        static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--grammar") >= 0)
            {
                Console.Out.Write(
                    VoxrCommandParser.GenerateGrammarJson(Slots(), Commands(), FollowUpWords())
                );
                return 0;
            }

            if (Array.IndexOf(args, "--check") >= 0)
            {
                Check.Run();
                return 0;
            }

            var parser = new VoxrCommandParser(Slots(), Commands());
            var sb = new StringBuilder();
            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                // A blank line is a real case (silence_negative), so it is replayed, not skipped.
                var results = parser.Parse(line);

                sb.Clear();
                sb.Append(line).Append('\t').Append(results.Length).Append('\t');
                for (int i = 0; i < results.Length; i++)
                {
                    if (i > 0)
                        sb.Append(" | ");
                    var cmd = results[i].Command;
                    sb.Append(cmd.Intent)
                        .Append(':')
                        .Append(cmd.Score.ToString("F6", CultureInfo.InvariantCulture))
                        .Append(':');
                    for (int s = 0; s < cmd.Slots.Length; s++)
                    {
                        if (s > 0)
                            sb.Append(',');
                        sb.Append(cmd.Slots[s].Name).Append('=').Append(cmd.Slots[s].Value);
                    }
                }
                Console.Out.WriteLine(sb.ToString());
            }
            return 0;
        }
    }
}
