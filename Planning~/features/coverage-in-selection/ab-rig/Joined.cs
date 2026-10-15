// ============================================================================
// Purpose:  The rig's --joined mode: sub-verb dispatch, the drive-check hook, option parsing, grammar-pin guard, kill check, scored-record writer, the timing check
// Layer:    Planning.AbRig
// Owns:     Joined (internal static class)
// Depends:  JoinedInput, JoinedScorer, JoinedReport, VoxrCommandParser, VoxrCommandDefinition, VoxrSlotDefinition, VoxrSlotMatch
// ============================================================================
// feat-joined-rig-parser Phases 2-3. A `score` run scores one side (one dump through one
// staged ref); `report` compares the two sides' scored files (JoinedReport); `drive-check`
// (feat-joined-rig-recogniser Phase 1) runs JoinedRecogniser's scripted worlds, reached through
// the DriveCheck hook it sets from a module initializer in a recogniser stage only; `timing`
// (feat-joined-rig-recogniser Phase 2) checks schema-2 dump positions, and with two dumps
// their agreement:
//
//   --joined score --dump D --manifest M --pin P --out O --side a|b --vary parser|decoder [--pinned] [--evaluate]
//   --joined report --a A --b B --out R
//   --joined drive-check
//   --joined timing --dump D [--dump-b D2]
//
// Exit 0 scored (or reported, or every world passed, or every timing case passed); 1 a
// drive-check or timing FAIL; 2 an input stop or the grammar-pin guard (or a drive-check or
// timing stop); 3 the kill check (the pinned dump does not reproduce the pin). Every stop is
// named on stderr, prefixed `joined: `;
// every stop after the options parse leaves no scored file (an argument stop precedes `--out`
// being known). Each scored case's outcome is JoinedScorer's.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using VoXR.Commands;

namespace AbRig
{
    static class Joined
    {
        static readonly string[] ValueOptions =
        {
            "--dump",
            "--manifest",
            "--pin",
            "--out",
            "--side",
            "--vary",
        };

        static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // The input being read, so an unexpected failure still names its file.
        static string reading;

        const string ScoreUsage =
            "usage: --joined score --dump D --manifest M --pin P --out O --side a|b --vary parser|decoder [--pinned] [--evaluate]";
        const string ReportUsage = "usage: --joined report --a A --b B --out R";
        const string DriveCheckUsage = "usage: --joined drive-check (a recogniser stage only)";
        const string TimingUsage = "usage: --joined timing --dump D [--dump-b D2]";

        // Set by JoinedRecogniser's module initializer; null in a parser stage, which does not
        // compile JoinedRecogniser (*Stage layering*).
        internal static Func<
            string[],
            VoxrSlotDefinition[],
            VoxrCommandDefinition[],
            int
        > DriveCheck;

        // `parser` is the default replay's (Program.cs), for the scorer; `grammarJson` is what
        // --grammar prints for the staged ref, which the guard compares with the pin; `commands`
        // are the parser's definitions, which the scorer's fire gate reads; `slots` and
        // `commands` are what drive-check configures its staged recogniser with.
        internal static int Run(
            string[] args,
            VoxrCommandParser parser,
            string grammarJson,
            VoxrSlotDefinition[] slots,
            VoxrCommandDefinition[] commands
        )
        {
            try
            {
                int at = Array.IndexOf(args, "--joined");
                string verb = at + 1 < args.Length ? args[at + 1] : null;
                if (verb == "score")
                    return Score(args, parser, grammarJson, commands);
                if (verb == "report")
                    return JoinedReport.Run(args);
                if (verb == "drive-check")
                {
                    if (DriveCheck == null)
                        throw new JoinedInput.Stop(
                            2,
                            "drive-check needs a recogniser stage: this stage has no JoinedRecogniser; stage with stage.sh <ref> <out> recogniser"
                        );
                    return DriveCheck(args, slots, commands);
                }
                if (verb == "timing")
                    return Timing(args);
                throw new JoinedInput.Stop(
                    2,
                    ScoreUsage,
                    ReportUsage,
                    DriveCheckUsage,
                    TimingUsage
                );
            }
            catch (JoinedInput.Stop stop)
            {
                foreach (string line in stop.Lines)
                    Console.Error.WriteLine("joined: " + line);
                return stop.Code;
            }
            catch (Exception e)
            {
                string at =
                    e is JsonException je && je.LineNumber.HasValue
                        ? ":" + (je.LineNumber.Value + 1)
                        : "";
                Console.Error.WriteLine(
                    "joined: "
                        + (reading ?? "(no input file)")
                        + at
                        + ": "
                        + e.GetType().Name
                        + ": "
                        + e.Message
                );
                return 2;
            }
        }

        static int Score(
            string[] args,
            VoxrCommandParser parser,
            string grammarJson,
            VoxrCommandDefinition[] commands
        )
        {
            bool pinned,
                evaluate;
            Dictionary<string, string> o = ParseOptions(args, out pinned, out evaluate);
            string outPath = o["--out"];

            // 1. No stale scored file survives a stop after the options parse; then every reader
            // finishes first.
            reading = outPath;
            if (File.Exists(outPath))
                File.Delete(outPath);
            reading = "./STAGED_FROM";
            string[] stagedFrom = JoinedInput.SplitLines(File.ReadAllText("STAGED_FROM"));
            reading = o["--dump"];
            JoinedInput.Dump dump = JoinedInput.ReadDump(o["--dump"]);
            reading = o["--manifest"];
            JoinedInput.Manifest manifest = JoinedInput.ReadManifest(o["--manifest"]);
            reading = o["--pin"];
            JoinedInput.Pin pin = JoinedInput.ReadPin(o["--pin"]);
            reading = null;
            JoinedInput.Join(dump, manifest, pin, pinned);

            // 2. Grammar-pin guard (F11): the staged ref's grammar is the pin's, ordinal.
            string[] rigGrammar = ParseGrammar(grammarJson);
            int diff = FirstDifference(rigGrammar, pin.Grammar);
            if (diff >= 0)
                throw new JoinedInput.Stop(
                    2,
                    "grammar guard: "
                        + (stagedFrom.Length > 0 ? stagedFrom[0] : "(empty STAGED_FROM)")
                        + ": the rig's grammar differs from the pin's ("
                        + pin.Path
                        + ") at index "
                        + diff
                        + ": rig "
                        + Entry(rigGrammar, diff)
                        + ", pin "
                        + Entry(pin.Grammar, diff)
                );

            // 3. Kill check (F2): the pinned dump reproduces the pin, configuration and finals.
            if (pinned)
            {
                var failing = new List<string>();
                if (dump.Gain != pin.Gain)
                    failing.Add(
                        "kill check: header `gain`: expected "
                            + Number(pin.Gain)
                            + ", actual "
                            + Number(dump.Gain)
                    );
                if (dump.Libvosk != pin.Libvosk)
                    failing.Add(
                        "kill check: header `libvosk`: expected \""
                            + pin.Libvosk
                            + "\", actual \""
                            + dump.Libvosk
                            + "\""
                    );
                int g = FirstDifference(dump.Grammar, pin.Grammar);
                if (g >= 0)
                    failing.Add(
                        "kill check: header `grammar` at index "
                            + g
                            + ": expected "
                            + Entry(pin.Grammar, g)
                            + ", actual "
                            + Entry(dump.Grammar, g)
                    );
                foreach (JoinedInput.DumpCase c in dump.Cases)
                {
                    if (c.Failures.Count > 0)
                        failing.Add(
                            "kill check: case \""
                                + c.Name
                                + "\": failed decode: "
                                + string.Join("; ", c.Failures)
                        );
                    string[] expected = pin.Cases[c.Name];
                    string[] actual = c.Finals.ToArray();
                    if (FirstDifference(actual, expected) >= 0)
                        failing.Add(
                            "kill check: case \""
                                + c.Name
                                + "\": finals expected "
                                + JsonArray(expected)
                                + ", actual "
                                + JsonArray(actual)
                        );
                }
                if (failing.Count > 0)
                    throw new JoinedInput.Stop(3, failing.ToArray());
            }
            // 4. Unpinned side: a failed decode is listed, never scored.
            else
            {
                var failed = new List<string>();
                foreach (JoinedInput.DumpCase c in dump.Cases)
                    if (c.Failures.Count > 0)
                        failed.Add(
                            dump.Path
                                + ": case \""
                                + c.Name
                                + "\": failed decode: "
                                + string.Join("; ", c.Failures)
                        );
                if (failed.Count > 0)
                    throw new JoinedInput.Stop(2, failed.ToArray());
            }

            // 5. The scored record: a header, then one line per manifest case.
            var dumpByCase = new Dictionary<string, JoinedInput.DumpCase>(StringComparer.Ordinal);
            foreach (JoinedInput.DumpCase c in dump.Cases)
                dumpByCase.Add(c.Name, c);

            int scored = 0,
                recogniserLayer = 0,
                sealedCount = 0;
            var classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string cls in JoinedScorer.Classes)
                classCounts.Add(cls, 0);
            var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteString("kind", "header");
                w.WriteString("side", o["--side"]);
                w.WriteString("vary", o["--vary"]);
                w.WriteStartArray("stagedFrom");
                foreach (string line in stagedFrom)
                    w.WriteStringValue(line);
                w.WriteEndArray();
                w.WriteString("dump", dump.Path);
                w.WriteStartObject("dumpHeader");
                foreach (JsonProperty p in dump.Header.EnumerateObject())
                    if (p.Name != "kind")
                        p.WriteTo(w);
                w.WriteEndObject();
                w.WriteString("manifest", manifest.Path);
                w.WriteString("provenance", manifest.Provenance);
                w.WriteBoolean("pinned", pinned);
                w.WriteStartObject("gate");
                w.WriteNumber("minScore", JoinedScorer.MinScore);
                w.WriteNumber("minConfidence", JoinedScorer.MinConfidence);
                w.WriteEndObject();
                w.WriteBoolean("evaluate", evaluate);
                w.WriteEndObject();
            }
            buffer.WriteByte((byte)'\n');

            foreach (JoinedInput.ManifestCase c in manifest.Cases)
            {
                // Under --evaluate a sealed case goes as unsealed (F7).
                string disposition;
                if (c.Sealed && !evaluate)
                {
                    disposition = "sealed";
                    sealedCount++;
                }
                else if (c.Layer == "recogniser")
                {
                    disposition = "recogniser-layer";
                    recogniserLayer++;
                }
                else
                {
                    disposition = "scored";
                    scored++;
                }

                JoinedInput.DumpCase dumped = dumpByCase[c.Name];
                JoinedScorer.Outcome s =
                    disposition == "scored"
                        ? JoinedScorer.Score(parser, commands, c, dumped)
                        : null;
                if (s != null)
                    classCounts[s.Class]++;

                using (var w = new Utf8JsonWriter(buffer, WriterOptions))
                {
                    w.WriteStartObject();
                    w.WriteString("kind", "case");
                    w.WriteString("case", c.Name);
                    w.WriteString("disposition", disposition);
                    if (disposition != "sealed")
                    {
                        w.WriteStartArray("finals");
                        foreach (string f in dumped.Finals)
                            w.WriteStringValue(f);
                        w.WriteEndArray();
                    }
                    // *Scored record*: the primary fire's intent, slots and score, or null / [].
                    if (s != null)
                    {
                        w.WriteString("class", s.Class);
                        if (s.HasPrimary)
                            w.WriteString("intent", s.Primary.Intent);
                        else
                            w.WriteNull("intent");
                        w.WriteStartArray("slots");
                        if (s.HasPrimary)
                            foreach (VoxrSlotMatch slot in s.Primary.Slots)
                            {
                                w.WriteStartObject();
                                w.WriteString("name", slot.Name);
                                w.WriteString("value", slot.Value);
                                w.WriteEndObject();
                            }
                        w.WriteEndArray();
                        if (s.HasPrimary)
                            w.WriteNumber("score", s.Primary.Score);
                        else
                            w.WriteNull("score");
                        if (s.SlotCredit.HasValue)
                            w.WriteNumber("slotCredit", s.SlotCredit.Value);
                        else
                            w.WriteString("slotCredit", "n/a");
                        w.WriteNumber("extraFires", s.ExtraFires);
                        w.WriteNumber("finalCount", s.FinalCount);
                    }
                    w.WriteEndObject();
                }
                buffer.WriteByte((byte)'\n');
            }

            reading = outPath;
            File.WriteAllBytes(outPath, buffer.ToArray());
            reading = null;

            // 6. The counts: dispositions, then the report's classes (correct = both correct-*).
            Console.Out.WriteLine("scored " + scored);
            Console.Out.WriteLine("recogniser-layer " + recogniserLayer);
            Console.Out.WriteLine("sealed " + sealedCount);
            Console.Out.WriteLine(
                "correct " + (classCounts["correct-fire"] + classCounts["correct-no-fire"])
            );
            Console.Out.WriteLine("wrong-slots " + classCounts["wrong-slots"]);
            Console.Out.WriteLine("wrong-intent " + classCounts["wrong-intent"]);
            Console.Out.WriteLine("missed " + classCounts["missed"]);
            Console.Out.WriteLine("false-fire " + classCounts["false-fire"]);
            return 0;
        }

        // `timing` (*Checks as commands*), on schema-2 dumps only; the reader has already held
        // each case's `consumed` non-decreasing and within `pushed`, and its `origin` constant.
        // FAIL per dump: a case-end `consumed` other than its `pushed`. FAIL with two dumps:
        // unequal case sets or ordered non-empty final texts, or finals paired by order unequal
        // in word count or in any word's per-case `start` or `end` (the printed time minus
        // `origin` / `decoderRate`, a decimal from the number's text, compared exactly). The
        // largest `consumed` difference is over those final pairs, never results by `seq`.
        static int Timing(string[] args)
        {
            int at = Array.IndexOf(args, "--joined");
            string pathA = null,
                pathB = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == at || i == at + 1)
                    continue;
                string a = args[i];
                if (a != "--dump" && a != "--dump-b")
                    throw new JoinedInput.Stop(2, "unknown argument \"" + a + "\"");
                if ((a == "--dump" ? pathA : pathB) != null)
                    throw new JoinedInput.Stop(2, a + " given twice");
                if (i + 1 >= args.Length || i + 1 == at)
                    throw new JoinedInput.Stop(2, a + " needs a value");
                if (a == "--dump")
                    pathA = args[++i];
                else
                    pathB = args[++i];
            }
            if (pathA == null)
                throw new JoinedInput.Stop(2, "missing --dump", TimingUsage);

            JoinedInput.Dump dumpA = ReadTimed(pathA);
            JoinedInput.Dump dumpB = pathB != null ? ReadTimed(pathB) : null;

            var casesB = new Dictionary<string, JoinedInput.DumpCase>(StringComparer.Ordinal);
            if (dumpB != null)
                foreach (JoinedInput.DumpCase c in dumpB.Cases)
                    casesB.Add(c.Name, c);
            var namesA = new HashSet<string>(StringComparer.Ordinal);

            int passed = 0,
                total = 0;
            long largest = 0;
            foreach (JoinedInput.DumpCase ca in dumpA.Cases)
            {
                namesA.Add(ca.Name);
                var why = new List<string>();
                CheckConsumed(dumpA, ca, why);
                if (dumpB != null)
                {
                    if (casesB.TryGetValue(ca.Name, out JoinedInput.DumpCase cb))
                    {
                        CheckConsumed(dumpB, cb, why);
                        CompareFinals(dumpA, ca, dumpB, cb, why, ref largest);
                    }
                    else
                        why.Add("not in " + dumpB.Path);
                }
                total++;
                passed += TimingVerdict(ca.Name, why);
            }
            if (dumpB != null)
            {
                foreach (JoinedInput.DumpCase cb in dumpB.Cases)
                {
                    if (namesA.Contains(cb.Name))
                        continue;
                    var why = new List<string>();
                    CheckConsumed(dumpB, cb, why);
                    why.Add("not in " + dumpA.Path);
                    total++;
                    passed += TimingVerdict(cb.Name, why);
                }
                Console.Out.WriteLine(
                    "timing: largest consumed difference " + largest + " input samples"
                );
            }
            Console.Out.WriteLine("timing: " + passed + "/" + total + " cases passed");
            return passed == total ? 0 : 1;
        }

        static JoinedInput.Dump ReadTimed(string path)
        {
            reading = path;
            JoinedInput.Dump dump = JoinedInput.ReadDump(path);
            reading = null;
            if (dump.Schema == 1)
                throw new JoinedInput.Stop(2, path + ": schema 1, no positions");
            return dump;
        }

        static void CheckConsumed(JoinedInput.Dump dump, JoinedInput.DumpCase c, List<string> why)
        {
            if (c.Consumed.Value != c.Pushed)
                why.Add(
                    dump.Path
                        + ": case-end `consumed` "
                        + c.Consumed.Value
                        + ", `pushed` "
                        + c.Pushed
                );
        }

        // Prints each FAIL line of the case; 1 when it passed, else 0.
        static int TimingVerdict(string name, List<string> why)
        {
            foreach (string line in why)
                Console.Out.WriteLine("FAIL " + name + ": " + line);
            return why.Count == 0 ? 1 : 0;
        }

        static void CompareFinals(
            JoinedInput.Dump dumpA,
            JoinedInput.DumpCase ca,
            JoinedInput.Dump dumpB,
            JoinedInput.DumpCase cb,
            List<string> why,
            ref long largest
        )
        {
            var fa = TimedFinals(dumpA, ca);
            var fb = TimedFinals(dumpB, cb);
            bool sameTexts = fa.Count == fb.Count;
            for (int k = 0; sameTexts && k < fa.Count; k++)
                sameTexts = string.Equals(fa[k].Text, fb[k].Text, StringComparison.Ordinal);
            if (!sameTexts)
            {
                why.Add(
                    "finals "
                        + JsonArray(fa.ConvertAll(f => f.Text).ToArray())
                        + " in "
                        + dumpA.Path
                        + ", "
                        + JsonArray(fb.ConvertAll(f => f.Text).ToArray())
                        + " in "
                        + dumpB.Path
                );
                return;
            }
            for (int k = 0; k < fa.Count; k++)
            {
                largest = Math.Max(largest, Math.Abs(fa[k].Consumed - fb[k].Consumed));
                string final = "final " + k + " \"" + fa[k].Text + "\"";
                if (fa[k].Start.Length != fb[k].Start.Length)
                {
                    why.Add(
                        final
                            + ": "
                            + fa[k].Start.Length
                            + " words in "
                            + dumpA.Path
                            + ", "
                            + fb[k].Start.Length
                            + " in "
                            + dumpB.Path
                    );
                    continue;
                }
                for (int w = 0; w < fa[k].Start.Length; w++)
                {
                    if (fa[k].Start[w] != fb[k].Start[w])
                        why.Add(TimeDiff(final, w, "start", fa[k].Start[w], fb[k].Start[w]));
                    if (fa[k].End[w] != fb[k].End[w])
                        why.Add(TimeDiff(final, w, "end", fa[k].End[w], fb[k].End[w]));
                }
            }
        }

        static string TimeDiff(string final, int word, string key, decimal a, decimal b) =>
            final
            + ": word "
            + word
            + " per-case `"
            + key
            + "` "
            + a.ToString(CultureInfo.InvariantCulture)
            + " vs "
            + b.ToString(CultureInfo.InvariantCulture);

        // The case's non-empty finals in dump order (the reader's rule: final, a payload with
        // no `error`, non-empty `text`), each with its `consumed` and every word's per-case
        // `start` and `end`.
        static List<(string Text, long Consumed, decimal[] Start, decimal[] End)> TimedFinals(
            JoinedInput.Dump dump,
            JoinedInput.DumpCase c
        )
        {
            var finals = new List<(string Text, long Consumed, decimal[] Start, decimal[] End)>();
            foreach (JoinedInput.DumpResult r in c.Results)
            {
                if (!r.Final || r.Payload == null)
                    continue;
                using (JsonDocument doc = JsonDocument.Parse(r.Payload))
                {
                    JsonElement p = doc.RootElement;
                    if (p.TryGetProperty("error", out _))
                        continue;
                    string text = p.GetProperty("text").GetString();
                    if (text.Length == 0)
                        continue;
                    decimal origin = (decimal)r.Origin.Value / dump.DecoderRate.Value;
                    JsonElement words = p.GetProperty("result");
                    var start = new decimal[words.GetArrayLength()];
                    var end = new decimal[start.Length];
                    int i = 0;
                    foreach (JsonElement e in words.EnumerateArray())
                    {
                        string where =
                            dump.Path
                            + ": case \""
                            + c.Name
                            + "\" seq "
                            + r.Seq
                            + " `result`["
                            + i
                            + "]";
                        start[i] = DecimalText(e.GetProperty("start"), "start", where) - origin;
                        end[i] = DecimalText(e.GetProperty("end"), "end", where) - origin;
                        i++;
                    }
                    finals.Add((text, r.Consumed.Value, start, end));
                }
            }
            return finals;
        }

        // A JSON number as a decimal from its own text, exponent forms included; never a double.
        static decimal DecimalText(JsonElement number, string key, string where)
        {
            string text = number.GetRawText();
            if (
                !decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal value
                )
            )
                throw new JoinedInput.Stop(
                    2,
                    where + ": `" + key + "` " + text + " does not parse as a decimal"
                );
            return value;
        }

        static Dictionary<string, string> ParseOptions(
            string[] args,
            out bool pinned,
            out bool evaluate
        )
        {
            int at = Array.IndexOf(args, "--joined");
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            pinned = false;
            evaluate = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == at || i == at + 1)
                    continue;
                string a = args[i];
                if (a == "--pinned")
                {
                    if (pinned)
                        throw new JoinedInput.Stop(2, "--pinned given twice");
                    pinned = true;
                    continue;
                }
                if (a == "--evaluate")
                {
                    if (evaluate)
                        throw new JoinedInput.Stop(2, "--evaluate given twice");
                    evaluate = true;
                    continue;
                }
                if (Array.IndexOf(ValueOptions, a) < 0)
                    throw new JoinedInput.Stop(2, "unknown argument \"" + a + "\"");
                if (options.ContainsKey(a))
                    throw new JoinedInput.Stop(2, a + " given twice");
                if (i + 1 >= args.Length || i + 1 == at)
                    throw new JoinedInput.Stop(2, a + " needs a value");
                options.Add(a, args[++i]);
            }

            foreach (string option in ValueOptions)
                if (!options.ContainsKey(option))
                    throw new JoinedInput.Stop(2, "missing " + option);
            if (options["--side"] != "a" && options["--side"] != "b")
                throw new JoinedInput.Stop(
                    2,
                    "--side must be a or b, not \"" + options["--side"] + "\""
                );
            if (options["--vary"] != "parser" && options["--vary"] != "decoder")
                throw new JoinedInput.Stop(
                    2,
                    "--vary must be parser or decoder, not \"" + options["--vary"] + "\""
                );
            return options;
        }

        static string[] ParseGrammar(string grammarJson)
        {
            using (JsonDocument doc = JsonDocument.Parse(grammarJson))
            {
                JsonElement root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    var entries = new string[root.GetArrayLength()];
                    int i = 0;
                    foreach (JsonElement e in root.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.String)
                            break;
                        entries[i++] = e.GetString();
                    }
                    if (i == entries.Length)
                        return entries;
                }
            }
            throw new JoinedInput.Stop(
                2,
                "grammar guard: the rig's grammar JSON is not an array of strings"
            );
        }

        // The first index where the two differ ordinally (a length difference counts at the
        // shorter's end); -1 when equal.
        static int FirstDifference(string[] a, string[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return i;
            return a.Length == b.Length ? -1 : n;
        }

        static string Entry(string[] entries, int index) =>
            index < entries.Length ? "\"" + entries[index] + "\"" : "(none)";

        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        static string JsonArray(string[] values)
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
