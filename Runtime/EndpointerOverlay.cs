// ============================================================================
// Purpose:  Resolving an endpointer preset and rewriting the endpoint trailing-silence
//           keys of an extracted model's conf/model.conf from a pristine copy of the
//           archive's own conf and the preset's row, through atomic writes, and composing
//           the load-time log line — Unity-free, so confgen compiles it unedited
// Layer:    Runtime
// Owns:     EndpointerOverlay (internal static class), EndpointerOverlay.Row (internal
//           sealed class), EndpointerOverlay.ApplyResult (internal sealed class),
//           EndpointerOverlay.Resolution (internal sealed class)
// Depends:  VoxrEndpointerProfile, VoxrListeningMode
// ============================================================================
using System;
using System.IO;
using System.Text;

namespace VoXR
{
    internal static class EndpointerOverlay
    {
        // One preset's values, each a decimal string or null for "the archive's own".
        // A plain class rather than a record: init accessors need IsExternalInit, which
        // netstandard 2.1 does not carry.
        internal sealed class Row
        {
            public readonly string Rule2,
                Rule3,
                Rule4;

            public Row(string rule2, string rule3, string rule4)
            {
                Rule2 = rule2;
                Rule3 = rule3;
                Rule4 = rule4;
            }
        }

        // Reason is null when the row was applied; otherwise it says why the effective
        // conf is the pristine copy (or, for an IO failure, whatever is on disk).
        internal sealed class ApplyResult
        {
            public readonly string ConfPath,
                Reason;

            // The effective conf's values, each null when its key is absent or the conf
            // could not be read.
            public readonly string Rule2,
                Rule3,
                Rule4;

            public ApplyResult(string confPath, string reason, byte[] effectiveConf)
            {
                ConfPath = confPath;
                Reason = reason;

                string[] values = KeyValues(effectiveConf);
                Rule2 = values[0];
                Rule3 = values[1];
                Rule4 = values[2];
            }
        }

        // A preset resolved for one load: the preset applied, its row, and why it was chosen.
        internal sealed class Resolution
        {
            public readonly VoxrEndpointerProfile Preset;
            public readonly Row Row;
            public readonly string Label;

            public Resolution(VoxrEndpointerProfile preset, Row row, string label)
            {
                Preset = preset;
                Row = row;
                Label = label;
            }
        }

        internal static readonly Row ModelDefault = new Row(null, null, null);
        internal static readonly Row Responsive = new Row("0.45", null, null);

        // The preset table. Automatic, and any value the enum does not define, has no row:
        // the listening mode decides.
        internal static Row RowOf(VoxrEndpointerProfile profile)
        {
            switch (profile)
            {
                case VoxrEndpointerProfile.ModelDefault:
                    return ModelDefault;
                case VoxrEndpointerProfile.Responsive:
                    return Responsive;
                default:
                    return null;
            }
        }

        // An explicit choice wins in either mode; otherwise the mode's default applies.
        internal static Resolution Resolve(VoxrEndpointerProfile choice, VoxrListeningMode mode)
        {
            Row row = RowOf(choice);
            if (row != null)
                return new Resolution(choice, row, "explicit choice");

            if (mode == VoxrListeningMode.PushToTalk)
                return new Resolution(
                    VoxrEndpointerProfile.ModelDefault,
                    ModelDefault,
                    "Push-to-talk default"
                );

            // Continuous: Phase 7's flip site, where the tuned preset becomes the default.
            return new Resolution(
                VoxrEndpointerProfile.ModelDefault,
                ModelDefault,
                "Continuous default"
            );
        }

        // One line per load. A missing value reads "absent" (Kaldi's default applies); a
        // row not applied keeps the values the decoder reads and names the reason.
        internal static string LogLine(
            VoxrEndpointerProfile preset,
            string label,
            ApplyResult result
        )
        {
            string line =
                $"[VoxrSpeechRecogniser] Endpointer profile {preset} ({label}): "
                + $"rule2={result.Rule2 ?? "absent"} "
                + $"rule3={result.Rule3 ?? "absent"} "
                + $"rule4={result.Rule4 ?? "absent"}";

            if (result.Reason != null)
                line += $" — not applied: {result.Reason} ({result.ConfPath})";

            return line;
        }

        internal const string ArchiveCopyFileName = ".voxr-archive-model.conf";
        internal const string TempSuffix = ".voxr-tmp";
        internal const string Rule2Key = "--endpoint.rule2.min-trailing-silence";
        internal const string Rule3Key = "--endpoint.rule3.min-trailing-silence";
        internal const string Rule4Key = "--endpoint.rule4.min-trailing-silence";

        static readonly string[] Keys = { Rule2Key, Rule3Key, Rule4Key };

        // Throws on invalid bytes and writes no preamble, so a valid conf decodes and
        // re-encodes byte for byte — a leading BOM included, as the U+FEFF it decodes to.
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // Never throws: an invalid sequence decodes to U+FFFD, and the key lines are ASCII.
        static readonly UTF8Encoding LenientUtf8 = new UTF8Encoding(false, false);

        const char ByteOrderMark = '﻿';

        // ASCII digits, optionally a dot and more digits — no sign, exponent, or culture,
        // so the check reads the same under Mono, IL2CPP and .NET 10.
        internal static bool IsDecimal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            int i = 0;
            int integerDigits = 0;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9')
            {
                i++;
                integerDigits++;
            }

            if (integerDigits == 0)
                return false;

            if (i == value.Length)
                return true;

            if (value[i] != '.')
                return false;

            i++;
            int fractionDigits = 0;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9')
            {
                i++;
                fractionDigits++;
            }

            return fractionDigits > 0 && i == value.Length;
        }

        // Pure: the effective conf for a pristine conf and a row. A key line is one whose
        // trimmed text begins "<key>="; a row value replaces that line's text, terminator
        // kept, or is appended in rule order when the conf lacks the key; every other byte
        // passes through. A conf that cannot be parsed comes back as pristine, with a reason.
        internal static byte[] Rewrite(byte[] pristine, Row row, out string reason)
        {
            reason = null;
            string[] rowValues = { row.Rule2, row.Rule3, row.Rule4 };

            for (int k = 0; k < Keys.Length; k++)
            {
                if (rowValues[k] != null && !IsDecimal(rowValues[k]))
                {
                    reason = $"row {Keys[k]} value '{rowValues[k]}' is not a non-negative decimal";
                    return pristine;
                }
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(pristine);
            }
            catch (DecoderFallbackException)
            {
                reason = "conf is not valid UTF-8";
                return pristine;
            }

            // The BOM passes through but stays outside line 1's key-line test.
            string bom = "";
            if (text.Length > 0 && text[0] == ByteOrderMark)
            {
                bom = text.Substring(0, 1);
                text = text.Substring(1);
            }

            // Split on '\n'; a '\r' before it belongs to the terminator. The last segment
            // has no terminator, and is no line at all when it is empty.
            string[] segments = text.Split('\n');
            int lineCount =
                segments[segments.Length - 1].Length == 0 ? segments.Length - 1 : segments.Length;

            string[] lines = new string[lineCount];
            string[] terminators = new string[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                string segment = segments[i];
                bool hasTerminator = i < segments.Length - 1;

                if (hasTerminator && segment.Length > 0 && segment[segment.Length - 1] == '\r')
                {
                    lines[i] = segment.Substring(0, segment.Length - 1);
                    terminators[i] = "\r\n";
                }
                else
                {
                    lines[i] = segment;
                    terminators[i] = hasTerminator ? "\n" : "";
                }
            }

            int[] keyLine = { -1, -1, -1 };
            for (int i = 0; i < lineCount; i++)
            {
                string trimmed = lines[i].Trim();

                for (int k = 0; k < Keys.Length; k++)
                {
                    string prefix = Keys[k] + "=";
                    if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    if (keyLine[k] >= 0)
                    {
                        reason = $"{Keys[k]} appears more than once";
                        return pristine;
                    }

                    string value = trimmed.Substring(prefix.Length).Trim();
                    if (!IsDecimal(value))
                    {
                        reason = $"{Keys[k]} value '{value}' is not a non-negative decimal";
                        return pristine;
                    }

                    keyLine[k] = i;
                }
            }

            var builder = new StringBuilder(bom, text.Length + 1 + 64 * Keys.Length);
            for (int i = 0; i < lineCount; i++)
            {
                string line = lines[i];
                for (int k = 0; k < Keys.Length; k++)
                {
                    if (keyLine[k] == i && rowValues[k] != null)
                        line = Keys[k] + "=" + rowValues[k];
                }

                builder.Append(line).Append(terminators[i]);
            }

            string newline = lineCount > 0 && terminators[0] == "\r\n" ? "\r\n" : "\n";
            bool finalTerminatorAdded = false;
            for (int k = 0; k < Keys.Length; k++)
            {
                if (keyLine[k] >= 0 || rowValues[k] == null)
                    continue;

                if (!finalTerminatorAdded)
                {
                    if (lineCount > 0 && terminators[lineCount - 1].Length == 0)
                        builder.Append(newline);
                    finalTerminatorAdded = true;
                }

                builder.Append(Keys[k]).Append('=').Append(rowValues[k]).Append(newline);
            }

            return StrictUtf8.GetBytes(builder.ToString());
        }

        // Applies a row to modelDir's conf/model.conf: captures the pristine copy before
        // the first write, computes from it, and replaces the conf only when its bytes
        // would change. Logs nothing and never throws; the caller reports the result.
        internal static ApplyResult Apply(string modelDir, Row row)
        {
            string confPath = Path.Combine(modelDir, "conf", "model.conf");
            if (!File.Exists(confPath))
                return new ApplyResult(confPath, "no conf/model.conf", null);

            string copyPath = Path.Combine(modelDir, ArchiveCopyFileName);
            string tempPath = null;

            try
            {
                // Only this overlay writes the conf, so an absent copy means the conf on
                // disk is still the archive's own.
                if (!File.Exists(copyPath))
                {
                    tempPath = copyPath + TempSuffix;
                    File.WriteAllBytes(tempPath, File.ReadAllBytes(confPath));
                    Publish(tempPath, copyPath);
                    tempPath = null;
                }

                byte[] effective = Rewrite(File.ReadAllBytes(copyPath), row, out string reason);

                if (!SameBytes(File.ReadAllBytes(confPath), effective))
                {
                    tempPath = confPath + TempSuffix;
                    File.WriteAllBytes(tempPath, effective);
                    Publish(tempPath, confPath);
                    tempPath = null;
                }

                return new ApplyResult(confPath, reason, effective);
            }
            catch (Exception e)
            {
                try
                {
                    if (tempPath != null && File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort cleanup
                }

                // The decoder reads whatever is on disk, so the values come from it.
                byte[] onDisk = null;
                try
                {
                    onDisk = File.ReadAllBytes(confPath);
                }
                catch
                {
                    // Best-effort read: an unreadable conf reports no values
                }

                return new ApplyResult(confPath, $"IO failure: {e.Message}", onDisk);
            }
        }

        // The rule2, rule3 and rule4 values of a conf, each null when its key is absent or
        // conf is null. A key's last occurrence wins, as Kaldi reads it; values trimmed.
        static string[] KeyValues(byte[] conf)
        {
            string[] values = new string[Keys.Length];
            if (conf == null)
                return values;

            string text = LenientUtf8.GetString(conf);
            if (text.Length > 0 && text[0] == ByteOrderMark)
                text = text.Substring(1);

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                for (int k = 0; k < Keys.Length; k++)
                {
                    string prefix = Keys[k] + "=";
                    if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                        values[k] = trimmed.Substring(prefix.Length).Trim();
                }
            }

            return values;
        }

        // A crash leaves the old file or the new one, never a truncated or missing target.
        static void Publish(string tempPath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, null);
            else
                File.Move(tempPath, targetPath);
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }
    }
}
