// ============================================================================
// Purpose:  The bench's report: fast mode's header and per-case rows, and a paced run's section, and a conditioned run's header lines and sections, built into one deterministic markdown text, written and printed, the paced section under one condition
// Layer:    Planning.AbRig
// Owns:     BenchReport (internal static class), BenchReport.Header (internal sealed class), BenchReport.Case (internal sealed class), BenchReport.Paced (internal sealed class), BenchReport.Conditioned (internal sealed class)
// Depends:  BenchMetrics, Joined, JoinedReport, JoinedScorer
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AbRig
{
    static class BenchReport
    {
        internal sealed class Header
        {
            internal string Profile;
            internal string Mode;
            internal double Gain;
            internal string Libvosk;
            internal string ModelConf;
            internal string[] Grammar;
            internal string[] StagedFrom;
            internal float TickRate;
            internal KeyValuePair<string, string>[] Config;
            internal string Voice;
            internal string Provenance;
            internal int HarnessExit;
            internal long EndCases;
            internal long EndPassed;
            internal int Cases;
        }

        internal sealed class Case
        {
            internal string Name;
            internal string Category;
            internal string FirstPartial;
            internal string Finals;
            internal string CommandEvent;
            internal string Class;
            internal int FinalCount;
            internal int ExtraFires;
            internal string Pending;
            internal string Wer;
        }

        internal sealed class Paced
        {
            internal long Pace;
            internal string Condition;
            internal int MatchedExit;
            internal int PacedExit;
            internal List<(string Case, string Stage, string Audio, string Wall, string Compute)> Split =
                new List<(string Case, string Stage, string Audio, string Wall, string Compute)>();
            internal List<(
                string Case,
                string Factor,
                int Measured,
                int Merged,
                string LateMatched,
                string LatePaced
            )> Factors =
                new List<(string Case, string Factor, int Measured, int Merged, string LateMatched, string LatePaced)>();
            internal string Corpus;
            internal List<string> Finding = new List<string>();
        }

        internal sealed class Conditioned
        {
            internal string Seed;
            internal string Conditions;
            internal string Noise;
            internal string Room;
            internal string Filter;
            internal string Level;
            internal string GameAudio;
            internal bool Sir;
            internal int Exit;
            internal long EndCases;
            internal long EndPassed;
            internal List<(Case Case, string Source, string Condition)> Rows =
                new List<(Case Case, string Source, string Condition)>();
            internal List<(string Title, string[] Header, List<string[]> Rows)> Curves =
                new List<(string Title, string[] Header, List<string[]> Rows)>();
            internal List<(string Title, string[] Header, List<string[]> Rows)> Negatives =
                new List<(string Title, string[] Header, List<string[]> Rows)>();
        }

        internal static string Offset(long n, long d) =>
            ((double)n / d).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);

        static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

        internal static string Build(
            Header h,
            List<Case> cases,
            BenchMetrics.CommandScore command,
            BenchMetrics.Scored scored,
            Paced paced = null,
            Conditioned conditioned = null
        )
        {
            var sb = new StringBuilder();
            void Line(string text) => sb.Append(text).Append('\n');
            void Tables(List<(string Title, string[] Header, List<string[]> Rows)> tables)
            {
                foreach ((string Title, string[] Header, List<string[]> Rows) table in tables)
                {
                    Line("");
                    Line("### " + table.Title);
                    Line("");
                    Line("| " + string.Join(" | ", table.Header) + " |");
                    var rule = new StringBuilder("|");
                    for (int k = 0; k < table.Header.Length; k++)
                        rule.Append("---|");
                    Line(rule.ToString());
                    foreach (string[] row in table.Rows)
                        Line("| " + string.Join(" | ", row) + " |");
                }
            }

            Line(paced == null ? "# Bench report — fast replay" : "# Bench report — paced replay");
            Line("");
            Line(
                "**TTS-only, provisional:** it can stop a change, never ranks decoders, and proves no gain before R5; latency and accuracy stand side by side, never one score."
            );
            Line("");
            Line("## Configuration");
            Line("");
            Line(
                paced == null
                    ? "- Mode: fast — the harness's --dump run, 16384-sample pushes"
                    : "- Mode: paced — the harness's --dump run, 16384-sample pushes, then its --paced 16384 and --paced "
                        + Int(paced.Pace)
                        + " passes at real speed"
            );
            Line(
                h.Profile == null
                    ? "- Endpointer profile: Automatic, mode Continuous (confgen, as Command 3)"
                    : "- Endpointer profile: "
                        + h.Profile
                        + ", mode "
                        + h.Mode
                        + " (confgen, named configuration)"
            );
            Line("- Gain: " + h.Gain.ToString("R", CultureInfo.InvariantCulture));
            Line("- libvosk: " + h.Libvosk);
            Line(
                h.ModelConf == null
                    ? "- modelConf: not recorded"
                    : "- modelConf: sha256 " + JoinedReport.ConfSha12(h.ModelConf)
            );
            if (h.Profile != null)
                Line(
                    "- Harness pin: a copy of the committed pin, gain and modelConf replaced by this run's"
                );
            Line("- Grammar: `" + Joined.JsonArray(h.Grammar) + "`");
            var staged = new List<string>();
            foreach (string line in h.StagedFrom)
                staged.Add("`" + line + "`");
            Line("- Staged from: " + string.Join("; ", staged));
            var settings = new List<string>();
            foreach (KeyValuePair<string, string> field in h.Config)
                settings.Add(field.Key + " = " + field.Value);
            Line(
                "- Recogniser settings (tick rate "
                    + h.TickRate.ToString("R", CultureInfo.InvariantCulture)
                    + " Hz): "
                    + string.Join(", ", settings)
            );
            Line("- Voice: " + h.Voice);
            Line("- Provenance: " + h.Provenance);
            if (conditioned == null)
                Line("- Seed: none — clean renders, no condition applied");
            else
            {
                Line("- Seed: " + conditioned.Seed);
                Line("- Conditions: " + conditioned.Conditions);
                Line("- Noise: " + conditioned.Noise);
                Line("- Room response: " + conditioned.Room);
                Line("- Mic filter: " + conditioned.Filter);
                Line("- Level: " + conditioned.Level);
                Line("- game audio: " + conditioned.GameAudio);
                Line(
                    "- Labels: TTS-only, provisional; augmented numbers rank conditions, not field rates; digital mixing is a lower bound on difficulty, not a model of it"
                );
            }
            Line("- Harness exit: " + Int(h.HarnessExit));
            Line("- Dump end: cases " + Int(h.EndCases) + ", passed " + Int(h.EndPassed));
            Line(
                "- Self-check: " + Int(h.Cases) + "/" + Int(h.Cases) + " cases' finals equal the pin"
            );
            if (conditioned != null)
                Line(
                    "- Conditioned harness exit: "
                        + Int(conditioned.Exit)
                        + "; dump end: cases "
                        + Int(conditioned.EndCases)
                        + ", passed "
                        + Int(conditioned.EndPassed)
                );
            if (paced != null)
            {
                Line(
                    "- Paced harness exits: matched (pace 16384) "
                        + Int(paced.MatchedExit)
                        + ", 20 ms (pace "
                        + Int(paced.Pace)
                        + ") "
                        + Int(paced.PacedExit)
                );
                Line(
                    paced.Condition == null
                        ? "- Matched-chunk check: "
                            + Int(paced.Factors.Count)
                            + "/"
                            + Int(paced.Factors.Count)
                            + " cases' finals and first partials within 4096 samples of the fast dump's at pace 16384"
                        : "- Matched-chunk check: "
                            + Int(paced.Factors.Count)
                            + "/"
                            + Int(paced.Factors.Count)
                            + " conditioned clips' finals and first partials within 4096 samples of the conditioned fast dump's at pace 16384, condition "
                            + paced.Condition
                );
            }
            Line("");
            Line("## Latency");
            Line("");
            Line(
                "Offsets in s from the end of the case's last speech segment, signed; none: no such stamp; n/a: no speech."
            );
            Line("");
            Line("| Case | Category | First partial | Finals | Command event |");
            Line("|---|---|---|---|---|");
            foreach (Case c in cases)
                Line(
                    "| "
                        + c.Name
                        + " | "
                        + (c.Category.Length == 0 ? "-" : c.Category)
                        + " | "
                        + c.FirstPartial
                        + " | "
                        + c.Finals
                        + " | "
                        + c.CommandEvent
                        + " |"
                );
            Line("");
            Line("## Accuracy");
            Line("");
            Line("| Case | Class | Finals | Extra fires | Pending | WER |");
            Line("|---|---|---|---|---|---|");
            foreach (Case c in cases)
                Line(
                    "| "
                        + c.Name
                        + " | "
                        + c.Class
                        + " | "
                        + Int(c.FinalCount)
                        + " | "
                        + Int(c.ExtraFires)
                        + " | "
                        + c.Pending
                        + " | "
                        + c.Wer
                        + " |"
                );
            Line("");
            Line("| Total | Value |");
            Line("|---|---|");
            for (int k = 0; k < JoinedScorer.Classes.Length; k++)
                Line("| " + JoinedScorer.Classes[k] + " | " + Int(command.Counts[k]) + " |");
            Line(
                "| Intent accuracy (n=" + Int(command.Positives) + ") | " + command.Intent + " |"
            );
            Line("| Semantic accuracy | " + command.Semantic + " |");
            Line("| Phantom rate | " + command.Phantom + " |");
            Line(
                "| Corpus WER | "
                    + scored.Wer
                    + " (S="
                    + Int(scored.S)
                    + " D="
                    + Int(scored.D)
                    + " I="
                    + Int(scored.I)
                    + " N="
                    + Int(scored.N)
                    + ") |"
            );
            Line("| Stray words (silence case) | " + Int(scored.Stray) + " |");
            Line("| ECE (" + Int(BenchMetrics.EceBins) + " bins) | " + scored.Ece + " |");
            Line("| NCE | " + scored.Nce + " |");
            if (conditioned != null)
            {
                Line("");
                Line("## Conditioned latency");
                Line("");
                Line(
                    "Offsets in s from the end of the clip's last scaled speech segment, signed, per clip and condition, no aggregate; none: no such stamp; n/a: no speech."
                );
                Line("");
                Line("| Clip | Source | Category | Condition | First partial | Finals | Command event |");
                Line("|---|---|---|---|---|---|---|");
                foreach ((Case Case, string Source, string Condition) r in conditioned.Rows)
                    Line(
                        "| "
                            + r.Case.Name
                            + " | "
                            + r.Source
                            + " | "
                            + (r.Case.Category.Length == 0 ? "-" : r.Case.Category)
                            + " | "
                            + r.Condition
                            + " | "
                            + r.Case.FirstPartial
                            + " | "
                            + r.Case.Finals
                            + " | "
                            + r.Case.CommandEvent
                            + " |"
                    );
                Line("");
                Line("## Conditioned accuracy");
                Line("");
                Line("Per clip and condition; each condition's totals are its curve points.");
                Line("");
                Line("| Clip | Condition | Class | Finals | Extra fires | Pending | WER |");
                Line("|---|---|---|---|---|---|---|");
                foreach ((Case Case, string Source, string Condition) r in conditioned.Rows)
                    Line(
                        "| "
                            + r.Case.Name
                            + " | "
                            + r.Condition
                            + " | "
                            + r.Case.Class
                            + " | "
                            + Int(r.Case.FinalCount)
                            + " | "
                            + Int(r.Case.ExtraFires)
                            + " | "
                            + r.Case.Pending
                            + " | "
                            + r.Case.Wer
                            + " |"
                    );
                Line("");
                Line("## Curves");
                Line("");
                Line(
                    "One table per speed: a column per SNR step and the clean reference over the same cases, each cell with its n; no figure pooled across conditions."
                        + (
                            conditioned.Sir
                                ? " With game audio, one table more, SIR: a column per SIR step at speed 1.0 and the clean reference over the same cases."
                                : ""
                        )
                );
                Tables(conditioned.Curves);
                Line("");
                Line("## Negative set");
                Line("");
                Line(
                    "**Provisional, stand-in (adr-0007):** false fires, first and extra, per negative case and per hour of negative audio, apart from the positive classes and never netted; zero reads \"0 — set too easy\"."
                );
                Tables(conditioned.Negatives);
            }
            if (paced == null)
                return sb.ToString();
            Line("");
            Line(paced.Condition == null ? "## Paced" : "## Paced — condition " + paced.Condition);
            Line("");
            Line(
                "**Desktop-only, single run:** wall times, compute parts, the real-time factor and push lateness measure this machine in one run; their run-to-run spread is not measured, and they are never compared across machines."
            );
            if (paced.Condition != null)
            {
                Line("");
                Line(
                    "The paced passes replay condition "
                        + paced.Condition
                        + "'s "
                        + Int(paced.Factors.Count)
                        + " conditioned clips; the clean cases are not paced."
                );
            }
            Line("");
            Line("### Latency split");
            Line("");
            Line(
                "Offsets in s from the end of the case's last speech segment, signed: audio from the 20 ms pass's consumed samples, wall from its pops on the real-time schedule, compute = wall − audio; the command event's audio is fast mode's and its wall the wall view's 90 Hz frame, compute = paced − fast; none: no such stamp; n/a: no speech; unpaced: a flush result's wall."
            );
            Line("");
            Line("| Case | Stage | Audio | Wall | Compute |");
            Line("|---|---|---|---|---|");
            foreach (
                (string Case, string Stage, string Audio, string Wall, string Compute) s in paced.Split
            )
                Line(
                    "| "
                        + s.Case
                        + " | "
                        + s.Stage
                        + " | "
                        + s.Audio
                        + " | "
                        + s.Wall
                        + " | "
                        + s.Compute
                        + " |"
                );
            Line("");
            Line("### Real-time factor");
            Line("");
            Line(
                "Decode time ÷ audio duration: the recognition thread's per-read work, front end included, over the matched pass's measured reads on fast mode's read grid, desktop-only; the corpus factor is summed decode ÷ summed audio; lateness is a pass's largest push behind its real-time schedule."
            );
            Line("");
            Line(
                "| Case | Factor | Measured reads | Merged changes | Lateness, pace 16384 | Lateness, pace "
                    + Int(paced.Pace)
                    + " |"
            );
            Line("|---|---|---|---|---|---|");
            int measured = 0;
            int merged = 0;
            foreach (
                (
                    string Case,
                    string Factor,
                    int Measured,
                    int Merged,
                    string LateMatched,
                    string LatePaced
                ) f in paced.Factors
            )
            {
                measured += f.Measured;
                merged += f.Merged;
                Line(
                    "| "
                        + f.Case
                        + " | "
                        + f.Factor
                        + " | "
                        + Int(f.Measured)
                        + " | "
                        + Int(f.Merged)
                        + " | "
                        + f.LateMatched
                        + " | "
                        + f.LatePaced
                        + " |"
                );
            }
            Line(
                "| Corpus | "
                    + paced.Corpus
                    + " | "
                    + Int(measured)
                    + " | "
                    + Int(merged)
                    + " | - | - |"
            );
            Line("");
            Line("### 20 ms finding");
            Line("");
            Line(
                "Fast mode's non-empty finals and command class against the 20 ms pass's, its class from the wall view; a finding, never a check."
            );
            Line("");
            if (paced.Finding.Count == 0)
                Line("- none");
            foreach (string line in paced.Finding)
                Line("- " + line);
            return sb.ToString();
        }

        internal static void Write(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Console.Out.Write(text);
        }
    }
}
