// Verifies every numeric claim the rewritten scoring docs make (issue #83) against the
// REAL parser sources staged from the worktree. Each case names the doc location it pins.
//
// Candidate-level scores (the losing candidates a worked example walks through) are read
// by invoking TryMatchScored directly — the staged sources compile into this assembly, so
// the private member is reachable by reflection without any InternalsVisibleTo.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using VoXR.Commands;

namespace AbRig
{
    static class DocCheck
    {
        static int failures;
        static int checks;

        static readonly MethodInfo TryMatchScored = typeof(VoxrCommandParser).GetMethod(
            "TryMatchScored",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        static readonly MethodInfo BuildTables = typeof(VoxrCommandParser).GetMethod(
            "BuildCoverageTables",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        static string[] Tok(string s) => s.Split(' ');

        // Score of one (pattern, startIdx) candidate, with coverage, exactly as selection sees it.
        static float Candidate(
            VoxrCommandParser p,
            string utterance,
            int startIdx,
            string[] pattern,
            int searchStart = 0
        )
        {
            var tokens = Tok(utterance);
            BuildTables.Invoke(p, new object[] { tokens });
            // Built positionally from the live parameter list rather than a fixed 4-tuple:
            // issue #82 added a trailing `forStartProbe` parameter, and Invoke refuses a short
            // argument array even when the missing parameter has a default. Type.Missing fills
            // whatever optional tail the method carries, so this survives the next one too.
            var parameters = TryMatchScored.GetParameters();
            var argv = new object[parameters.Length];
            argv[0] = tokens;
            argv[1] = startIdx;
            argv[2] = pattern;
            argv[3] = searchStart;
            for (int i = 4; i < argv.Length; i++)
                argv[i] = Type.Missing;

            object mr = TryMatchScored.Invoke(p, argv);
            return (float)mr.GetType().GetField("Score").GetValue(mr);
        }

        static void Expect(
            string where,
            string what,
            float actual,
            float expected,
            float tol = 0.005f
        )
        {
            checks++;
            bool ok = Math.Abs(actual - expected) <= tol;
            if (!ok)
                failures++;
            Console.WriteLine(
                $"{(ok ? "ok  " : "FAIL")}  {where, -28} {what, -52} got {actual.ToString("F3", CultureInfo.InvariantCulture)}  want {expected.ToString("F3", CultureInfo.InvariantCulture)}"
            );
        }

        static void ExpectText(string where, string what, string actual, string expected)
        {
            checks++;
            bool ok = actual == expected;
            if (!ok)
                failures++;
            Console.WriteLine(
                $"{(ok ? "ok  " : "FAIL")}  {where, -28} {what, -52} got {actual}  want {expected}"
            );
        }

        // Flatten Parse output to "intent:score[slot=value,...]|..." for whole-utterance claims.
        static string ParseSummary(VoxrCommandParser p, string utterance)
        {
            var parts = new List<string>();
            foreach (var r in p.Parse(utterance))
            {
                if (!r.IsMatch)
                    continue;
                var slots = new List<string>();
                foreach (var s in r.Command.Slots)
                    slots.Add($"{s.Name}={s.Value}");
                parts.Add(
                    $"{r.Command.Intent}:{r.Command.Score.ToString("F3", CultureInfo.InvariantCulture)}"
                        + (slots.Count > 0 ? "[" + string.Join(",", slots) + "]" : "")
                );
            }
            return parts.Count == 0 ? "(none)" : string.Join(" | ", parts);
        }

        static VoxrSlotDefinition Slot(string name, params string[] values) =>
            new VoxrSlotDefinition(name, values);

        static VoxrCommandDefinition Cmd(string intent, params string[][] patterns) =>
            new VoxrCommandDefinition(intent, patterns);

        public static int Run()
        {
            // ---- scoring.md §7 A — a clean multi-slot command -------------------------
            {
                // Built with the lab's demo-grammar registered set (issue #161, Amendment A5):
                // this block's start-2 claim is a resolver claim — a registered {weapon} missed
                // costs nothing (DR-8).
                var p = new VoxrCommandParser(
                    new[] { Slot("weapon", "missiles"), Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd("launch_weapon", new[] { "launch", "{weapon}", "target", "{target}" }),
                    },
                    registeredSlots: new RegisteredSlotNames(
                        new[] { "weapon", "target", "range", "heading" }
                    )
                );
                var pat = new[] { "launch", "{weapon}", "target", "{target}" };
                const string u = "launch missiles target hotel one";
                Expect(
                    "scoring.md §7 A",
                    "start 0 (all four match)",
                    Candidate(p, u, 0, pat),
                    1.00f
                );
                Expect(
                    "scoring.md §7 A",
                    "start 1 (misses launch, skips it)",
                    Candidate(p, u, 1, pat),
                    0.60f
                );
                Expect(
                    "scoring.md §7 A",
                    "start 2 (misses launch+weapon)",
                    Candidate(p, u, 2, pat),
                    0.400f
                );
                ExpectText(
                    "scoring.md §7 A",
                    "winner",
                    ParseSummary(p, u),
                    "launch_weapon:1.000[weapon=missiles,target=hotel one]"
                );
            }

            // ---- scoring.md §7 B / §2 table — coverage inverts the #42 pair ------------
            {
                var slots = new[] { Slot("burn_level", "hard burn") };
                var bare = new[] { "decelerate" };
                var filled = new[] { "decelerate", "by", "{burn_level}" };
                var p = new VoxrCommandParser(slots, new[] { Cmd("decelerate", bare, filled) });
                const string u = "decelerate hard burn";
                Expect(
                    "scoring.md §7 B",
                    "bare pattern, 2 orphans",
                    Candidate(p, u, 0, bare),
                    0.333f
                );
                Expect(
                    "scoring.md §7 B",
                    "slot-filled, 'by' dropped",
                    Candidate(p, u, 0, filled),
                    0.667f
                );
                ExpectText(
                    "scoring.md §7 B",
                    "winner fires WITH its argument",
                    ParseSummary(p, u),
                    "decelerate:0.667[burn_level=hard burn]"
                );

                // §2 — [unk] is transparent, not a run terminator.
                Expect(
                    "scoring.md §2 unk",
                    "'decelerate [unk] hard burn' bare",
                    Candidate(p, "decelerate [unk] hard burn", 0, bare),
                    0.333f
                );

                // §7 B — the ?by remedy.
                var optional = new[] { "decelerate", "?by", "{burn_level}" };
                var pOpt = new VoxrCommandParser(
                    slots,
                    new[] { Cmd("decelerate", bare, optional) }
                );
                Expect(
                    "scoring.md §7 B",
                    "?by form scores 2/2",
                    Candidate(pOpt, u, 0, optional),
                    1.00f
                );
                ExpectText(
                    "scoring.md §7 B",
                    "?by winner",
                    ParseSummary(pOpt, u),
                    "decelerate:1.000[burn_level=hard burn]"
                );
                ExpectText(
                    "scoring.md §7 B",
                    "bare utterance still matches bare",
                    ParseSummary(pOpt, "decelerate"),
                    "decelerate:1.000"
                );

                // §7 B2 — the same demotion with no sibling to land on.
                var pBare = new VoxrCommandParser(slots, new[] { Cmd("decelerate", bare) });
                Expect(
                    "scoring.md §7 B2",
                    "lone bare pattern",
                    Candidate(pBare, u, 0, bare),
                    0.333f
                );
                ExpectText(
                    "scoring.md §7 B2",
                    "still the round's winner (gate rejects later)",
                    ParseSummary(pBare, u),
                    "decelerate:0.333"
                );
            }

            // ---- scoring.md §3 — the intercept-track pair is now settled on score ------
            {
                var slots = new[] { Slot("track", "hotel one"), Slot("burn_level", "hard burn") };
                var bare = new[] { "intercept", "track", "{track}" };
                var tailed = new[] { "intercept", "track", "{track}", "{burn_level}" };
                var p = new VoxrCommandParser(slots, new[] { Cmd("intercept", bare, tailed) });
                const string u = "intercept track hotel one hard burn";
                Expect(
                    "scoring.md §3",
                    "bare leaves 'hard burn' unexplained",
                    Candidate(p, u, 0, bare),
                    0.60f
                );
                Expect(
                    "scoring.md §3",
                    "tailed consumes everything",
                    Candidate(p, u, 0, tailed),
                    1.00f
                );
                ExpectText(
                    "scoring.md §3",
                    "one command, both slots",
                    ParseSummary(p, u),
                    "intercept:1.000[track=hotel one,burn_level=hard burn]"
                );
            }

            // ---- scoring.md §6 — the fire/fire-at verdict table ------------------------
            {
                var slots = new[] { Slot("target", "hotel one") };
                var bare = new[] { "fire" };
                var full = new[] { "fire", "at", "{target}" };
                // The demo-grammar registered set, as §7 A above: the 'fire at' row is a resolver
                // claim — a registered {target} missed costs nothing (DR-8).
                var p = new VoxrCommandParser(
                    slots,
                    new[] { Cmd("fire", bare, full) },
                    registeredSlots: new RegisteredSlotNames(
                        new[] { "weapon", "target", "range", "heading" }
                    )
                );
                Expect(
                    "scoring.md §6",
                    "buffer 'fire' — bare",
                    Candidate(p, "fire", 0, bare),
                    1.00f
                );
                Expect(
                    "scoring.md §6",
                    "buffer 'fire at' — bare",
                    Candidate(p, "fire at", 0, bare),
                    0.50f
                );
                Expect(
                    "scoring.md §6",
                    "buffer 'fire at' — fire at {target}",
                    Candidate(p, "fire at", 0, full),
                    1.00f
                );
                Expect(
                    "scoring.md §6",
                    "buffer 'fire at hotel one' — full",
                    Candidate(p, "fire at hotel one", 0, full),
                    1.00f
                );
            }

            // ---- scoring.md §6 cond.5 — 'switch to' scores 0.67 on both siblings -------
            {
                var p = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        Cmd("mode_weapons", new[] { "switch", "to", "weapons" }),
                        Cmd("mode_navigation", new[] { "switch", "to", "navigation" }),
                    }
                );
                Expect(
                    "scoring.md §6 cond.5",
                    "'switch to' vs weapons",
                    Candidate(p, "switch to", 0, new[] { "switch", "to", "weapons" }),
                    0.667f
                );
                Expect(
                    "scoring.md §6 cond.5",
                    "'switch to' vs navigation",
                    Candidate(p, "switch to", 0, new[] { "switch", "to", "navigation" }),
                    0.667f
                );
            }

            // ---- scoring.md §2 — the A3 exception: matching MORE must not lose ---------
            {
                var p = new VoxrCommandParser(
                    new[] { Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd("mode_weapons", new[] { "switch", "to", "weapons" }),
                        Cmd("mode_navigation", new[] { "switch", "to", "navigation" }),
                        Cmd("weapons_mode", new[] { "weapons", "mode" }),
                        Cmd("approach_target", new[] { "approach", "target", "{target}" }),
                    }
                );
                const string u = "switch to weapons target hotel";
                // Asserted as literals, not compared to each other: a relational check would
                // pass on any pair of numbers and could not falsify the published table.
                Expect(
                    "scoring.md §2 A3",
                    "the pattern that MATCHED its final element",
                    Candidate(p, u, 0, new[] { "switch", "to", "weapons" }),
                    0.60f
                );
                Expect(
                    "scoring.md §2 A3",
                    "the one that missed, charged for what it mis-predicted",
                    Candidate(p, u, 0, new[] { "switch", "to", "navigation" }),
                    0.333f
                );
                ExpectText(
                    "scoring.md §2 A3",
                    "the right command wins",
                    ParseSummary(p, u).Split(' ')[0],
                    "mode_weapons:0.600"
                );
            }

            // ---- scoring.md §7 B, troubleshooting.md — the residue coverage does NOT close --
            // Registering a pattern that starts on the stranded value's first word terminates
            // the bare candidate's orphan run, so it is charged nothing and strands the value
            // exactly as before #65 — at the DEFAULT weight. Found by the PR #84 review.
            {
                var slots = new[] { Slot("burn_level", "hard burn") };
                var pair = Cmd(
                    "decelerate",
                    new[] { "decelerate" },
                    new[] { "decelerate", "by", "{burn_level}" }
                );
                ExpectText(
                    "scoring.md §7 B",
                    "control: nothing starts on 'hard'",
                    ParseSummary(
                        new VoxrCommandParser(slots, new[] { pair }),
                        "decelerate hard burn"
                    ),
                    "decelerate:0.667[burn_level=hard burn]"
                );
                var withHardStop = new VoxrCommandParser(
                    slots,
                    new[] { pair, Cmd("hard_stop", new[] { "hard", "stop" }) }
                );
                ExpectText(
                    "scoring.md §7 B",
                    "residue: argument discarded at default weight",
                    ParseSummary(withHardStop, "decelerate hard burn"),
                    "decelerate:1.000 | hard_stop:0.333"
                );
                ExpectText(
                    "scoring.md §7 B",
                    "?by fixes the residue too",
                    ParseSummary(
                        new VoxrCommandParser(
                            slots,
                            new[]
                            {
                                Cmd(
                                    "decelerate",
                                    new[] { "decelerate" },
                                    new[] { "decelerate", "?by", "{burn_level}" }
                                ),
                                Cmd("hard_stop", new[] { "hard", "stop" }),
                            }
                        ),
                        "decelerate hard burn"
                    ),
                    "decelerate:1.000[burn_level=hard burn]"
                );
            }

            // ---- scoring.md §6 cond.3 — trailing coverage DOES reach the eager verdict -----
            // Not directly: it decides which candidate selection hands to the conditions.
            // Found by the PR #84 review; the page previously claimed it never could.
            {
                var slots = new[] { Slot("burn_level", "hard burn") };
                var pair = Cmd(
                    "decelerate",
                    new[] { "decelerate" },
                    new[] { "decelerate", "by", "{burn_level}" }
                );
                var eager = typeof(VoxrCommandParser).GetMethod(
                    "TryEagerCommit",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                string Verdict(float w) =>
                    eager
                        .Invoke(
                            new VoxrCommandParser(slots, new[] { pair }, w),
                            new object[] { "decelerate hard burn".Split(' '), null, 0.6f, 0.4f }
                        )
                        .ToString();
                ExpectText(
                    "scoring.md §6 cond.3",
                    "coverageWeight 1.0 -> commits",
                    Verdict(1.0f),
                    "Commit"
                );
                ExpectText(
                    "scoring.md §6 cond.3",
                    "coverageWeight 0.0 -> None",
                    Verdict(0.0f),
                    "None"
                );
            }

            // ---- scoring.md §2/§4 — the orphan amnesty keeps multi-command intact ------
            {
                var p = new VoxrCommandParser(
                    new[] { Slot("weapon", "missiles"), Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd("cease_fire", new[] { "cease", "fire" }),
                        Cmd("launch_weapon", new[] { "launch", "{weapon}", "target", "{target}" }),
                    }
                );
                ExpectText(
                    "scoring.md §2/§4",
                    "both commands at a full 1.00",
                    ParseSummary(p, "cease fire launch missiles target hotel one"),
                    "cease_fire:1.000 | launch_weapon:1.000[weapon=missiles,target=hotel one]"
                );
            }

            // ---- scoring.md §7 D — second command loses its leading word ---------------
            {
                var p = new VoxrCommandParser(
                    new[] { Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd("cease_fire", new[] { "cease", "fire" }),
                        Cmd("approach_target", new[] { "approach", "target", "{target}" }),
                    }
                );
                ExpectText(
                    "scoring.md §7 D",
                    "step 1 unchanged: the orphan run still terminates, cease_fire still 1.00",
                    ParseSummary(p, "cease fire target hotel one"),
                    "cease_fire:1.000"
                );
                ExpectText(
                    "scoring.md §7 D",
                    "contrast trace: second command said in full, both at 1.00",
                    ParseSummary(p, "cease fire approach target hotel one"),
                    "cease_fire:1.000 | approach_target:1.000[target=hotel one]"
                );
            }

            // ---- #124 leading-required-miss bar — every number item 2 publishes -------
            {
                // scoring.md §5 "the bar": the reported #124 shape fires ONE command.
                var p = new VoxrCommandParser(
                    new[] { Slot("track", "one two four four") },
                    new[]
                    {
                        Cmd("query_time_to_target", new[] { "time", "to", "target" }),
                        Cmd("intercept_target", new[] { "intercept", "track", "{track}" }),
                    }
                );
                ExpectText(
                    "scoring.md §5 bar",
                    "the reported #124 utterance now fires one command, not two",
                    ParseSummary(p, "time to target track one two four four"),
                    "query_time_to_target:1.000"
                );

                // ...and the mitigation: let a legitimate pattern claim the tail.
                // command-recognition.md publishes 1.00, degrading to 0.80 when 'track' drops.
                var pClaim = new VoxrCommandParser(
                    new[] { Slot("track", "one two four four") },
                    new[]
                    {
                        Cmd(
                            "query_time_to_target",
                            new[] { "time", "to", "target" },
                            new[] { "time", "to", "target", "track", "{track}" }
                        ),
                        Cmd("intercept_target", new[] { "intercept", "track", "{track}" }),
                    }
                );
                ExpectText(
                    "command-recognition.md tail",
                    "claiming the tail makes it one command at 1.00",
                    ParseSummary(pClaim, "time to target track one two four four"),
                    "query_time_to_target:1.000[track=one two four four]"
                );
                ExpectText(
                    "command-recognition.md tail",
                    "degrades to 0.80 when the decoder drops 'track'",
                    ParseSummary(pClaim, "time to target one two four four"),
                    "query_time_to_target:0.800[track=one two four four]"
                );
            }

            {
                // scoring.md §5 bar: the leading-debris shape. The barred candidate still
                // consumes, which is why cease_fire keeps a clean 1.00 rather than 0.40.
                var p = new VoxrCommandParser(
                    new[] { Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd("cease_fire", new[] { "cease", "fire" }),
                        Cmd("approach_target", new[] { "approach", "target", "{target}" }),
                    }
                );
                ExpectText(
                    "scoring.md §5 bar",
                    "leading debris is absorbed by the barred round; cease_fire stays 1.00",
                    ParseSummary(p, "target hotel one cease fire"),
                    "cease_fire:1.000"
                );

                // scoring.md §1 :74 — lengthening does not recover a LEADING miss.
                // The candidate scores 0.667 (above the default gate) and still cannot fire.
                var p3 = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[] { Cmd("cease_fire", new[] { "cease", "fire", "now" }) }
                );
                Expect(
                    "scoring.md §1 :74",
                    "'fire now' scores 2/3 as a candidate",
                    Candidate(p3, "fire now", 0, new[] { "cease", "fire", "now" }),
                    0.667f
                );
                ExpectText(
                    "scoring.md §1 :74",
                    "...and is barred anyway, so length does not rescue a leading miss",
                    ParseSummary(p3, "fire now"),
                    "(none)"
                );
            }

            {
                // KNOWN_LIMITATIONS "sibling warning is silent below the default minScore":
                // the REPLACEMENT repro. The discriminator must be non-leading or the bar
                // refuses both candidates and the tie is never live.
                var p = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        Cmd("cease_fire", new[] { "cease", "fire" }),
                        Cmd("cease_burn", new[] { "cease", "burn" }),
                    }
                );
                Expect(
                    "KNOWN_LIMITATIONS repro",
                    "non-leading discriminator: 'cease' ties both at 0.5",
                    Candidate(p, "cease", 0, new[] { "cease", "fire" }),
                    0.5f
                );
                Expect(
                    "KNOWN_LIMITATIONS repro",
                    "...and the rival scores identically, so the tie is live",
                    Candidate(p, "cease", 0, new[] { "cease", "burn" }),
                    0.5f
                );
                ExpectText(
                    "KNOWN_LIMITATIONS repro",
                    "the tie IS live below the gate — first-registered fires, unlike the old repro",
                    ParseSummary(p, "cease"),
                    "cease_fire:0.500"
                );

                // The repro this REPLACED must now produce nothing, which is why it changed.
                var pOld = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        Cmd("cease_fire", new[] { "cease", "fire" }),
                        Cmd("resume_fire", new[] { "resume", "fire" }),
                    }
                );
                ExpectText(
                    "KNOWN_LIMITATIONS repro",
                    "the superseded repro no longer reproduces: both barred, nothing fires",
                    ParseSummary(pOld, "fire"),
                    "(none)"
                );
            }

            {
                // KNOWN_LIMITATIONS "first word dropped is silent rather than recovered":
                // the entry's own repro, and the 0.667 it publishes.
                var p = new VoxrCommandParser(
                    new[] { Slot("heading", "two seven zero") },
                    new[] { Cmd("set_heading", new[] { "set", "heading", "{heading}" }) }
                );
                Expect(
                    "KNOWN_LIMITATIONS bar cost",
                    "'heading two seven zero' scores 2/3 as a candidate",
                    Candidate(p, "heading two seven zero", 0, new[] { "set", "heading", "{heading}" }),
                    0.667f
                );
                ExpectText(
                    "KNOWN_LIMITATIONS bar cost",
                    "...clears the default gate and still fires nothing",
                    ParseSummary(p, "heading two seven zero"),
                    "(none)"
                );
            }

            // ---- #124 P1 reversal — the branch's headline NEW ADVICE ------------------
            // command-recognition.md remedy 2 and KNOWN_LIMITATIONS' "what now works":
            // a LEADING discriminator bars both siblings at any pattern length, so the
            // outcome is silence rather than the wrong command. Nothing else pins this,
            // and it is the claim authors will actually restructure grammars on.
            {
                var pShort = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        Cmd("mode_weapons", new[] { "weapons", "mode" }),
                        Cmd("mode_navigation", new[] { "navigation", "mode" }),
                    }
                );
                Expect(
                    "command-recognition.md rem2",
                    "two-element leading pair still ties at (0+1)/2",
                    Candidate(pShort, "mode", 0, new[] { "weapons", "mode" }),
                    0.5f
                );
                ExpectText(
                    "command-recognition.md rem2",
                    "...and nothing fires on the tie",
                    ParseSummary(pShort, "mode"),
                    "(none)"
                );

                var pLong = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        Cmd("mode_weapons", new[] { "weapons", "mode", "active" }),
                        Cmd("mode_navigation", new[] { "navigation", "mode", "active" }),
                    }
                );
                Expect(
                    "command-recognition.md rem2",
                    "grown to three elements the pair ties at 0.67, above the gate",
                    Candidate(pLong, "mode active", 0, new[] { "weapons", "mode", "active" }),
                    0.667f
                );
                ExpectText(
                    "command-recognition.md rem2",
                    "...and STILL nothing fires — length does not bring the tie back",
                    ParseSummary(pLong, "mode active"),
                    "(none)"
                );
            }

            // ---- KNOWN_LIMITATIONS / §2 — trailing filler on a non-[unk] path ----------
            {
                var p = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[] { Cmd("cease_fire", new[] { "cease", "fire" }) }
                );
                ExpectText(
                    "scoring.md §2",
                    "'cease fire please' charged as real text",
                    ParseSummary(p, "cease fire please"),
                    "cease_fire:0.667"
                );
                ExpectText(
                    "scoring.md §2",
                    "'cease fire [unk]' free through the decoder",
                    ParseSummary(p, "cease fire [unk]"),
                    "cease_fire:1.000"
                );
            }

            // ---- command-recognition.md — the Coverage table ---------------------------
            {
                var p = new VoxrCommandParser(
                    new[] { Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd("cease_fire", new[] { "disengage" }),
                        Cmd("approach_target", new[] { "approach", "target", "{target}" }),
                    }
                );
                ExpectText(
                    "command-recognition.md",
                    "'disengage'",
                    ParseSummary(p, "disengage"),
                    "cease_fire:1.000"
                );
                ExpectText(
                    "command-recognition.md",
                    "'target disengage' (leading side)",
                    ParseSummary(p, "target disengage"),
                    "cease_fire:0.500"
                );
                ExpectText(
                    "command-recognition.md",
                    "'disengage target' (trailing side)",
                    ParseSummary(p, "disengage target"),
                    "cease_fire:0.500"
                );
            }

            // ---- command-recognition.md — 5-element form past one skipped word ---------
            {
                var p = new VoxrCommandParser(
                    new[]
                    {
                        Slot("quantity", "all"),
                        Slot("weapon", "missiles"),
                        Slot("target", "hotel one"),
                    },
                    new[]
                    {
                        Cmd(
                            "launch_weapon",
                            new[] { "launch", "{?quantity}", "{weapon}", "target", "{target}" }
                        ),
                    }
                );
                ExpectText(
                    "command-recognition.md",
                    "'launch launch all missiles target hotel one'",
                    ParseSummary(p, "launch launch all missiles target hotel one"),
                    "launch_weapon:0.833[quantity=all,weapon=missiles,target=hotel one]"
                );
            }

            // ---- scoring.md §1 — the fragility table is unaffected by coverage ---------
            {
                var p = new VoxrCommandParser(
                    new[] { Slot("burn_level", "hard burn") },
                    new[] { Cmd("decelerate", new[] { "decelerate", "by", "{burn_level}" }) }
                );
                ExpectText(
                    "scoring.md §1",
                    "3-element, one literal dropped",
                    ParseSummary(p, "decelerate hard burn"),
                    "decelerate:0.667[burn_level=hard burn]"
                );

                var p7 = new VoxrCommandParser(
                    new[] { Slot("weapon", "missiles"), Slot("target", "hotel one") },
                    new[]
                    {
                        Cmd(
                            "launch_weapon",
                            new[] { "launch", "{weapon}", "target", "{target}", "on", "my", "mark" }
                        ),
                    }
                );
                ExpectText(
                    "scoring.md §1",
                    "7-element, one literal dropped",
                    ParseSummary(p7, "launch missiles hotel one on my mark"),
                    "launch_weapon:0.857[weapon=missiles,target=hotel one]"
                );

                var p2 = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[] { Cmd("cease_fire", new[] { "cease", "fire" }) }
                );
                ExpectText(
                    "scoring.md §1",
                    "2-element floor: barred, not merely under the gate",
                    ParseSummary(p2, "fire"),
                    "(none)"
                );
            }

            // ---- scoring.md §2 — coverageWeight = 0 restores the pre-#65 selection -----
            {
                var slots = new[] { Slot("burn_level", "hard burn") };
                var bare = new[] { "decelerate" };
                var filled = new[] { "decelerate", "by", "{burn_level}" };
                var p0 = new VoxrCommandParser(
                    slots,
                    new[] { Cmd("decelerate", bare, filled) },
                    0f
                );
                ExpectText(
                    "scoring.md §2 weight 0",
                    "bare wins again, argument discarded",
                    ParseSummary(p0, "decelerate hard burn"),
                    "decelerate:1.000"
                );

                foreach (
                    var (label, w) in new[]
                    {
                        ("negative", -1f),
                        ("NaN", float.NaN),
                        ("infinity", float.PositiveInfinity),
                    }
                )
                {
                    var pw = new VoxrCommandParser(
                        slots,
                        new[] { Cmd("decelerate", bare, filled) },
                        w
                    );
                    ExpectText(
                        "scoring.md §2 weight",
                        $"{label} clamps to 0",
                        ParseSummary(pw, "decelerate hard burn"),
                        "decelerate:1.000"
                    );
                }
            }

            // ---- scoring.md §2 — follow-up vocabulary terminates an orphan run ---------
            {
                var cmds = new[] { Cmd("cease_fire", new[] { "disengage" }) };
                var noFollowUp = new VoxrCommandParser(Array.Empty<VoxrSlotDefinition>(), cmds);
                ExpectText(
                    "scoring.md §2 follow-up",
                    "without the word list, 'yes' is an orphan",
                    ParseSummary(noFollowUp, "disengage yes"),
                    "cease_fire:0.500"
                );
                var withFollowUp = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    cmds,
                    1.0f,
                    new[] { "yes", "no" }
                );
                ExpectText(
                    "scoring.md §2 follow-up",
                    "with it, 'disengage yes' is not charged",
                    ParseSummary(withFollowUp, "disengage yes"),
                    "cease_fire:1.000"
                );
            }

            // ---- scoring.md §2 — a LEADING optional literal is a pattern start too -------
            // Confirmed by the PR #84 review: the start-set walk continues past optionals,
            // so "?please" puts BOTH "please" and "fire" into the set, and a stray "please"
            // terminates the orphan run for every candidate in the grammar.
            {
                var noOptional = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[] { Cmd("cease_fire", new[] { "disengage" }), Cmd("fire", new[] { "fire" }) }
                );
                ExpectText(
                    "scoring.md §2 leading-opt",
                    "without a leading optional, 'please' is an orphan",
                    ParseSummary(noOptional, "disengage please now"),
                    "cease_fire:0.333"
                );
                var withOptional = new VoxrCommandParser(
                    Array.Empty<VoxrSlotDefinition>(),
                    new[]
                    {
                        Cmd("cease_fire", new[] { "disengage" }),
                        Cmd("fire", new[] { "?please", "fire" }),
                    }
                );
                ExpectText(
                    "scoring.md §2 leading-opt",
                    "with it, the run terminates at 'please' grammar-wide",
                    ParseSummary(withOptional, "disengage please now"),
                    "cease_fire:1.000"
                );
            }

            Console.WriteLine();
            Console.WriteLine(
                $"{checks - failures}/{checks} doc claims verified against the real parser."
            );
            return failures == 0 ? 0 : 1;
        }

        // No Main: reached through Program's --doccheck flag. -p:StartupObject= does not
        // survive an incremental rebuild, which yields false greens.
    }
}
