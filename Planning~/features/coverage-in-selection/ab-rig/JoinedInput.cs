// ============================================================================
// Purpose:  Strict readers for the joined mode's inputs (harness dump, corpus manifest, pin) and their case joins
// Layer:    Planning.AbRig
// Owns:     JoinedInput (internal static class), JoinedInput.Stop (internal sealed class), JoinedInput.Word (internal sealed class), JoinedInput.Dump (internal sealed class), JoinedInput.DumpCase (internal sealed class), JoinedInput.DumpResult (internal sealed class), JoinedInput.Manifest (internal sealed class), JoinedInput.ManifestCase (internal sealed class), JoinedInput.Pin (internal sealed class)
// Depends:  (none)
// ============================================================================
// Every reader finishes before the guard, the kill check or any scoring looks at its
// output, and every input defect is a named stop: exit 2 with the file (and line, for the
// dump) or the case and key. Unknown dump record keys are ignored (`schema` versions the
// dump: 1, or 2 with the bridge positions, read strictly; feat-joined-rig-recogniser,
// *Schema 2*), save that a read with `pace` set reads `pace`, `wall`, `pushes` and `reads`
// strictly, refusing them for a fast dump and requiring them for a paced one
// (bench-paced-mode, *Stamps*); the manifest and the pin are read strictly
// (feat-joined-rig-parser, *Marks*).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AbRig
{
    static class JoinedInput
    {
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
            internal long Samples;
            internal long SampleRate;
            internal readonly List<string> Finals = new List<string>();
            internal readonly List<Word[]> Words = new List<Word[]>();
            internal readonly List<string> Failures = new List<string>();
            internal long Pushed;
            internal long? Consumed;
            internal readonly List<DumpResult> Results = new List<DumpResult>();
            internal List<(long End, long Due, long Made)> Pushes;
            internal List<(long Consumed, long At, long Pushed)> Reads;
        }

        internal sealed class DumpResult
        {
            internal long Seq;
            internal bool Final;
            internal long? Consumed;
            internal long? Origin;
            internal string Payload;
            internal string Raw;
            internal long? Wall;
        }

        internal sealed class Dump
        {
            internal string Path;
            internal JsonElement Header;
            internal long Schema;
            internal long? DecoderRate;
            internal double Gain;
            internal string Libvosk;
            internal string[] Grammar;
            internal string ModelConf;
            internal long? Pace;
            internal readonly List<DumpCase> Cases = new List<DumpCase>();
        }

        internal sealed class ManifestCase
        {
            internal string File;
            internal string Name;
            internal bool Sealed;
            internal string Layer;
            internal string Category = "";
            internal string FirstWord;
            internal string ExpectedIntent;
            internal KeyValuePair<string, string>[] ExpectedSlots;
            internal string Phrase;
            internal string[] Segments;
            internal (long Start, long End)[] Speech;
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
            internal string ModelConf;

            // file -> expectedFinals, in the pin's case order.
            internal readonly Dictionary<string, string[]> Cases = new Dictionary<string, string[]>(
                StringComparer.Ordinal
            );
            internal readonly List<string> Order = new List<string>();
        }

        internal static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

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
            "leadSilenceSeconds",
            "noiseFloorDbfs",
            "firstWord",
        };

        static readonly string[] PinKeys =
        {
            "_comment",
            "cases",
            "gain",
            "grammar",
            "libvosk",
            "modelConf",
        };

        internal static Dump ReadDump(string path) => ReadDump(path, null);

        internal static Dump ReadDump(string path, long? pace)
        {
            string text = File.ReadAllText(path, StrictUtf8);
            string[] lines = SplitLines(text);
            if (lines.Length == 0)
                throw new Stop(2, path + ": empty dump");

            var dump = new Dump { Path = path };
            var framed = new HashSet<string>(StringComparer.Ordinal);
            DumpCase open = null;
            long nextSeq = 0;
            bool ended = false;
            long lastConsumed = 0;
            long? caseOrigin = null;

            for (int i = 0; i < lines.Length; i++)
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
                    if (schema != 1 && schema != 2)
                        throw new Stop(2, where + ": `schema` " + schema + ", expected 1 or 2");
                    dump.Schema = schema;
                    if (schema == 2)
                    {
                        long decoderRate = Int(rec, "decoderRate", where);
                        if (decoderRate <= 0)
                            throw new Stop(
                                2,
                                where + ": `decoderRate` " + decoderRate + ", expected > 0"
                            );
                        dump.DecoderRate = decoderRate;
                    }
                    Str(rec, "manifest", where);
                    Str(rec, "fixtures", where);
                    Str(rec, "model", where);
                    dump.Gain = Num(rec, "gain", where);
                    dump.Libvosk = Str(rec, "libvosk", where);
                    dump.Grammar = StrArray(rec, "grammar", where);
                    if (
                        rec.TryGetProperty("modelConf", out JsonElement modelConf)
                        && modelConf.ValueKind != JsonValueKind.Null
                    )
                        dump.ModelConf = Str(rec, "modelConf", where);
                    Int(rec, "ringCapacity", where);
                    if (pace.HasValue)
                    {
                        if (rec.TryGetProperty("pace", out _))
                            dump.Pace = Int(rec, "pace", where);
                        if (pace.Value == 0 && dump.Pace.HasValue)
                            throw new Stop(
                                2,
                                where + ": `pace` " + dump.Pace.Value + ", expected none (a fast dump)"
                            );
                        if (pace.Value > 0 && !dump.Pace.HasValue)
                            throw new Stop(2, where + ": no `pace`, expected " + pace.Value);
                        if (dump.Pace.HasValue && dump.Pace.Value != pace.Value)
                            throw new Stop(
                                2,
                                where + ": `pace` " + dump.Pace.Value + ", expected " + pace.Value
                            );
                    }
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
                        long samples = Int(rec, "samples", where);
                        long sampleRate = Int(rec, "sampleRate", where);
                        if (!framed.Add(name))
                            throw new Stop(2, where + ": case \"" + name + "\" framed twice");
                        open = new DumpCase
                        {
                            Name = name,
                            Samples = samples,
                            SampleRate = sampleRate,
                        };
                        dump.Cases.Add(open);
                        nextSeq = 0;
                        lastConsumed = 0;
                        caseOrigin = null;
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
                        long pushed = Int(rec, "pushed", where);
                        Int(rec, "prevPushed", where);
                        var entry = new DumpResult { Seq = seq, Final = isFinal };
                        if (dump.Schema == 2)
                        {
                            long consumed = Int(rec, "consumed", where);
                            long origin = Int(rec, "origin", where);
                            if (consumed < 0)
                                throw new Stop(
                                    2,
                                    where + ": `consumed` " + consumed + ", expected >= 0"
                                );
                            if (origin < 0)
                                throw new Stop(
                                    2,
                                    where + ": `origin` " + origin + ", expected >= 0"
                                );
                            if (consumed < lastConsumed)
                                throw new Stop(
                                    2,
                                    where
                                        + ": `consumed` "
                                        + consumed
                                        + " below the previous result's "
                                        + lastConsumed
                                );
                            if (consumed > pushed)
                                throw new Stop(
                                    2,
                                    where + ": `consumed` " + consumed + " above `pushed` " + pushed
                                );
                            if (caseOrigin.HasValue && origin != caseOrigin.Value)
                                throw new Stop(
                                    2,
                                    where
                                        + ": `origin` "
                                        + origin
                                        + ", but the case's is "
                                        + caseOrigin.Value
                                );
                            lastConsumed = consumed;
                            caseOrigin = origin;
                            entry.Consumed = consumed;
                            entry.Origin = origin;
                        }
                        if (pace > 0)
                            entry.Wall = Int(rec, "wall", where);
                        else if (pace == 0 && rec.TryGetProperty("wall", out _))
                            throw WithoutPace(where, "wall");
                        open.Results.Add(entry);

                        if (rec.TryGetProperty("raw", out JsonElement raw))
                        {
                            if (raw.ValueKind != JsonValueKind.String)
                                throw new Stop(2, where + ": mistyped field `raw`");
                            entry.Raw = raw.GetString();
                            open.Failures.Add("unparseable result (`raw`) at seq " + seq);
                            break;
                        }
                        JsonElement payload = Obj(rec, "payload", where);
                        entry.Payload = payload.GetRawText();
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
                        open.Pushed = Int(rec, "pushed", where);
                        if (dump.Schema == 2)
                        {
                            long consumed = Int(rec, "consumed", where);
                            if (consumed < lastConsumed)
                                throw new Stop(
                                    2,
                                    where
                                        + ": `consumed` "
                                        + consumed
                                        + " below the last result's "
                                        + lastConsumed
                                );
                            open.Consumed = consumed;
                        }
                        JsonElement errors = Arr(rec, "errors", where);
                        if (errors.GetArrayLength() > 0)
                            open.Failures.Add("`errors` " + errors.GetRawText());
                        if (pace > 0)
                        {
                            open.Pushes = Triples(rec, "pushes", where);
                            open.Reads = Triples(rec, "reads", where);
                        }
                        else if (pace == 0)
                        {
                            if (rec.TryGetProperty("pushes", out _))
                                throw WithoutPace(where, "pushes");
                            if (rec.TryGetProperty("reads", out _))
                                throw WithoutPace(where, "reads");
                        }
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
                    path + ":" + lines.Length + ": case \"" + open.Name + "\" has no `case-end`"
                );
            if (!ended)
                throw new Stop(2, path + ":" + lines.Length + ": no `end` record");
            return dump;
        }

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
                    if (p.Name == "speech")
                    {
                        string bad =
                            named
                            + ": key `speech` is not an array of ascending {start, end} integer pairs";
                        if (p.Value.ValueKind != JsonValueKind.Array)
                            throw new Stop(2, bad);
                        var speech = new (long Start, long End)[p.Value.GetArrayLength()];
                        int i = 0;
                        long previous = 0;
                        foreach (JsonElement e in p.Value.EnumerateArray())
                        {
                            int keys = 0;
                            long? start = null,
                                end = null;
                            if (e.ValueKind == JsonValueKind.Object)
                                foreach (JsonProperty q in e.EnumerateObject())
                                {
                                    keys++;
                                    if (
                                        q.Value.ValueKind != JsonValueKind.Number
                                        || !q.Value.TryGetInt64(out long v)
                                    )
                                        continue;
                                    if (q.Name == "start")
                                        start = v;
                                    else if (q.Name == "end")
                                        end = v;
                                }
                            if (
                                keys != 2
                                || start == null
                                || end == null
                                || start.Value < 0
                                || start.Value >= end.Value
                                || start.Value < previous
                            )
                                throw new Stop(2, bad + " (entry " + i + ")");
                            speech[i++] = (start.Value, end.Value);
                            previous = end.Value;
                        }
                        mc.Speech = speech;
                        continue;
                    }
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
                    else if (p.Name == "category")
                    {
                        if (p.Value.ValueKind != JsonValueKind.String)
                            throw new Stop(2, named + ": key `category` is not a string");
                        mc.Category = p.Value.GetString();
                    }
                    else if (p.Name == "phrase")
                    {
                        if (p.Value.ValueKind != JsonValueKind.String)
                            throw new Stop(2, named + ": key `phrase` is not a string");
                        mc.Phrase = p.Value.GetString();
                    }
                    else if (p.Name == "segments")
                    {
                        string bad = named + ": key `segments` is not an array of strings";
                        if (p.Value.ValueKind != JsonValueKind.Array)
                            throw new Stop(2, bad);
                        var segments = new string[p.Value.GetArrayLength()];
                        int i = 0;
                        foreach (JsonElement e in p.Value.EnumerateArray())
                        {
                            if (e.ValueKind != JsonValueKind.String)
                                throw new Stop(2, bad);
                            segments[i++] = e.GetString();
                        }
                        mc.Segments = segments;
                    }
                    else if (p.Name == "leadSilenceSeconds" || p.Name == "noiseFloorDbfs")
                    {
                        if (p.Value.ValueKind != JsonValueKind.Number)
                            throw new Stop(2, named + ": key `" + p.Name + "` is not a number");
                    }
                    else if (p.Name == "firstWord")
                    {
                        string word =
                            p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                        if (
                            string.IsNullOrEmpty(word)
                            || Array.Exists(word.ToCharArray(), char.IsWhiteSpace)
                        )
                            throw new Stop(2, named + ": key `firstWord` is not a single word");
                        mc.FirstWord = word;
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
                if (mc.ExpectedIntent == null)
                    throw new Stop(2, named + ": no key `expectedIntent`");
                if (mc.ExpectedSlots == null)
                    throw new Stop(2, named + ": no key `expectedSlots`");
                if (mc.Category == "pause-lead" && mc.FirstWord == null)
                    throw new Stop(2, named + ": no key `firstWord` on a `pause-lead` case");
                if (mc.Category != "pause-lead" && mc.FirstWord != null)
                    throw new Stop(2, named + ": key `firstWord` on a case not `pause-lead`");

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
                ModelConf = Str(root, "modelConf", path),
            };

            int index = 0;
            foreach (JsonElement c in Arr(root, "cases", path).EnumerateArray())
            {
                string at = path + ": cases[" + index + "]";
                index++;
                if (c.ValueKind != JsonValueKind.Object)
                    throw new Stop(2, at + ": not an object");
                foreach (JsonProperty p in c.EnumerateObject())
                    if (
                        p.Name != "file"
                        && p.Name != "expectedFinals"
                        && p.Name != "frontEndDigest"
                        && p.Name != "frontEndSamples"
                    )
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

        internal static JsonElement ParseObject(string line, string where)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(line))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        RejectDuplicateKeys(doc.RootElement, where);
                        return doc.RootElement.Clone();
                    }
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
                    RejectDuplicateKeys(doc.RootElement, path);
                    return doc.RootElement.Clone();
                }
            }
            catch (JsonException e)
            {
                string line = e.LineNumber.HasValue ? ":" + (e.LineNumber.Value + 1) : "";
                throw new Stop(2, path + line + ": " + e.Message);
            }
        }

        static void RejectDuplicateKeys(JsonElement value, string where)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty p in value.EnumerateObject())
                {
                    if (!names.Add(p.Name))
                        throw new Stop(2, where + ": duplicate key `" + p.Name + "`");
                    RejectDuplicateKeys(p.Value, where);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement e in value.EnumerateArray())
                    RejectDuplicateKeys(e, where);
        }

        internal static string[] SplitLines(string text)
        {
            string[] lines = text.Split('\n');
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0)
                count--;
            Array.Resize(ref lines, count);
            return lines;
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

        static List<(long, long, long)> Triples(JsonElement obj, string key, string where)
        {
            var result = new List<(long, long, long)>();
            foreach (JsonElement e in Arr(obj, key, where).EnumerateArray())
            {
                var v = new long[3];
                int n = 0;
                if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() == 3)
                    foreach (JsonElement x in e.EnumerateArray())
                        if (x.ValueKind == JsonValueKind.Number && x.TryGetInt64(out v[n]))
                            n++;
                if (n != 3)
                    throw new Stop(2, where + ": missing or mistyped field `" + key + "`");
                result.Add((v[0], v[1], v[2]));
            }
            return result;
        }

        static Stop WithoutPace(string where, string key) =>
            new Stop(2, where + ": `" + key + "` without `pace` in the header");
    }
}
