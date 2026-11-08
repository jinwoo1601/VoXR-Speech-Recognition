// ============================================================================
// Purpose:  The bench's fast-run report: the header and per-case rows built into one deterministic markdown text, written and printed
// Layer:    Planning.AbRig
// Owns:     BenchReport (internal static class), BenchReport.Header (internal sealed class), BenchReport.Case (internal sealed class)
// Depends:  BenchMetrics, JoinedReport, JoinedScorer
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AbRig
{
    static class BenchReport
    {
        static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        internal sealed class Header
        {
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

        internal static string Offset(long n, long d) =>
            ((double)n / d).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);

        static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

        internal static string Build(
            Header h,
            List<Case> cases,
            BenchMetrics.CommandScore command,
            BenchMetrics.Scored scored
        )
        {
            var sb = new StringBuilder();
            void Line(string text) => sb.Append(text).Append('\n');

            Line("# Bench report — fast replay");
            Line("");
            Line(
                "**TTS-only, provisional:** it can stop a change, never ranks decoders, and proves no gain before R5; latency and accuracy stand side by side, never one score."
            );
            Line("");
            Line("## Configuration");
            Line("");
            Line("- Mode: fast — the harness's --dump run, 16384-sample pushes");
            Line("- Endpointer profile: Automatic, mode Continuous (confgen, as Command 3)");
            Line("- Gain: " + h.Gain.ToString("R", CultureInfo.InvariantCulture));
            Line("- libvosk: " + h.Libvosk);
            Line(
                h.ModelConf == null
                    ? "- modelConf: not recorded"
                    : "- modelConf: sha256 " + JoinedReport.ConfSha12(h.ModelConf)
            );
            Line("- Grammar: `" + JsonArray(h.Grammar) + "`");
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
            Line("- Seed: none — clean renders, no condition applied");
            Line("- Harness exit: " + Int(h.HarnessExit));
            Line("- Dump end: cases " + Int(h.EndCases) + ", passed " + Int(h.EndPassed));
            Line(
                "- Self-check: " + Int(h.Cases) + "/" + Int(h.Cases) + " cases' finals equal the pin"
            );
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
            return sb.ToString();
        }

        internal static void Write(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Console.Out.Write(text);
        }

        internal static string JsonArray(string[] values)
        {
            var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartArray();
                foreach (string v in values)
                    w.WriteStringValue(v);
                w.WriteEndArray();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
