// Player-configuration behavioural checks (issue #74 item 3).
//
// This rig is the ONLY build in the project that compiles without UNITY_EDITOR. Unity always
// defines it, so every EditMode and PlayMode test runs on the Editor side of any `#if
// UNITY_EDITOR` fork — which makes a whole class of bug invisible to the 500-test suite: a
// runtime path gated on a symbol every test defines. This design has now hit that class twice
// (item 2's _siblingSets player break; item 3's EnsureSiblingLookup gating, which would have
// shipped a feature that worked in the Editor and did nothing in a game).
//
// So the claims that fork on UNITY_EDITOR are asserted HERE, where the player side is the side
// that runs. Invoke with `-p:StartupObject=AbRig.PlayerCheck`; exit 0 means every check passed.
using System;
using VoXR.Commands;

namespace AbRig
{
    static class PlayerCheck
    {
        static int _failures;

        static void Check(bool condition, string what)
        {
            Console.WriteLine((condition ? "  ok   " : "  FAIL ") + what);
            if (!condition)
                _failures++;
        }

        static VoxrSlotDefinition[] Slots() =>
            new[] { new VoxrSlotDefinition("ship", new[] { "alpha" }) };

        static VoxrCommandDefinition[] Siblings(params string[] discriminators)
        {
            var commands = new VoxrCommandDefinition[discriminators.Length];
            for (int i = 0; i < discriminators.Length; i++)
                commands[i] = new VoxrCommandDefinition(
                    "set_" + discriminators[i],
                    new[] { new[] { "set", "{ship}", discriminators[i], "on" } }
                );
            return commands;
        }

        static int Main()
        {
            Console.WriteLine("Player-configuration checks (no UNITY_EDITOR defined):");

            // The flag genuinely gates recording in a player. If EnsureSiblingLookup were
            // Editor-gated again this is the only check in the project that would notice.
            //
            // It does NOT cover the recogniser's disambiguateSiblingTies -> recordSiblingTies
            // plumbing: stage.sh does not stage VoxrCommandRecogniser.cs, so that hop is never
            // compiled here. It carries no #if UNITY_EDITOR fork, so the Unity suites do cover
            // it — but this file is not what would catch it breaking.
            var off = new VoxrCommandParser(Slots(), Siblings("mode", "level"));
            var offResults = off.Parse("set alpha on", null);
            Check(
                off.TiedSiblingBuffer == null,
                "flag off allocates no rival buffers at all (DR-7: the opt-in costs a flag-off "
                    + "player nothing, per rebuild as well as per parse)"
            );
            Check(
                offResults.Length == 1 && offResults[0].Command.Intent == "set_mode",
                "…and the flush still fires the first-registered sibling, exactly as before"
            );

            var on = new VoxrCommandParser(
                Slots(),
                Siblings("mode", "level"),
                recordSiblingTies: true
            );
            on.Parse("set alpha on", null);
            Check(
                on.TiedSiblingBuffer[0].RivalCount == 1,
                "flag on DOES record the rival in a player build — the feature is not inert"
            );
            Check(
                on.TiedSiblingBuffer[0].WinnerValue == "mode"
                    && on.TiedSiblingRivalAt(0, 0).Value == "level",
                "…and carries both discriminating words, so a question can be phrased"
            );

            // A parse that fills the result buffer must not let the next extraction round index
            // the rival buffers past their end. _resultBuf.Length is max(commands.Length, 1), so
            // three rounds against two commands is the smallest case.
            try
            {
                var r = on.Parse("set alpha on set alpha on set alpha on", null);
                Check(r.Length == 2, "more extraction rounds than results does not overrun");
            }
            catch (Exception e)
            {
                Check(false, "more extraction rounds than results threw " + e.GetType().Name);
            }

            // n-ary recording, and the cap reporting its own overflow rather than truncating in
            // silence (requirements F19).
            var three = new VoxrCommandParser(
                Slots(),
                Siblings("mode", "level", "standby"),
                recordSiblingTies: true
            );
            three.Parse("set alpha on", null);
            Check(
                three.TiedSiblingBuffer[0].RivalCount == 2
                    && !three.TiedSiblingBuffer[0].Truncated,
                "a three-way set records both rivals and reports no truncation"
            );

            var over = new VoxrCommandParser(
                Slots(),
                Siblings("mode", "level", "standby", "trim", "gain", "bias"),
                recordSiblingTies: true
            );
            over.Parse("set alpha on", null);
            Check(
                over.TiedSiblingBuffer[0].RivalCount == VoxrCommandParser.MaxDisambiguationRivals
                    && over.TiedSiblingBuffer[0].Truncated,
                "a set past the cap offers the first N and says so"
            );

            Console.WriteLine(
                _failures == 0 ? "all player-configuration checks passed" : $"{_failures} FAILED"
            );
            return _failures == 0 ? 0 : 1;
        }
    }
}
