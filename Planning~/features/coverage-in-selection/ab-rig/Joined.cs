// ============================================================================
// Purpose:  The rig's --joined mode: option parsing, grammar-pin guard, kill check, scored-record writer
// Layer:    Planning.AbRig
// Owns:     Joined (internal static class)
// Depends:  JoinedInput, VoxrCommandParser
// ============================================================================
// feat-joined-rig-parser Phase 2. One run scores one side (one dump through one staged ref):
//
//   --joined score --dump D --manifest M --pin P --out O --side a|b --vary parser|decoder [--pinned]
//
// Exit 0 scored; 2 an input stop or the grammar-pin guard; 3 the kill check (the pinned
// dump does not reproduce the pin). Every stop is named on stderr, prefixed `joined: `, and
// leaves no scored file. Scoring itself (JoinedScorer) and `report` are Phase 3's.
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

        // `parser` is the default replay's (Program.cs), for Phase 3's scorer; `grammarJson` is
        // what --grammar prints for the staged ref, which the guard compares with the pin.
        internal static int Run(string[] args, VoxrCommandParser parser, string grammarJson)
        {
            try
            {
                return Score(args, grammarJson);
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

        static int Score(string[] args, string grammarJson)
        {
            bool pinned;
            Dictionary<string, string> o = ParseOptions(args, out pinned);
            string outPath = o["--out"];

            // 1. No stale scored file survives a stop; then every reader finishes first.
            reading = outPath;
            if (File.Exists(outPath))
                File.Delete(outPath);
            reading = "./STAGED_FROM";
            string[] stagedFrom = SplitLines(File.ReadAllText("STAGED_FROM"));
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
            var finalsByCase = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (JoinedInput.DumpCase c in dump.Cases)
                finalsByCase.Add(c.Name, c.Finals);

            int scored = 0,
                recogniserLayer = 0,
                sealedCount = 0;
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
                w.WriteEndObject();
            }
            buffer.WriteByte((byte)'\n');

            foreach (JoinedInput.ManifestCase c in manifest.Cases)
            {
                string disposition;
                if (c.Sealed)
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

                using (var w = new Utf8JsonWriter(buffer, WriterOptions))
                {
                    w.WriteStartObject();
                    w.WriteString("kind", "case");
                    w.WriteString("case", c.Name);
                    w.WriteString("disposition", disposition);
                    if (!c.Sealed)
                    {
                        w.WriteStartArray("finals");
                        foreach (string f in finalsByCase[c.Name])
                            w.WriteStringValue(f);
                        w.WriteEndArray();
                    }
                    w.WriteEndObject();
                }
                buffer.WriteByte((byte)'\n');
            }

            reading = outPath;
            File.WriteAllBytes(outPath, buffer.ToArray());
            reading = null;

            // 6. The counts.
            Console.Out.WriteLine("scored " + scored);
            Console.Out.WriteLine("recogniser-layer " + recogniserLayer);
            Console.Out.WriteLine("sealed " + sealedCount);
            return 0;
        }

        static Dictionary<string, string> ParseOptions(string[] args, out bool pinned)
        {
            int at = Array.IndexOf(args, "--joined");
            if (at + 1 >= args.Length || args[at + 1] != "score")
                throw new JoinedInput.Stop(
                    2,
                    "usage: --joined score --dump D --manifest M --pin P --out O --side a|b --vary parser|decoder [--pinned]"
                );

            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            pinned = false;
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

        static string[] SplitLines(string text)
        {
            string[] lines = text.Split('\n');
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0)
                count--;
            Array.Resize(ref lines, count);
            return lines;
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
