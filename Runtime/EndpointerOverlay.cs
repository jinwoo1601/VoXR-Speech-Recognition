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

        internal sealed class ApplyResult
        {
            public readonly string ConfPath,
                Reason;

            public readonly string Rule2,
                Rule3,
                Rule4;

            public readonly bool NothingToApply;

            public ApplyResult(
                string confPath,
                string reason,
                bool nothingToApply,
                byte[] effectiveConf
            )
            {
                ConfPath = confPath;
                Reason = reason;
                NothingToApply = nothingToApply;

                string[] values = KeyValues(effectiveConf);
                Rule2 = values[0];
                Rule3 = values[1];
                Rule4 = values[2];
            }
        }

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

            return new Resolution(
                VoxrEndpointerProfile.Responsive,
                Responsive,
                "Continuous default"
            );
        }

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
            {
                if (result.NothingToApply)
                    line +=
                        $" — nothing to apply, but the conf has a problem: {result.Reason} ({result.ConfPath})";
                else
                    line += $" — not applied: {result.Reason} ({result.ConfPath})";
            }

            return line;
        }

        internal const string ArchiveCopyFileName = ".voxr-archive-model.conf";
        internal const string TempSuffix = ".voxr-tmp";
        internal const string Rule2Key = "--endpoint.rule2.min-trailing-silence";
        internal const string Rule3Key = "--endpoint.rule3.min-trailing-silence";
        internal const string Rule4Key = "--endpoint.rule4.min-trailing-silence";

        static readonly string[] Keys = { Rule2Key, Rule3Key, Rule4Key };

        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        static readonly UTF8Encoding LenientUtf8 = new UTF8Encoding(false, false);

        const char ByteOrderMark = '﻿';

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

            string bom = "";
            if (text.Length > 0 && text[0] == ByteOrderMark)
            {
                bom = text.Substring(0, 1);
                text = text.Substring(1);
            }

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

        internal static ApplyResult Apply(string modelDir, Row row)
        {
            string confPath = Path.Combine(modelDir, "conf", "model.conf");
            bool empty = row.Rule2 == null && row.Rule3 == null && row.Rule4 == null;
            if (!File.Exists(confPath))
                return new ApplyResult(confPath, "no conf/model.conf", empty, null);

            string copyPath = Path.Combine(modelDir, ArchiveCopyFileName);
            string tempPath = null;

            try
            {
                if (!File.Exists(copyPath))
                {
                    tempPath = copyPath + TempSuffix;
                    File.WriteAllBytes(tempPath, File.ReadAllBytes(confPath));
                    Publish(tempPath, copyPath);
                    tempPath = null;
                }

                byte[] effective = Rewrite(File.ReadAllBytes(copyPath), row, out string reason);

                if (!File.ReadAllBytes(confPath).AsSpan().SequenceEqual(effective))
                {
                    tempPath = confPath + TempSuffix;
                    File.WriteAllBytes(tempPath, effective);
                    Publish(tempPath, confPath);
                    tempPath = null;
                }

                return new ApplyResult(confPath, reason, empty, effective);
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
                }

                byte[] onDisk = null;
                try
                {
                    onDisk = File.ReadAllBytes(confPath);
                }
                catch
                {
                }

                return new ApplyResult(confPath, $"IO failure: {e.Message}", false, onDisk);
            }
        }

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

        static void Publish(string tempPath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, null);
            else
                File.Move(tempPath, targetPath);
        }
    }
}
