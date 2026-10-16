// ============================================================================
// Purpose:  The joined mode's report: the two sides' scored files read strictly, compared, written to report.md and printed
// Layer:    Planning.AbRig
// Owns:     JoinedReport (internal static class), JoinedReport.Side (internal sealed class)
// Depends:  JoinedInput, JoinedScorer
// ============================================================================
// feat-joined-rig-parser Phase 3 (*Report*; F4, F6-F8):
//
//   --joined report --a <scored-a> --b <scored-b> --out <report.md>
//
// Both scored files are read whole and strictly before anything is written: every line exactly
// the keys `score` writes, the files sides a and b, agreeing on `vary`, manifest, provenance,
// gate, `evaluate` (and the dump under `vary` parser), `pinned` as `vary` implies, and case
// names in order. Any defect is a stop, exit 2, naming the file and line or the difference,
// and leaves no report. Counts sit side by side, never totalled or netted. A recogniser-layer
// file (feat-joined-rig-recogniser Phase 3) has `layer`, `config` and `tickRate` for `gate`
// and per scored case the timing keys; both files of one layer and one tick rate. A
// recogniser-layer report under `vary` decoder (endpointer-profile Phase 3, *Kill criteria in
// the report*) ends in `## Sweep measures`: the buffer setup and W, finals per utterance, S,
// KC1 and KC2; there a config lacking `bufferWindow` or `eagerFlushOnCompleteMatch`, or the
// two sides' configs differing in either, is a stop, exit 2.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbRig
{
    static class JoinedReport
    {
        const string Usage = "usage: --joined report --a A --b B --out R";

        static readonly string[] ValueOptions = { "--a", "--b", "--out" };

        // The keys `score` writes, in its order: the header, then a case line by disposition.
        static readonly string[] HeaderKeys =
        {
            "kind",
            "side",
            "vary",
            "stagedFrom",
            "dump",
            "dumpHeader",
            "manifest",
            "provenance",
            "pinned",
            "gate",
            "evaluate",
        };
        static readonly string[] SealedKeys = { "kind", "case", "disposition" };
        static readonly string[] UnscoredKeys = { "kind", "case", "disposition", "finals" };
        static readonly string[] ScoredKeys =
        {
            "kind",
            "case",
            "disposition",
            "finals",
            "class",
            "intent",
            "slots",
            "score",
            "slotCredit",
            "extraFires",
            "finalCount",
        };

        // The recogniser layer's: `gate` becomes `layer config tickRate`; a case is `sealed` or
        // `scored`, scored with the timing keys.
        static readonly string[] RecogniserHeaderKeys =
        {
            "kind",
            "side",
            "vary",
            "stagedFrom",
            "dump",
            "dumpHeader",
            "manifest",
            "provenance",
            "pinned",
            "layer",
            "config",
            "tickRate",
            "evaluate",
        };
        static readonly string[] RecogniserScoredKeys =
        {
            "kind",
            "case",
            "disposition",
            "finals",
            "class",
            "intent",
            "slots",
            "score",
            "slotCredit",
            "extraFires",
            "finalCount",
            "fireTimes",
            "delay",
            "pending",
        };

        // What a flip compares (*Report*): transcripts, disposition, class, intent, slots,
        // credit, extra fires.
        static readonly string[] FlipKeys =
        {
            "finals",
            "disposition",
            "class",
            "intent",
            "slots",
            "slotCredit",
            "extraFires",
        };

        // The counts rows, A and B side by side; no total.
        static readonly string[] CountLabels =
        {
            "correct",
            "of which correct (no fire)",
            "wrong-slots",
            "wrong-intent",
            "missed",
            "false-fire",
            "extra fires",
            "not scored at this layer",
            "sealed-skipped",
        };
        const int NotScoredRow = 7;

        internal sealed class Side
        {
            internal string Path;
            internal JsonElement Header;
            internal readonly List<JsonElement> Cases = new List<JsonElement>();
        }

        internal static int Run(string[] args)
        {
            Dictionary<string, string> o = ParseOptions(args);
            string outPath = o["--out"];

            // No stale report survives a stop after the options parse; then both files are read
            // before anything else.
            try
            {
                if (File.Exists(outPath))
                    File.Delete(outPath);
            }
            catch (Exception e)
            {
                throw new JoinedInput.Stop(2, outPath + ": " + e.Message);
            }
            Side a = Read(o["--a"], "a");
            Side b = Read(o["--b"], "b");
            Compare(a, b);

            string text = Render(a, b);
            try
            {
                File.WriteAllBytes(outPath, new UTF8Encoding(false).GetBytes(text));
            }
            catch (Exception e)
            {
                throw new JoinedInput.Stop(2, outPath + ": " + e.Message);
            }
            Console.Out.Write(text);
            return 0;
        }

        static Dictionary<string, string> ParseOptions(string[] args)
        {
            int at = Array.IndexOf(args, "--joined");
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                if (i == at || i == at + 1)
                    continue;
                string a = args[i];
                if (Array.IndexOf(ValueOptions, a) < 0)
                    throw new JoinedInput.Stop(2, "unknown argument \"" + a + "\"", Usage);
                if (options.ContainsKey(a))
                    throw new JoinedInput.Stop(2, a + " given twice", Usage);
                if (i + 1 >= args.Length || i + 1 == at)
                    throw new JoinedInput.Stop(2, a + " needs a value", Usage);
                options.Add(a, args[++i]);
            }
            foreach (string option in ValueOptions)
                if (!options.ContainsKey(option))
                    throw new JoinedInput.Stop(2, "missing " + option, Usage);
            return options;
        }

        // --- reading ----------------------------------------------------------------------

        static Side Read(string path, string side)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, JoinedInput.StrictUtf8);
            }
            catch (Exception e)
            {
                throw new JoinedInput.Stop(2, path + ": " + e.Message);
            }
            string[] lines = JoinedInput.SplitLines(text);
            if (lines.Length == 0)
                throw new JoinedInput.Stop(2, path + ": empty scored file");

            var s = new Side { Path = path };
            for (int i = 0; i < lines.Length; i++)
            {
                string where = path + ":" + (i + 1);
                if (lines[i].IndexOf('\r') >= 0)
                    throw new JoinedInput.Stop(
                        2,
                        where + ": CR in the line (scored files are LF only)"
                    );
                JsonElement rec = JoinedInput.ParseObject(lines[i], where);
                if (i == 0)
                {
                    CheckHeader(rec, side, where);
                    s.Header = rec;
                }
                else
                {
                    CheckCase(rec, Layered(s), where);
                    s.Cases.Add(rec);
                }
            }
            return s;
        }

        static void CheckHeader(JsonElement rec, string side, string where)
        {
            bool layered = rec.TryGetProperty("layer", out _);
            Keys(rec, layered ? RecogniserHeaderKeys : HeaderKeys, where);
            Expect(rec, "kind", IsString(rec, "kind", "header"), where);
            if (rec.GetProperty("side").ValueKind != JsonValueKind.String)
                Expect(rec, "side", false, where);
            string actual = rec.GetProperty("side").GetString();
            if (actual != side)
                throw new JoinedInput.Stop(
                    2,
                    where + ": `side` \"" + actual + "\", expected \"" + side + "\""
                );
            Expect(
                rec,
                "vary",
                IsString(rec, "vary", "parser") || IsString(rec, "vary", "decoder"),
                where
            );
            Expect(rec, "stagedFrom", IsStringArray(rec.GetProperty("stagedFrom")), where);
            Expect(rec, "dump", Is(rec, "dump", JsonValueKind.String), where);
            Expect(rec, "dumpHeader", Is(rec, "dumpHeader", JsonValueKind.Object), where);
            Expect(rec, "manifest", Is(rec, "manifest", JsonValueKind.String), where);
            Expect(rec, "provenance", Is(rec, "provenance", JsonValueKind.String), where);
            Expect(rec, "pinned", IsBool(rec, "pinned"), where);
            if (layered)
            {
                Expect(rec, "layer", IsString(rec, "layer", "recogniser"), where);
                Expect(rec, "config", IsStringObject(rec.GetProperty("config")), where);
                Expect(rec, "tickRate", Is(rec, "tickRate", JsonValueKind.Number), where);
            }
            else
                Expect(rec, "gate", Is(rec, "gate", JsonValueKind.Object), where);
            Expect(rec, "evaluate", IsBool(rec, "evaluate"), where);
        }

        static void CheckCase(JsonElement rec, bool layered, string where)
        {
            string disposition =
                rec.TryGetProperty("disposition", out JsonElement d)
                && d.ValueKind == JsonValueKind.String
                    ? d.GetString()
                    : null;
            string[] keys =
                disposition == "scored" ? (layered ? RecogniserScoredKeys : ScoredKeys)
                : disposition == "recogniser-layer" && !layered ? UnscoredKeys
                : disposition == "sealed" ? SealedKeys
                : null;
            if (keys == null)
                throw new JoinedInput.Stop(
                    2,
                    where
                        + (
                            layered
                                ? ": missing or mistyped key `disposition` (scored, sealed)"
                                : ": missing or mistyped key `disposition` (scored, recogniser-layer, sealed)"
                        )
                );
            Keys(rec, keys, where);
            Expect(rec, "kind", IsString(rec, "kind", "case"), where);
            Expect(rec, "case", Is(rec, "case", JsonValueKind.String), where);
            if (disposition == "sealed")
                return;
            Expect(rec, "finals", IsStringArray(rec.GetProperty("finals")), where);
            if (disposition != "scored")
                return;

            Expect(
                rec,
                "class",
                Is(rec, "class", JsonValueKind.String)
                    && Array.IndexOf(JoinedScorer.Classes, rec.GetProperty("class").GetString())
                        >= 0,
                where
            );
            Expect(
                rec,
                "intent",
                Is(rec, "intent", JsonValueKind.String) || Is(rec, "intent", JsonValueKind.Null),
                where
            );
            Expect(rec, "slots", IsSlots(rec.GetProperty("slots")), where);
            Expect(
                rec,
                "score",
                Is(rec, "score", JsonValueKind.Number) || Is(rec, "score", JsonValueKind.Null),
                where
            );
            Expect(
                rec,
                "slotCredit",
                IsString(rec, "slotCredit", "n/a") || Is(rec, "slotCredit", JsonValueKind.Number),
                where
            );
            Expect(rec, "extraFires", IsCount(rec.GetProperty("extraFires")), where);
            Expect(rec, "finalCount", IsCount(rec.GetProperty("finalCount")), where);
            if (!layered)
                return;
            Expect(rec, "fireTimes", IsNumberArray(rec.GetProperty("fireTimes")), where);
            Expect(
                rec,
                "delay",
                Is(rec, "delay", JsonValueKind.Number) || Is(rec, "delay", JsonValueKind.Null),
                where
            );
            Expect(rec, "pending", IsBool(rec, "pending"), where);
        }

        // The line's keys are exactly `keys`, in order.
        static void Keys(JsonElement rec, string[] keys, string where)
        {
            var actual = new List<string>();
            foreach (JsonProperty p in rec.EnumerateObject())
                actual.Add(p.Name);
            bool equal = actual.Count == keys.Length;
            for (int i = 0; equal && i < keys.Length; i++)
                equal = string.Equals(actual[i], keys[i], StringComparison.Ordinal);
            if (!equal)
                throw new JoinedInput.Stop(
                    2,
                    where
                        + ": keys "
                        + string.Join(", ", actual)
                        + "; `score` writes "
                        + string.Join(", ", keys)
                );
        }

        static void Expect(JsonElement rec, string key, bool ok, string where)
        {
            if (!ok)
                throw new JoinedInput.Stop(
                    2,
                    where + ": mistyped key `" + key + "`: " + rec.GetProperty(key).GetRawText()
                );
        }

        static bool Is(JsonElement rec, string key, JsonValueKind kind) =>
            rec.GetProperty(key).ValueKind == kind;

        static bool IsString(JsonElement rec, string key, string value) =>
            Is(rec, key, JsonValueKind.String) && rec.GetProperty(key).GetString() == value;

        static bool IsBool(JsonElement rec, string key) =>
            Is(rec, key, JsonValueKind.True) || Is(rec, key, JsonValueKind.False);

        static bool IsCount(JsonElement v) =>
            v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= 0;

        static bool IsStringArray(JsonElement v)
        {
            if (v.ValueKind != JsonValueKind.Array)
                return false;
            foreach (JsonElement e in v.EnumerateArray())
                if (e.ValueKind != JsonValueKind.String)
                    return false;
            return true;
        }

        static bool IsNumberArray(JsonElement v)
        {
            if (v.ValueKind != JsonValueKind.Array)
                return false;
            foreach (JsonElement e in v.EnumerateArray())
                if (e.ValueKind != JsonValueKind.Number)
                    return false;
            return true;
        }

        // `config`: an object whose every value is a string.
        static bool IsStringObject(JsonElement v)
        {
            if (v.ValueKind != JsonValueKind.Object)
                return false;
            foreach (JsonProperty p in v.EnumerateObject())
                if (p.Value.ValueKind != JsonValueKind.String)
                    return false;
            return true;
        }

        // `slots`: [{"name","value"}], each exactly two strings.
        static bool IsSlots(JsonElement v)
        {
            if (v.ValueKind != JsonValueKind.Array)
                return false;
            foreach (JsonElement e in v.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                    return false;
                int keys = 0;
                foreach (JsonProperty p in e.EnumerateObject())
                {
                    keys++;
                    if (keys > 2 || p.Name != (keys == 1 ? "name" : "value"))
                        return false;
                    if (p.Value.ValueKind != JsonValueKind.String)
                        return false;
                }
                if (keys != 2)
                    return false;
            }
            return true;
        }

        // --- the two sides agree ----------------------------------------------------------

        static void Compare(Side a, Side b)
        {
            // Both files of one layer, and at the recogniser layer of one tick rate; there no
            // `gate` to compare, and `config` may differ (it is the ref's).
            bool layered = Layered(a);
            if (layered != Layered(b))
                throw new JoinedInput.Stop(
                    2,
                    "`layer` differs: "
                        + a.Path
                        + " "
                        + (Raw(a.Header, "layer") ?? "(absent)")
                        + ", "
                        + b.Path
                        + " "
                        + (Raw(b.Header, "layer") ?? "(absent)")
                );
            if (layered)
            {
                string ta = a.Header.GetProperty("tickRate").GetRawText();
                string tb = b.Header.GetProperty("tickRate").GetRawText();
                if (!string.Equals(ta, tb, StringComparison.Ordinal))
                    throw new JoinedInput.Stop(
                        2,
                        "`tickRate` differs: " + a.Path + " " + ta + ", " + b.Path + " " + tb
                    );
            }

            // `vary` first: under `parser` both sides scored one dump (F3), so it joins the keys.
            var keys = new List<string> { "vary", "manifest", "provenance", "gate", "evaluate" };
            if (layered)
                keys.Remove("gate");
            bool parser = a.Header.GetProperty("vary").GetString() == "parser";
            if (parser)
            {
                keys.Add("dump");
                keys.Add("dumpHeader");
            }
            foreach (string key in keys)
            {
                string ra = a.Header.GetProperty(key).GetRawText();
                string rb = b.Header.GetProperty(key).GetRawText();
                if (!string.Equals(ra, rb, StringComparison.Ordinal))
                    throw new JoinedInput.Stop(
                        2,
                        "`" + key + "` differs: " + a.Path + " " + ra + ", " + b.Path + " " + rb
                    );
            }
            // `pinned` as `vary` implies: both sides under `parser`; A only under `decoder`.
            ExpectPinned(a, true, parser);
            ExpectPinned(b, parser, parser);
            int n = Math.Max(a.Cases.Count, b.Cases.Count);
            for (int i = 0; i < n; i++)
            {
                string ca = i < a.Cases.Count ? CaseName(a.Cases[i]) : null;
                string cb = i < b.Cases.Count ? CaseName(b.Cases[i]) : null;
                if (!string.Equals(ca, cb, StringComparison.Ordinal))
                    throw new JoinedInput.Stop(
                        2,
                        "case names differ at case "
                            + (i + 1)
                            + ": "
                            + a.Path
                            + " "
                            + (ca == null ? "(none)" : "\"" + ca + "\"")
                            + ", "
                            + b.Path
                            + " "
                            + (cb == null ? "(none)" : "\"" + cb + "\"")
                    );
            }
        }

        static void ExpectPinned(Side s, bool expected, bool parser)
        {
            bool actual = Bool(s.Header, "pinned");
            if (actual != expected)
                throw new JoinedInput.Stop(
                    2,
                    s.Path
                        + ": `pinned` "
                        + (actual ? "true" : "false")
                        + ", expected "
                        + (expected ? "true" : "false")
                        + " under `vary` \""
                        + (parser ? "parser" : "decoder")
                        + "\""
                );
        }

        // --- rendering --------------------------------------------------------------------

        static string Render(Side a, Side b)
        {
            var sb = new StringBuilder();
            string vary = a.Header.GetProperty("vary").GetString();
            bool layered = Layered(a);

            // 1. Header: corpus, provenance, the side varied and both identities, kill check, gate
            // (at the recogniser layer: the layer, tick rate, configurations, the one-read note).
            sb.Append("# Joined report\n\n");
            if (Bool(a.Header, "evaluate") || Bool(b.Header, "evaluate"))
                sb.Append("**HOLDOUT EVALUATION**\n\n");
            sb.Append("- Corpus: `")
                .Append(a.Header.GetProperty("manifest").GetString())
                .Append("`, ")
                .Append(a.Cases.Count)
                .Append(" cases\n");
            sb.Append("- Provenance: ")
                .Append(a.Header.GetProperty("provenance").GetString())
                .Append('\n');
            if (layered)
                sb.Append("- Layer: recogniser\n");
            sb.Append("- Side varied: ").Append(vary);
            if (layered && vary == "parser")
                sb.Append(" (the C# side: parser and recogniser together)");
            sb.Append('\n');
            if (vary == "parser")
            {
                foreach (Side s in new[] { a, b })
                {
                    sb.Append("- ").Append(Label(s)).Append(" staged from:\n");
                    foreach (
                        JsonElement line in s.Header.GetProperty("stagedFrom").EnumerateArray()
                    )
                        sb.Append("  - `").Append(line.GetString()).Append("`\n");
                }
            }
            else
            {
                foreach (Side s in new[] { a, b })
                    sb.Append("- ")
                        .Append(Label(s))
                        .Append(" dump: `")
                        .Append(s.Header.GetProperty("dump").GetString())
                        .Append("`\n");
                sb.Append("- Dump header fields where B differs:");
                JsonElement ha = a.Header.GetProperty("dumpHeader");
                JsonElement hb = b.Header.GetProperty("dumpHeader");
                var names = new List<string>();
                foreach (JsonProperty p in ha.EnumerateObject())
                    if (!names.Contains(p.Name))
                        names.Add(p.Name);
                foreach (JsonProperty p in hb.EnumerateObject())
                    if (!names.Contains(p.Name))
                        names.Add(p.Name);
                bool any = false;
                foreach (string name in names)
                {
                    string ra = Raw(ha, name);
                    string rb = Raw(hb, name);
                    if (string.Equals(ra, rb, StringComparison.Ordinal))
                        continue;
                    any = true;
                    sb.Append("\n  - `")
                        .Append(name)
                        .Append("`: A `")
                        .Append(ra ?? "(absent)")
                        .Append("`, B `")
                        .Append(rb ?? "(absent)")
                        .Append('`');
                }
                sb.Append(any ? "\n" : " (none)\n");
            }
            sb.Append("- Kill check: A ")
                .Append(Bool(a.Header, "pinned") ? "passed" : "not checked")
                .Append(", B ")
                .Append(Bool(b.Header, "pinned") ? "passed" : "not checked")
                .Append('\n');
            foreach (Side s in new[] { a, b })
                sb.Append("- Model conf ")
                    .Append(Label(s))
                    .Append(": ")
                    .Append(ModelConf(s.Header.GetProperty("dumpHeader")))
                    .Append('\n');
            if (layered)
            {
                sb.Append("- Tick rate: A ")
                    .Append(a.Header.GetProperty("tickRate").GetRawText())
                    .Append(" Hz, B ")
                    .Append(b.Header.GetProperty("tickRate").GetRawText())
                    .Append(" Hz\n");
                foreach (Side s in new[] { a, b })
                {
                    sb.Append("- Configuration ").Append(Label(s)).Append(":\n");
                    foreach (JsonProperty p in s.Header.GetProperty("config").EnumerateObject())
                        sb.Append("  - ")
                            .Append(p.Name)
                            .Append('=')
                            .Append(p.Value.GetString())
                            .Append('\n');
                }
                sb.Append(
                    "- Per-record positions may differ between runs by at most one decoder read.\n"
                );
            }
            else
            {
                sb.Append("- Gate:");
                bool first = true;
                foreach (JsonProperty p in a.Header.GetProperty("gate").EnumerateObject())
                {
                    sb.Append(first ? " " : ", ")
                        .Append(p.Name)
                        .Append(' ')
                        .Append(p.Value.GetRawText());
                    first = false;
                }
                sb.Append('\n');
            }

            // 2. Counts, A and B columns (no not-scored row at the recogniser layer).
            int[] ca = Counts(a);
            int[] cb = Counts(b);
            sb.Append("\n## Counts\n\n| | A | B |\n|---|---|---|\n");
            for (int i = 0; i < CountLabels.Length; i++)
                if (!layered || i != NotScoredRow)
                    sb.Append("| ")
                        .Append(CountLabels[i])
                        .Append(" | ")
                        .Append(ca[i])
                        .Append(" | ")
                        .Append(cb[i])
                        .Append(" |\n");

            // 3. Cases, in file order; `*` marks a flip.
            var flips = new List<int>();
            sb.Append("\n## Cases\n\n| case | A | A credit | B | B credit | flip |\n");
            sb.Append("|---|---|---|---|---|---|\n");
            for (int i = 0; i < a.Cases.Count; i++)
            {
                JsonElement ra = a.Cases[i];
                JsonElement rb = b.Cases[i];
                bool flip = Flipped(ra, rb);
                if (flip)
                    flips.Add(i);
                sb.Append("| ")
                    .Append(CaseName(ra))
                    .Append(" | ")
                    .Append(Outcome(ra))
                    .Append(" | ")
                    .Append(Credit(ra))
                    .Append(" | ")
                    .Append(Outcome(rb))
                    .Append(" | ")
                    .Append(Credit(rb))
                    .Append(" | ")
                    .Append(flip ? "*" : "")
                    .Append(" |\n");
            }

            // 4. Not scored at this layer; at the recogniser layer, which scores every non-sealed
            // case, the timing measures instead (*Timing measures*), never a flip.
            if (layered)
            {
                sb.Append("\n## Timing\n\n");
                sb.Append(
                    "| case | A fires (s) | A delay (s) | A finals | A pending | B fires (s) | B delay (s) | B finals | B pending |\n"
                );
                sb.Append("|---|---|---|---|---|---|---|---|---|\n");
                for (int i = 0; i < a.Cases.Count; i++)
                {
                    sb.Append("| ").Append(CaseName(a.Cases[i]));
                    AppendTiming(sb, a.Cases[i]);
                    AppendTiming(sb, b.Cases[i]);
                    sb.Append(" |\n");
                }
            }
            else
            {
                sb.Append("\n## Not scored at this layer\n\n");
                bool listed = false;
                for (int i = 0; i < a.Cases.Count; i++)
                    if (
                        Disposition(a.Cases[i]) == "recogniser-layer"
                        || Disposition(b.Cases[i]) == "recogniser-layer"
                    )
                    {
                        sb.Append("- ").Append(CaseName(a.Cases[i])).Append('\n');
                        listed = true;
                    }
                if (!listed)
                    sb.Append("(none)\n");
            }

            // 5. Flips, each with both sides' transcript and outcome (raw JSON).
            sb.Append("\n## Flips: ").Append(flips.Count).Append('\n');
            foreach (int i in flips)
            {
                sb.Append("\n### ").Append(CaseName(a.Cases[i])).Append("\n\n");
                AppendFlipSide(sb, "A", a.Cases[i]);
                AppendFlipSide(sb, "B", b.Cases[i]);
            }

            // 6. Decoder side only: every case's A and B transcripts (F4).
            if (vary == "decoder")
            {
                sb.Append("\n## Transcripts\n");
                for (int i = 0; i < a.Cases.Count; i++)
                {
                    sb.Append("\n- ").Append(CaseName(a.Cases[i])).Append('\n');
                    sb.Append("  - A: `").Append(Transcript(a.Cases[i])).Append("`\n");
                    sb.Append("  - B: `").Append(Transcript(b.Cases[i])).Append("`\n");
                }
            }

            // 7. Recogniser layer, decoder side only: the sweep measures.
            if (layered && vary == "decoder")
                AppendSweep(sb, a, b);
            return sb.ToString();
        }

        // *Sweep measures*, cases paired by index: the buffer setup and W off both configs;
        // per side the mean finals per utterance over scored cases, and Δf, B − A; S, the mean
        // A delay − B delay over cases correct on both sides with a delay on both (n, printed);
        // KC1, pass when Δf × W ≤ S, unrounded, not evaluated when n is 0; KC2, every case
        // scored on both sides whose space-joined finals differ, any one a STOP.
        static void AppendSweep(StringBuilder sb, Side a, Side b)
        {
            string window = SweepConfig(a, b, "bufferWindow");
            string eager = SweepConfig(a, b, "eagerFlushOnCompleteMatch");
            double w = double.Parse(window, CultureInfo.InvariantCulture);

            double mA = MeanFinals(a);
            double mB = MeanFinals(b);
            double df = mB - mA;

            double sum = 0;
            int n = 0;
            var kc2 = new List<int>();
            for (int i = 0; i < a.Cases.Count; i++)
            {
                JsonElement ra = a.Cases[i];
                JsonElement rb = b.Cases[i];
                if (Disposition(ra) != "scored" || Disposition(rb) != "scored")
                    continue;
                JsonElement da = ra.GetProperty("delay");
                JsonElement db = rb.GetProperty("delay");
                if (
                    Correct(ra)
                    && Correct(rb)
                    && da.ValueKind == JsonValueKind.Number
                    && db.ValueKind == JsonValueKind.Number
                )
                {
                    sum += da.GetDouble() - db.GetDouble();
                    n++;
                }
                if (!string.Equals(SpaceJoined(ra), SpaceJoined(rb), StringComparison.Ordinal))
                    kc2.Add(i);
            }
            double s = n > 0 ? sum / n : 0;

            sb.Append("\n## Sweep measures\n\n");
            sb.Append("- Buffer: ")
                .Append(eager == "true" ? "eager commit" : "time-driven")
                .Append(", W = ")
                .Append(Seconds(w))
                .Append(" s\n");
            sb.Append("- Finals per utterance A: ")
                .Append(Seconds(mA))
                .Append("; B: ")
                .Append(Seconds(mB))
                .Append("; Δf: ")
                .Append(Seconds(df))
                .Append('\n');
            sb.Append("- S: ")
                .Append(n > 0 ? Seconds(s) : "n/a")
                .Append(" s over ")
                .Append(n)
                .Append(" cases correct on both sides\n");
            sb.Append("- KC1 (Δf × W ≤ S): ")
                .Append(
                    n == 0 ? "not evaluated"
                    : df * w <= s ? "pass"
                    : "fail"
                )
                .Append('\n');
            if (kc2.Count == 0)
            {
                sb.Append("- KC2: none\n");
                return;
            }
            sb.Append("- KC2: STOP — ").Append(kc2.Count).Append(" case(s)\n");
            foreach (int i in kc2)
                sb.Append("  - ")
                    .Append(CaseName(a.Cases[i]))
                    .Append(": A \"")
                    .Append(SpaceJoined(a.Cases[i]))
                    .Append("\" → B \"")
                    .Append(SpaceJoined(b.Cases[i]))
                    .Append("\"\n");
        }

        // A `config` key's text, present on both sides and equal, else a stop (R3).
        static string SweepConfig(Side a, Side b, string key)
        {
            foreach (Side s in new[] { a, b })
                if (!s.Header.GetProperty("config").TryGetProperty(key, out _))
                    throw new JoinedInput.Stop(
                        2,
                        "Sweep measures: " + s.Path + ": config has no " + key
                    );
            string ra = a.Header.GetProperty("config").GetProperty(key).GetRawText();
            string rb = b.Header.GetProperty("config").GetProperty(key).GetRawText();
            if (!string.Equals(ra, rb, StringComparison.Ordinal))
                throw new JoinedInput.Stop(
                    2,
                    "Sweep measures: config "
                        + key
                        + " differs: "
                        + a.Path
                        + " "
                        + ra
                        + ", "
                        + b.Path
                        + " "
                        + rb
                );
            return a.Header.GetProperty("config").GetProperty(key).GetString();
        }

        // The mean `finalCount` over a side's scored cases.
        static double MeanFinals(Side s)
        {
            double total = 0;
            int count = 0;
            foreach (JsonElement rec in s.Cases)
                if (Disposition(rec) == "scored")
                {
                    total += rec.GetProperty("finalCount").GetInt32();
                    count++;
                }
            return total / count;
        }

        static bool Correct(JsonElement rec)
        {
            string cls = rec.GetProperty("class").GetString();
            return cls == "correct-fire" || cls == "correct-no-fire";
        }

        // The finals space-joined (KC2 compares and prints these).
        static string SpaceJoined(JsonElement rec)
        {
            var parts = new List<string>();
            foreach (JsonElement f in rec.GetProperty("finals").EnumerateArray())
                parts.Add(f.GetString());
            return string.Join(" ", parts);
        }

        static void AppendFlipSide(StringBuilder sb, string label, JsonElement rec)
        {
            sb.Append("- ")
                .Append(label)
                .Append(": transcript `")
                .Append(Transcript(rec))
                .Append("`; outcome ")
                .Append(Outcome(rec))
                .Append("; intent `")
                .Append(Raw(rec, "intent") ?? "-")
                .Append("`; slots `")
                .Append(Raw(rec, "slots") ?? "-")
                .Append("`; credit `")
                .Append(Raw(rec, "slotCredit") ?? "-")
                .Append("`; extra fires `")
                .Append(Raw(rec, "extraFires") ?? "-")
                .Append("`\n");
        }

        // One side's timing cells: each fire's time and the delay in s (`0.000`), the finals
        // count, the pending label (`pending, timed out` without a fire, `pending` with one);
        // `-` for none, and for every cell of a sealed case.
        static void AppendTiming(StringBuilder sb, JsonElement rec)
        {
            string fires = "-",
                delay = "-",
                finals = "-",
                pending = "-";
            if (Disposition(rec) == "scored")
            {
                var times = new List<string>();
                foreach (JsonElement t in rec.GetProperty("fireTimes").EnumerateArray())
                    times.Add(Seconds(t.GetDouble()));
                if (times.Count > 0)
                    fires = string.Join(", ", times);
                JsonElement d = rec.GetProperty("delay");
                if (d.ValueKind == JsonValueKind.Number)
                    delay = Seconds(d.GetDouble());
                finals = rec.GetProperty("finalCount").GetRawText();
                if (Bool(rec, "pending"))
                    pending = times.Count == 0 ? "pending, timed out" : "pending";
            }
            sb.Append(" | ")
                .Append(fires)
                .Append(" | ")
                .Append(delay)
                .Append(" | ")
                .Append(finals)
                .Append(" | ")
                .Append(pending);
        }

        static string Seconds(double value) =>
            value.ToString("0.000", CultureInfo.InvariantCulture);

        // correct, of which correct (no fire), wrong-slots, wrong-intent, missed, false-fire,
        // extra fires, not scored at this layer, sealed-skipped (CountLabels' order).
        static int[] Counts(Side s)
        {
            var counts = new int[CountLabels.Length];
            foreach (JsonElement rec in s.Cases)
            {
                string disposition = Disposition(rec);
                if (disposition == "recogniser-layer")
                    counts[7]++;
                else if (disposition == "sealed")
                    counts[8]++;
                else
                {
                    switch (rec.GetProperty("class").GetString())
                    {
                        case "correct-fire":
                            counts[0]++;
                            break;
                        case "correct-no-fire":
                            counts[0]++;
                            counts[1]++;
                            break;
                        case "wrong-slots":
                            counts[2]++;
                            break;
                        case "wrong-intent":
                            counts[3]++;
                            break;
                        case "missed":
                            counts[4]++;
                            break;
                        case "false-fire":
                            counts[5]++;
                            break;
                    }
                    counts[6] += rec.GetProperty("extraFires").GetInt32();
                }
            }
            return counts;
        }

        static bool Flipped(JsonElement a, JsonElement b)
        {
            foreach (string key in FlipKeys)
                if (!string.Equals(Raw(a, key), Raw(b, key), StringComparison.Ordinal))
                    return true;
            return false;
        }

        // The class (`correct-no-fire` shown `correct (no fire)`), or the disposition if unscored.
        static string Outcome(JsonElement rec)
        {
            string disposition = Disposition(rec);
            if (disposition != "scored")
                return disposition;
            string cls = rec.GetProperty("class").GetString();
            return cls == "correct-no-fire" ? "correct (no fire)" : cls;
        }

        static string Credit(JsonElement rec)
        {
            if (!rec.TryGetProperty("slotCredit", out JsonElement v))
                return "";
            return v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
        }

        // The finals joined ` | `; `(none)` with none, `(sealed)` for a sealed (unlisted) case.
        static string Transcript(JsonElement rec)
        {
            if (!rec.TryGetProperty("finals", out JsonElement finals))
                return "(sealed)";
            if (finals.GetArrayLength() == 0)
                return "(none)";
            var parts = new List<string>();
            foreach (JsonElement f in finals.EnumerateArray())
                parts.Add(f.GetString());
            return string.Join(" | ", parts);
        }

        // A conf's identity: SHA-256 of its UTF-8 bytes, the first 12 lowercase hex digits.
        internal static string ConfSha12(string conf) =>
            Convert
                .ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conf)))
                .Substring(0, 12);

        // A dump header's `modelConf`: `sha256 <12 hex>` then its `--endpoint.rule` lines in
        // order, `; `-joined; `not recorded` when the header has none.
        static string ModelConf(JsonElement dumpHeader)
        {
            if (
                !dumpHeader.TryGetProperty("modelConf", out JsonElement v)
                || v.ValueKind != JsonValueKind.String
            )
                return "not recorded";
            string conf = v.GetString();
            var parts = new List<string> { "sha256 " + ConfSha12(conf) };
            foreach (string line in conf.Split('\n'))
                if (line.Trim().StartsWith("--endpoint.rule", StringComparison.Ordinal))
                    parts.Add(line.Trim());
            return string.Join("; ", parts);
        }

        static string Label(Side s) => s.Header.GetProperty("side").GetString() == "a" ? "A" : "B";

        static string CaseName(JsonElement rec) => rec.GetProperty("case").GetString();

        static string Disposition(JsonElement rec) => rec.GetProperty("disposition").GetString();

        static bool Bool(JsonElement rec, string key) => rec.GetProperty(key).GetBoolean();

        // A recogniser-layer file: its header has `layer`.
        static bool Layered(Side s) => s.Header.TryGetProperty("layer", out _);

        static string Raw(JsonElement rec, string key) =>
            rec.TryGetProperty(key, out JsonElement v) ? v.GetRawText() : null;
    }
}
