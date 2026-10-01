// `abrig --check` — replays the exact fixture grammars and utterances used by the new tests
// in Tests~/Runtime/, at whichever revision was staged.
//
// This does NOT replace the Unity run; it is a cheap cross-check on the hand-derived numbers
// so an arithmetic slip is caught before a multi-minute EditMode+PlayMode cycle rather than
// after one. Run it at both revisions: every row's `before` must be what Phase 1 predicts the
// red failure message will say, and every `after` must be the value the test asserts.
using System;
using System.Globalization;
using VoXR.Commands;

namespace AbRig
{
    static class Check
    {
        static void Row(string label, VoxrCommandParser parser, string utterance)
        {
            var results = parser.Parse(utterance);
            Console.Out.Write($"{label,-46} n={results.Length}  ");
            for (int i = 0; i < results.Length; i++)
            {
                if (i > 0)
                    Console.Out.Write(" | ");
                var c = results[i].Command;
                Console.Out.Write(
                    $"{c.Intent}={c.Score.ToString("F4", CultureInfo.InvariantCulture)}"
                );
                foreach (var s in c.Slots)
                    Console.Out.Write($" [{s.Name}={s.Value}]");
            }
            Console.Out.WriteLine();
        }

        static VoxrSlotDefinition[] Burn() =>
            new[] { new VoxrSlotDefinition("burn_level", new[] { "coast", "hard burn" }) };

        internal static void Run()
        {
            var missedLiteral = new VoxrCommandParser(
                Burn(),
                new[]
                {
                    new VoxrCommandDefinition(
                        "time_to_target",
                        new[] { new[] { "time", "to", "target" } }
                    ),
                    new VoxrCommandDefinition(
                        "decelerate_by",
                        new[] { new[] { "decelerate", "by", "{burn_level}" } }
                    ),
                    new VoxrCommandDefinition("cease_fire", new[] { new[] { "cease", "fire" } }),
                }
            );
            Row("F1 3-element  'time target'", missedLiteral, "time target");
            Row("F1 slot kept  'decelerate hard burn'", missedLiteral, "decelerate hard burn");
            Row("F3 2-element  'fire'", missedLiteral, "fire");

            var longPattern = new VoxrCommandParser(
                new[]
                {
                    new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                    new VoxrSlotDefinition("target", new[] { "hotel one", "alpha three" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[]
                        {
                            new[]
                            {
                                "launch",
                                "{weapon}",
                                "target",
                                "{target}",
                                "on",
                                "my",
                                "mark",
                            },
                        }
                    ),
                }
            );
            Row("F1 7-element", longPattern, "launch missiles hotel one on my mark");

            var boundary = new VoxrCommandParser(
                Burn(),
                new[]
                {
                    new VoxrCommandDefinition(
                        "set_burn",
                        new[] { new[] { "set", "burn", "to", "{burn_level}", "now" } }
                    ),
                }
            );
            Row("F5 boundary 3/5", boundary, "set hard burn now");

            // F10 — the widened slot-miss hole. The eager refusal itself is asserted in the
            // Unity test; what this row cross-checks is the score that makes the refusal
            // load-bearing rather than incidental.
            var widened = new VoxrCommandParser(
                new[]
                {
                    new VoxrSlotDefinition("quantity", new[] { "all", "one", "two", "three" }),
                    new VoxrSlotDefinition("weapon", new[] { "missiles", "torpedoes" }),
                    new VoxrSlotDefinition("target", new[] { "hotel one", "alpha three" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[]
                        {
                            new[]
                            {
                                "launch",
                                "{quantity}",
                                "{weapon}",
                                "target",
                                "{target}",
                                "on",
                                "my",
                                "mark",
                            },
                        }
                    ),
                }
            );
            Row("F10 8-element slot miss", widened, "launch missiles hotel one on my mark");
            Console.Out.WriteLine(
                "F9 eager verdict: "
                    + new VoxrCommandParser(
                        new VoxrSlotDefinition[0],
                        new[]
                        {
                            new VoxrCommandDefinition(
                                "time_to_target",
                                new[] { new[] { "time", "to", "target" } }
                            ),
                        }
                    ).TryEagerCommit(new[] { "time", "target" }, null, 0.6f, 0.4f)
            );
            Console.Out.WriteLine(
                "F10 eager verdict: "
                    + widened.TryEagerCommit(
                        "launch missiles hotel one on my mark".Split(' '),
                        null,
                        0.6f,
                        0.4f
                    )
            );

            // --- The two pre-existing tests the zero-crossing flips (Amendment A1 ruling 2) ---
            var numeric = new VoxrCommandParser(
                new[]
                {
                    new VoxrSlotDefinition(
                        "target",
                        new[] { "hotel one", "hotel two", "bravo two" }
                    ),
                    VoxrSlotDefinition.NumberSequence("heading", minWords: 1, maxWords: 3),
                    VoxrSlotDefinition.NumberSequence("elevation", minWords: 1, maxWords: 2),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "set_heading",
                        new[]
                        {
                            new[] { "orient", "to", "heading", "{heading}" },
                            new[] { "orient", "to", "heading", "{heading}", "mark", "{?elevation}" },
                        }
                    ),
                    new VoxrCommandDefinition(
                        "close_distance",
                        new[]
                        {
                            new[]
                            {
                                "close",
                                "distance",
                                "{heading}",
                                "klicks",
                                "target",
                                "{target}",
                            },
                        }
                    ),
                }
            );
            Row("FLIP RespectsMaxWords", numeric, "orient to heading five mark one two three");

            var hazard = new VoxrCommandParser(
                Burn(),
                new[]
                {
                    new VoxrCommandDefinition("decelerate", new[] { new[] { "decelerate" } }),
                    new VoxrCommandDefinition(
                        "decelerate_by",
                        new[] { new[] { "decelerate", "by", "{burn_level}" } }
                    ),
                }
            );
            Row("FLIP HazardSplit", hazard, "decelerate hard burn");

            // The Phase 4B finding, pinned as a test: dropped discriminator -> first sibling.
            var siblings = new VoxrCommandParser(
                new VoxrSlotDefinition[0],
                new[]
                {
                    new VoxrCommandDefinition(
                        "mode_weapons",
                        new[] { new[] { "switch", "to", "weapons" } }
                    ),
                    new VoxrCommandDefinition(
                        "mode_navigation",
                        new[] { new[] { "switch", "to", "navigation" } }
                    ),
                }
            );
            Row("PIN dropped discriminator 'switch to'", siblings, "switch to");

            // --- DR-7 admission pins ---
            var preempt = new VoxrCommandParser(
                new[] { new VoxrSlotDefinition("target", new[] { "alpha one", "hotel one" }) },
                new[]
                {
                    new VoxrCommandDefinition(
                        "approach_target",
                        new[] { new[] { "approach", "target", "{target}" } }
                    ),
                    new VoxrCommandDefinition("mode_weapons", new[] { new[] { "weapons", "mode" } }),
                }
            );
            Row("DR7 M1 preempt 'alpha one weapons mode'", preempt, "alpha one weapons mode");

            var evict = new VoxrCommandParser(
                Burn(),
                new[]
                {
                    new VoxrCommandDefinition(
                        "set_burn",
                        new[] { new[] { "set", "burn", "to", "{burn_level}" } }
                    ),
                    new VoxrCommandDefinition("fire", new[] { new[] { "fire" } }),
                }
            );
            Row("DR7 M2 evict", evict, "hard burn set burn to coast fire");

            var partial = new VoxrCommandParser(
                new[] { new VoxrSlotDefinition("target", new[] { "alpha one", "hotel one" }) },
                new[]
                {
                    new VoxrCommandDefinition(
                        "approach_target",
                        new[] { new[] { "close", "in", "on", "target", "{target}" } }
                    ),
                }
            );
            Row("DR7 M3 partial 'close in'", partial, "close in");

            // --- Coverage additions from the PR #72 review ---
            var eagerDr7 = new VoxrCommandParser(
                new VoxrSlotDefinition[0],
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[] { new[] { "launch", "missiles", "target", "hotel", "mark" } }
                    ),
                }
            );
            Row("COV eager 'launch mark'", eagerDr7, "launch mark");
            Console.Out.WriteLine(
                "COV eager verdict @0.4: "
                    + eagerDr7.TryEagerCommit(new[] { "launch", "mark" }, null, 0.4f, 0.4f)
            );

            var optFor = new VoxrCommandParser(
                new[]
                {
                    new VoxrSlotDefinition("q1", new[] { "one", "two" }),
                    new VoxrSlotDefinition("q2", new[] { "one", "two" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "alpha_cmd",
                        new[] { new[] { "alpha", "{?q1}", "{?q2}", "bravo", "charlie" } }
                    ),
                }
            );
            Row("COV opt-matched-not-FOR 'alpha one two'", optFor, "alpha one two");

            var optAgainst = new VoxrCommandParser(
                new[]
                {
                    new VoxrSlotDefinition("quantity", new[] { "all", "one" }),
                    new VoxrSlotDefinition("weapon", new[] { "missiles" }),
                    new VoxrSlotDefinition("target", new[] { "hotel one" }),
                },
                new[]
                {
                    new VoxrCommandDefinition(
                        "launch_weapon",
                        new[]
                        {
                            new[] { "launch", "{?quantity}", "{weapon}", "target", "{target}" },
                        }
                    ),
                }
            );
            Row("COV opt-omitted-not-AGAINST 'launch missiles'", optAgainst, "launch missiles");

            var pend = new VoxrCommandParser(
                Burn(),
                new[]
                {
                    new VoxrCommandDefinition(
                        "set_burn",
                        new[]
                        {
                            new[] { "set", "the", "burn", "level", "to", "{burn_level}", "now" },
                        }
                    ),
                }
            );
            Row("COV pending ARMS 'set the burn now'", pend, "set the burn now");
            Row("COV pending REFUSED 'set burn now'", pend, "set burn now");
        }
    }
}
