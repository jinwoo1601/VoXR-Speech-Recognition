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

            // --sweep: the exhaustive round-1 start-index sweep behind the "blocked / recovered"
            // figure in scoring.md and KNOWN_LIMITATIONS.md. Dispatched here rather than via
            // -p:StartupObject=, which does not survive an incremental rebuild.
            if (Array.IndexOf(args, "--sweep") >= 0)
            {
                Sweep.Run();
                return 0;
            }

            // --doccheck: verify every published number in the scoring docs against the real
            // parser. Dispatched here for the same reason as --sweep.
            if (Array.IndexOf(args, "--doccheck") >= 0)
                return DocCheck.Run();

            // --bench: time and allocation per Parse over the utterances on stdin. Added for
            // issue #65 §5.2, whose requirements §5 promises the feature "reports against that
            // baseline" (~7.7 us/utterance, 344.4 bytes/utterance, measured for item 1) and
            // which nothing in the build otherwise measures.
            //
            // Reports the SAME work on both sides of the A/B, so the absolute numbers matter
            // less than the delta: same grammar, same utterance list, same iteration count.
            if (Array.IndexOf(args, "--bench") >= 0)
            {
                var benchParser = new VoxrCommandParser(Slots(), Commands(), VoxrCommandParser.DefaultCoverageWeight, FollowUpWords());
                var lines = new List<string>();
                string benchLine;
                while ((benchLine = Console.In.ReadLine()) != null)
                    lines.Add(benchLine);

                const int Warmup = 200;
                const int Reps = 2000;

                for (int r = 0; r < Warmup; r++)
                    foreach (var l in lines)
                        benchParser.Parse(l);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int r = 0; r < Reps; r++)
                    foreach (var l in lines)
                        benchParser.Parse(l);
                sw.Stop();
                long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

                long parses = (long)Reps * lines.Count;
                double usEach = sw.Elapsed.TotalMilliseconds * 1000.0 / parses;
                double bytesEach = (bytesAfter - bytesBefore) / (double)parses;

                Console.Out.WriteLine(
                    $"parses={parses}\tus_per_utterance={usEach.ToString("F3", CultureInfo.InvariantCulture)}"
                        + $"\tbytes_per_utterance={bytesEach.ToString("F1", CultureInfo.InvariantCulture)}"
                );
                return 0;
            }

            // --verdicts: one buffer per line, emit the speculative eager verdict alongside
            // what the subsequent flush would actually fire. Added for issue #65 §5.2 Phase 4,
            // which has to measure the class of utterances whose eager verdict MOVES — the
            // trailing coverage term reorders the eager scan's winner, so design §5.4's
            // "cannot destabilise the gate, structurally" is false and the real extent has to
            // be measured rather than argued.
            // --prefix-verdicts: the same eager scan, but over every GROWING PREFIX of each
            // utterance rather than the finished line. Added for issue #82's review, which
            // pointed out that --verdicts samples only the final buffer state — and the final
            // buffer is exactly the state TryEagerCommit does NOT run on. A coverage change
            // that only ever raises scores could carry a best candidate over minScore mid-word,
            // so the class has to be measured on prefixes or not claimed at all.
            //
            // One row per (utterance, prefix length), so a diff between two revisions names the
            // exact buffer whose verdict moved.
            if (Array.IndexOf(args, "--prefix-verdicts") >= 0)
            {
                var pre = new VoxrCommandParser(Slots(), Commands(), VoxrCommandParser.DefaultCoverageWeight, FollowUpWords());
                var psb = new StringBuilder();
                string pline;
                while ((pline = Console.In.ReadLine()) != null)
                {
                    if (pline.Length == 0)
                        continue;
                    var all = pline.Split(' ');
                    for (int n = 1; n <= all.Length; n++)
                    {
                        var prefix = new string[n];
                        Array.Copy(all, prefix, n);
                        var v = pre.TryEagerCommit(prefix, null, 0.6f, 0.4f);
                        psb.Clear();
                        psb.Append(string.Join(" ", prefix)).Append('\t').Append(v);
                        Console.Out.WriteLine(psb.ToString());
                    }
                }
                return 0;
            }

            if (Array.IndexOf(args, "--verdicts") >= 0)
            {
                var eager = new VoxrCommandParser(Slots(), Commands(), VoxrCommandParser.DefaultCoverageWeight, FollowUpWords());
                var vsb = new StringBuilder();
                string buffer;
                while ((buffer = Console.In.ReadLine()) != null)
                {
                    var toks = buffer.Length == 0 ? Array.Empty<string>() : buffer.Split(' ');
                    var verdict = eager.TryEagerCommit(toks, null, 0.6f, 0.4f);
                    var flushed = eager.Parse(buffer);

                    vsb.Clear();
                    vsb.Append(buffer).Append('\t').Append(verdict).Append('\t');
                    if (flushed.Length == 0)
                    {
                        vsb.Append("(none)");
                    }
                    else
                    {
                        var top = flushed[0].Command;
                        vsb.Append(top.Intent)
                            .Append('#')
                            .Append(top.MatchedPatternIndex)
                            .Append(':')
                            .Append(top.Score.ToString("F6", CultureInfo.InvariantCulture));
                    }
                    Console.Out.WriteLine(vsb.ToString());
                }
                return 0;
            }

            var parser = new VoxrCommandParser(Slots(), Commands(), VoxrCommandParser.DefaultCoverageWeight, FollowUpWords());
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
