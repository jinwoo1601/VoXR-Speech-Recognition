// ============================================================================
// Purpose:  Strict readers for the joined mode's inputs (harness dump, corpus manifest, pin) and their case joins
// Layer:    Planning.AbRig
// Owns:     JoinedInput (internal static class), JoinedInput.Stop (internal sealed class), JoinedInput.Word (internal sealed class), JoinedInput.Dump (internal sealed class), JoinedInput.DumpCase (internal sealed class), JoinedInput.Manifest (internal sealed class), JoinedInput.ManifestCase (internal sealed class), JoinedInput.Pin (internal sealed class)
// Depends:  (none)
// ============================================================================
// Every reader finishes before the guard, the kill check or any scoring looks at its
// output, and every input defect is a named stop: exit 2 with the file (and line, for the
// dump) or the case and key. Unknown dump record keys are ignored (`schema` versions the
// dump); the manifest and the pin are read strictly (feat-joined-rig-parser, *Marks*).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AbRig
{
    static class JoinedInput
    {
        // A named stop: Joined.Run prints each line to stderr and exits with Code (2 an input
        // or guard stop, 3 the kill check).
        internal sealed class Stop : Exception
        {
            internal readonly int Code;
            internal readonly string[] Lines;

            internal Stop(int code, params string[] lines)
                : base(string.Join(" / ", lines))
            {
                Code = code;
                Lines = lines;
            }
        }

        // One `payload.result` entry of a non-empty final: Vosk's `word`, `conf`, `start`, `end`,
        // each number read straight to a float as VoxrJsonParser.ParseWordsFromJson reads it.
        internal sealed class Word
        {
            internal string Text;
            internal float Conf;
            internal float Start;
            internal float End;
        }

        internal sealed class DumpCase
        {
            internal string Name;

            // Ordered `text`s of the case's `final:true` records, empty ones dropped.
            internal readonly List<string> Finals = new List<string>();

            // Each of Finals' `payload.result` words, parallel to Finals.
            internal readonly List<Word[]> Words = new List<Word[]>();

            // Why the case is a failed decode (`raw`, an `error` payload, non-empty `errors`);
            // empty = decoded.
            internal readonly List<string> Failures = new List<string>();
        }

        internal sealed class Dump
        {
            internal string Path;
            internal JsonElement Header;
            internal double Gain;
            internal string Libvosk;
            internal string[] Grammar;
            internal readonly List<DumpCase> Cases = new List<DumpCase>();
        }

        internal sealed class ManifestCase
        {
            internal string File;
            internal string Name; // the join key: `file` after its last '/'
            internal bool Sealed;
            internal string Layer; // "parser" or "recogniser"; absent = parser
            internal string ExpectedIntent; // "" = a negative
            internal KeyValuePair<string, string>[] ExpectedSlots; // ordered {name, value}
        }

        internal sealed class Manifest
        {
            internal string Path;
            internal string Provenance;
            internal readonly List<ManifestCase> Cases = new List<ManifestCase>();
        }

        internal sealed class Pin
        {
            internal string Path;
            internal double Gain;
            internal string Libvosk;
            internal string[] Grammar;

            // file -> expectedFinals, in the pin's case order.
            internal readonly Dictionary<string, string[]> Cases = new Dictionary<string, string[]>(
                StringComparer.Ordinal
            );
            internal readonly List<string> Order = new List<string>();
        }

        static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // Today's corpus manifest keys plus the two marks; any other key stops the run.
        static readonly HashSet<string> ManifestCaseKeys = new HashSet<string>(
            StringComparer.Ordinal
        )
        {
            "file",
            "category",
            "phrase",
            "segments",
            "gapSeconds",
            "silenceSeconds",
            "peak",
            "expectedIntent",
            "expectedSlots",
            "expectedTranscript",
            "description",
            "sealed",
            "layer",
        };

        static readonly string[] PinKeys = { "_comment", "cases", "gain", "grammar", "libvosk" };

        // --- dump -------------------------------------------------------------------------

        internal static Dump ReadDump(string path)
        {
            string text = File.ReadAllText(path, StrictUtf8);
            string[] lines = text.Split('\n');
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0)
                count--; // the LF closing the last record
            if (count == 0)
                throw new Stop(2, path + ": empty dump");

            var dump = new Dump { Path = path };
            var framed = new HashSet<string>(StringComparer.Ordinal);
            DumpCase open = null;
            long nextSeq = 0;
            bool ended = false;

            for (int i = 0; i < count; i++)
            {
                string where = path + ":" + (i + 1);
                if (ended)
                    throw new Stop(2, where + ": a record after `end`");
                if (lines[i].IndexOf('\r') >= 0)
                    throw new Stop(2, where + ": CR in the line (the dump is LF only)");

                JsonElement rec = ParseObject(lines[i], where);
                string kind = Str(rec, "kind", where);

                if (i == 0)
                {
                    if (kind != "header")
                        throw new Stop(
                            2,
                            where + ": the first record is \"" + kind + "\", not `header`"
                        );
                    long schema = Int(rec, "schema", where);
                    if (schema != 1)
                        throw new Stop(2, where + ": `schema` " + schema + ", expected 1");
                    Str(rec, "manifest", where);
                    Str(rec, "fixtures", where);
                    Str(rec, "model", where);
                    dump.Gain = Num(rec, "gain", where);
                    dump.Libvosk = Str(rec, "libvosk", where);
                    dump.Grammar = StrArray(rec, "grammar", where);
                    Int(rec, "ringCapacity", where);
                    dump.Header = rec;
                    continue;
                }

                switch (kind)
                {
                    case "header":
                        throw new Stop(2, where + ": a second `header`");

                    case "case":
                    {
                        if (open != null)
                            throw new Stop(
                                2,
                                where + ": case \"" + open.Name + "\" has no `case-end`"
                            );
                        string name = Str(rec, "case", where);
                        Int(rec, "samples", where);
                        Int(rec, "sampleRate", where);
                        if (!framed.Add(name))
                            throw new Stop(2, where + ": case \"" + name + "\" framed twice");
                        open = new DumpCase { Name = name };
                        dump.Cases.Add(open);
                        nextSeq = 0;
                        break;
                    }

                    case "result":
                    {
                        if (open == null)
                            throw new Stop(2, where + ": `result` outside a case frame");
                        string name = Str(rec, "case", where);
                        if (name != open.Name)
                            throw new Stop(
                                2,
                                where
                                    + ": `result` for case \""
                                    + name
                                    + "\" inside case \""
                                    + open.Name
                                    + "\""
                            );
                        long seq = Int(rec, "seq", where);
                        if (seq != nextSeq)
                            throw new Stop(2, where + ": `seq` " + seq + ", expected " + nextSeq);
                        nextSeq++;
                        bool isFinal = Bool(rec, "final", where);
                        Int(rec, "pushed", where);
                        Int(rec, "prevPushed", where);

                        if (rec.TryGetProperty("raw", out JsonElement raw))
                        {
                            if (raw.ValueKind != JsonValueKind.String)
                                throw new Stop(2, where + ": mistyped field `raw`");
                            open.Failures.Add("unparseable result (`raw`) at seq " + seq);
                            break;
                        }
                        JsonElement payload = Obj(rec, "payload", where);
                        if (payload.TryGetProperty("error", out _))
                            open.Failures.Add(
                                "`error` payload at seq " + seq + ": " + payload.GetRawText()
                            );
                        else if (isFinal)
                        {
                            string finalText = Str(payload, "text", where);
                            if (finalText.Length > 0)
                            {
                                open.Finals.Add(finalText);
                                open.Words.Add(ReadWords(payload, where));
                            }
                        }
                        else
                            Str(payload, "partial", where);
                        break;
                    }

                    case "case-end":
                    {
                        if (open == null)
                            throw new Stop(2, where + ": `case-end` outside a case frame");
                        string name = Str(rec, "case", where);
                        if (name != open.Name)
                            throw new Stop(
                                2,
                                where
                                    + ": `case-end` for case \""
                                    + name
                                    + "\" inside case \""
                                    + open.Name
                                    + "\""
                            );
                        long results = Int(rec, "results", where);
                        if (results != nextSeq)
                            throw new Stop(
                                2,
                                where + ": `results` " + results + ", but the case holds " + nextSeq
                            );
                        Int(rec, "pushed", where);
                        JsonElement errors = Arr(rec, "errors", where);
                        if (errors.GetArrayLength() > 0)
                            open.Failures.Add("`errors` " + errors.GetRawText());
                        open = null;
                        break;
                    }

                    case "end":
                    {
                        if (open != null)
                            throw new Stop(
                                2,
                                where + ": case \"" + open.Name + "\" has no `case-end`"
                            );
                        long cases = Int(rec, "cases", where);
                        if (cases != dump.Cases.Count)
                            throw new Stop(
                                2,
                                where
                                    + ": `cases` "
                                    + cases
                                    + ", but the dump frames "
                                    + dump.Cases.Count
                            );
                        Int(rec, "passed", where);
                        ended = true;
                        break;
                    }

                    default:
                        throw new Stop(2, where + ": unknown `kind` \"" + kind + "\"");
                }
            }

            if (open != null)
                throw new Stop(
                    2,
                    path + ":" + count + ": case \"" + open.Name + "\" has no `case-end`"
                );
            if (!ended)
                throw new Stop(2, path + ":" + count + ": no `end` record");
            return dump;
        }

        // A non-empty final's `payload.result`: required, each entry an object with a string
        // `word` and numeric `conf`, `start`, `end` (only empty-text finals may lack it).
        static Word[] ReadWords(JsonElement payload, string where)
        {
            JsonElement result = Arr(payload, "result", where);
            var words = new Word[result.GetArrayLength()];
            int i = 0;
            foreach (JsonElement e in result.EnumerateArray())
            {
                string at = where + ": `result`[" + i + "]";
                if (e.ValueKind != JsonValueKind.Object)
                    throw new Stop(2, at + ": not an object");
                words[i++] = new Word
                {
                    Text = Str(e, "word", at),
                    Conf = Float(e, "conf", at),
                    Start = Float(e, "start", at),
                    End = Float(e, "end", at),
                };
            }
            return words;
        }

        // --- corpus manifest --------------------------------------------------------------

        internal static Manifest ReadManifest(string path)
        {
            JsonElement root = ParseFile(path);
            var manifest = new Manifest { Path = path };
            bool hasCases = false;
            JsonElement cases = default;

            foreach (JsonProperty p in root.EnumerateObject())
            {
                switch (p.Name)
                {
                    case "voice":
                        break;
                    case "provenance":
                        if (p.Value.ValueKind != JsonValueKind.String)
                            throw new Stop(2, path + ": `provenance` is not a string");
                        manifest.Provenance = p.Value.GetString();
                        break;
                    case "cases":
                        if (p.Value.ValueKind != JsonValueKind.Array)
                            throw new Stop(2, path + ": `cases` is not an array");
                        cases = p.Value;
                        hasCases = true;
                        break;
                    default:
                        throw new Stop(2, path + ": unknown top-level key \"" + p.Name + "\"");
                }
            }
            if (manifest.Provenance == null)
                throw new Stop(2, path + ": no `provenance`");
            if (!hasCases)
                throw new Stop(2, path + ": no `cases`");

            var byName = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement c in cases.EnumerateArray())
            {
                string at = path + ": cases[" + index + "]";
                index++;
                if (c.ValueKind != JsonValueKind.Object)
                    throw new Stop(2, at + ": not an object");
                if (
                    !c.TryGetProperty("file", out JsonElement file)
                    || file.ValueKind != JsonValueKind.String
                )
                    throw new Stop(2, at + ": missing or mistyped key `file`");

                var mc = new ManifestCase { File = file.GetString(), Layer = "parser" };
                string named = path + ": case \"" + mc.File + "\"";
                foreach (JsonProperty p in c.EnumerateObject())
                {
                    if (!ManifestCaseKeys.Contains(p.Name))
                        throw new Stop(2, named + ": unknown key \"" + p.Name + "\"");
                    if (p.Name == "sealed")
                    {
                        if (
                            p.Value.ValueKind != JsonValueKind.True
                            && p.Value.ValueKind != JsonValueKind.False
                        )
                            throw new Stop(2, named + ": key `sealed` is not a boolean");
                        mc.Sealed = p.Value.GetBoolean();
                    }
                    else if (p.Name == "layer")
                    {
                        string layer =
                            p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                        if (layer != "parser" && layer != "recogniser")
                            throw new Stop(
                                2,
                                named
                                    + ": key `layer` is "
                                    + p.Value.GetRawText()
                                    + ", not \"parser\" or \"recogniser\""
                            );
                        mc.Layer = layer;
                    }
                    else if (p.Name == "expectedIntent")
                    {
                        if (p.Value.ValueKind != JsonValueKind.String)
                            throw new Stop(2, named + ": key `expectedIntent` is not a string");
                        mc.ExpectedIntent = p.Value.GetString();
                    }
                    else if (p.Name == "expectedSlots")
                        mc.ExpectedSlots = ReadExpectedSlots(p.Value, named);
                }
                // The scorer's inputs are required: an absent one is never a silent negative.
                if (mc.ExpectedIntent == null)
                    throw new Stop(2, named + ": no key `expectedIntent`");
                if (mc.ExpectedSlots == null)
                    throw new Stop(2, named + ": no key `expectedSlots`");

                mc.Name = mc.File.Substring(mc.File.LastIndexOf('/') + 1);
                if (!byName.Add(mc.Name))
                    throw new Stop(
                        2,
                        named + ": basename \"" + mc.Name + "\" shared with another case"
                    );
                manifest.Cases.Add(mc);
            }
            return manifest;
        }

        // `expectedSlots`: an array of objects, each exactly a string `name` and a string `value`.
        static KeyValuePair<string, string>[] ReadExpectedSlots(JsonElement value, string named)
        {
            string bad =
                named + ": key `expectedSlots` is not an array of {name, value} string pairs";
            if (value.ValueKind != JsonValueKind.Array)
                throw new Stop(2, bad);
            var slots = new KeyValuePair<string, string>[value.GetArrayLength()];
            int i = 0;
            foreach (JsonElement e in value.EnumerateArray())
            {
                int keys = 0;
                string name = null,
                    slotValue = null;
                if (e.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty p in e.EnumerateObject())
                    {
                        keys++;
                        if (p.Value.ValueKind != JsonValueKind.String)
                            continue;
                        if (p.Name == "name")
                            name = p.Value.GetString();
                        else if (p.Name == "value")
                            slotValue = p.Value.GetString();
                    }
                if (keys != 2 || name == null || slotValue == null)
                    throw new Stop(2, bad + " (entry " + i + ")");
                slots[i++] = new KeyValuePair<string, string>(name, slotValue);
            }
            return slots;
        }

        // --- pin (expectations.json) ------------------------------------------------------

        internal static Pin ReadPin(string path)
        {
            JsonElement root = ParseFile(path);
            foreach (JsonProperty p in root.EnumerateObject())
                if (Array.IndexOf(PinKeys, p.Name) < 0)
                    throw new Stop(2, path + ": unknown key \"" + p.Name + "\"");
            foreach (string key in PinKeys)
                if (!root.TryGetProperty(key, out _))
                    throw new Stop(2, path + ": no `" + key + "`");

            var pin = new Pin
            {
                Path = path,
                Gain = Num(root, "gain", path),
                Libvosk = Str(root, "libvosk", path),
                Grammar = StrArray(root, "grammar", path),
            };

            int index = 0;
            foreach (JsonElement c in Arr(root, "cases", path).EnumerateArray())
            {
                string at = path + ": cases[" + index + "]";
                index++;
                if (c.ValueKind != JsonValueKind.Object)
                    throw new Stop(2, at + ": not an object");
                foreach (JsonProperty p in c.EnumerateObject())
                    if (p.Name != "file" && p.Name != "expectedFinals")
                        throw new Stop(2, at + ": unknown key \"" + p.Name + "\"");
                string file = Str(c, "file", at);
                string[] finals = StrArray(c, "expectedFinals", at);
                if (pin.Cases.ContainsKey(file))
                    throw new Stop(2, path + ": case \"" + file + "\" repeated");
                pin.Cases.Add(file, finals);
                pin.Order.Add(file);
            }
            return pin;
        }

        // --- joins ------------------------------------------------------------------------

        // Case sets are the readers' (ruled 2026-10-14, "Exit 2, reader stop"): every dump case
        // in the manifest and the reverse; with `pinned`, the pin's and the dump's sets equal.
        internal static void Join(Dump dump, Manifest manifest, Pin pin, bool pinned)
        {
            var dumpNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (DumpCase c in dump.Cases)
                dumpNames.Add(c.Name);
            var manifestNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (ManifestCase c in manifest.Cases)
                manifestNames.Add(c.Name);

            var lines = new List<string>();
            foreach (DumpCase c in dump.Cases)
                if (!manifestNames.Contains(c.Name))
                    lines.Add(
                        dump.Path
                            + ": case \""
                            + c.Name
                            + "\" is not in the manifest "
                            + manifest.Path
                    );
            foreach (ManifestCase c in manifest.Cases)
                if (!dumpNames.Contains(c.Name))
                    lines.Add(
                        manifest.Path + ": case \"" + c.Name + "\" is not in the dump " + dump.Path
                    );
            if (pinned)
            {
                foreach (DumpCase c in dump.Cases)
                    if (!pin.Cases.ContainsKey(c.Name))
                        lines.Add(
                            dump.Path + ": case \"" + c.Name + "\" is not in the pin " + pin.Path
                        );
                foreach (string file in pin.Order)
                    if (!dumpNames.Contains(file))
                        lines.Add(
                            pin.Path
                                + ": case \""
                                + file
                                + "\" is not in the pinned dump "
                                + dump.Path
                        );
            }
            if (lines.Count > 0)
                throw new Stop(2, lines.ToArray());
        }

        // --- JSON helpers: a missing or mistyped field is a stop naming `where` --------------

        static JsonElement ParseObject(string line, string where)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(line))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        return doc.RootElement.Clone();
                }
            }
            catch (JsonException) { }
            throw new Stop(2, where + ": not a JSON object");
        }

        static JsonElement ParseFile(string path)
        {
            string text = File.ReadAllText(path, StrictUtf8);
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        throw new Stop(2, path + ": not a JSON object");
                    return doc.RootElement.Clone();
                }
            }
            catch (JsonException e)
            {
                string line = e.LineNumber.HasValue ? ":" + (e.LineNumber.Value + 1) : "";
                throw new Stop(2, path + line + ": " + e.Message);
            }
        }

        static JsonElement Field(JsonElement obj, string key, JsonValueKind kind, string where)
        {
            if (!obj.TryGetProperty(key, out JsonElement v) || v.ValueKind != kind)
                throw new Stop(2, where + ": missing or mistyped field `" + key + "`");
            return v;
        }

        static string Str(JsonElement obj, string key, string where) =>
            Field(obj, key, JsonValueKind.String, where).GetString();

        static double Num(JsonElement obj, string key, string where) =>
            Field(obj, key, JsonValueKind.Number, where).GetDouble();

        static float Float(JsonElement obj, string key, string where)
        {
            if (!Field(obj, key, JsonValueKind.Number, where).TryGetSingle(out float v))
                throw new Stop(2, where + ": missing or mistyped field `" + key + "`");
            return v;
        }

        static JsonElement Obj(JsonElement obj, string key, string where) =>
            Field(obj, key, JsonValueKind.Object, where);

        static JsonElement Arr(JsonElement obj, string key, string where) =>
            Field(obj, key, JsonValueKind.Array, where);

        static long Int(JsonElement obj, string key, string where)
        {
            if (!Field(obj, key, JsonValueKind.Number, where).TryGetInt64(out long v))
                throw new Stop(2, where + ": missing or mistyped field `" + key + "`");
            return v;
        }

        static bool Bool(JsonElement obj, string key, string where)
        {
            if (
                !obj.TryGetProperty(key, out JsonElement v)
                || (v.ValueKind != JsonValueKind.True && v.ValueKind != JsonValueKind.False)
            )
                throw new Stop(2, where + ": missing or mistyped field `" + key + "`");
            return v.GetBoolean();
        }

        static string[] StrArray(JsonElement obj, string key, string where)
        {
            JsonElement arr = Arr(obj, key, where);
            var result = new string[arr.GetArrayLength()];
            int i = 0;
            foreach (JsonElement e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String)
                    throw new Stop(2, where + ": missing or mistyped field `" + key + "`");
                result[i++] = e.GetString();
            }
            return result;
        }
    }
}
