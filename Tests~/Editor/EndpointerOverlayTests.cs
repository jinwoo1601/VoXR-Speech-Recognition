// ============================================================================
// Purpose:  EditMode tests for the endpointer overlay's rewrite rules, pristine copy,
//           atomic writes, preset table, resolution and load-time log line
// Layer:    Tests.Editor
// Owns:     EndpointerOverlayTests (public class)
// Depends:  EndpointerOverlay, VoxrEndpointerProfile, VoxrListeningMode, VoxrSpeechRecogniser
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VoXR;

namespace VoXR.Tests.Editor
{
    public class EndpointerOverlayTests
    {
        const string Rule2 = EndpointerOverlay.Rule2Key;
        const string Rule3 = EndpointerOverlay.Rule3Key;
        const string Rule4 = EndpointerOverlay.Rule4Key;

        const string PristineConf =
            "--min-active-rows=200\n" + Rule2 + "=0.5\n" + Rule3 + "=1.0\n" + Rule4 + "=2.0\n";

        static readonly EndpointerOverlay.Row TunedRow = new EndpointerOverlay.Row(
            "0.35",
            null,
            null
        );

        string _testDir;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(
                Application.temporaryCachePath,
                "VoxrTestOverlay_" + System.Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Test]
        public void Rewrite_ModelDefault_ReturnsPristineBytes()
        {
            string[] confs =
            {
                PristineConf,
                PristineConf.Replace("\n", "\r\n"),
                "﻿" + PristineConf,
                PristineConf.TrimEnd('\n'),
            };

            foreach (string conf in confs)
            {
                byte[] pristine = Bytes(conf);
                byte[] result = EndpointerOverlay.Rewrite(
                    pristine,
                    EndpointerOverlay.ModelDefault,
                    out string reason
                );

                Assert.IsNull(reason, $"A valid conf must parse: {Describe(conf)}");
                CollectionAssert.AreEqual(
                    pristine,
                    result,
                    $"Model default must yield the pristine bytes: {Describe(conf)}"
                );
            }
        }

        [Test]
        public void Rewrite_TunedRule2_ReplacesOneLine_KeepsTerminator()
        {
            foreach (string newline in new[] { "\n", "\r\n" })
            {
                string conf = PristineConf.Replace("\n", newline);
                string expected = conf.Replace(Rule2 + "=0.5", Rule2 + "=0.35");

                byte[] result = EndpointerOverlay.Rewrite(Bytes(conf), TunedRow, out string reason);

                Assert.IsNull(reason, $"A valid conf must parse: {Describe(conf)}");
                CollectionAssert.AreEqual(
                    Bytes(expected),
                    result,
                    $"Only rule2's line may change: {Describe(conf)}"
                );
            }
        }

        [Test]
        public void Rewrite_KeyLineWithLeadingWhitespace_IsMatched()
        {
            string conf = "--min-active-rows=200\n \t" + Rule2 + "=0.5\n";
            string expected = "--min-active-rows=200\n" + Rule2 + "=0.35\n";

            byte[] result = EndpointerOverlay.Rewrite(Bytes(conf), TunedRow, out string reason);

            Assert.IsNull(reason, "A valid conf must parse.");
            CollectionAssert.AreEqual(
                Bytes(expected),
                result,
                "An indented key line is the key's line: replaced in place, never appended a second time."
            );
        }

        [Test]
        public void Rewrite_MissingKeys_AppendedInRuleOrder_FinalTerminatorFirst()
        {
            var row = new EndpointerOverlay.Row("0.35", "1.5", "3.0");

            foreach (string newline in new[] { "\n", "\r\n" })
            {
                // rule3 alone present and no final terminator: rule2 and rule4 are appended,
                // in rule order, after a terminator the conf's own last line lacked.
                string conf = "--min-active-rows=200" + newline + Rule3 + "=1.0";
                string expected =
                    "--min-active-rows=200"
                    + newline
                    + Rule3
                    + "=1.5"
                    + newline
                    + Rule2
                    + "=0.35"
                    + newline
                    + Rule4
                    + "=3.0"
                    + newline;

                byte[] result = EndpointerOverlay.Rewrite(Bytes(conf), row, out string reason);

                Assert.IsNull(reason, $"A valid conf must parse: {Describe(conf)}");
                CollectionAssert.AreEqual(
                    Bytes(expected),
                    result,
                    $"Missing keys append in rule order with the conf's terminator: {Describe(conf)}"
                );
            }
        }

        [Test]
        public void Rewrite_ArchiveOwn_LeavesMissingKeyAbsent()
        {
            string conf = "--min-active-rows=200\n" + Rule2 + "=0.5\n";
            string expected = "--min-active-rows=200\n" + Rule2 + "=0.35\n";

            byte[] result = EndpointerOverlay.Rewrite(Bytes(conf), TunedRow, out string reason);

            Assert.IsNull(reason, "A valid conf must parse.");
            CollectionAssert.AreEqual(
                Bytes(expected),
                result,
                "A null row value is the archive's own: a key the conf lacks stays absent."
            );
        }

        [Test]
        public void Rewrite_InvalidUtf8_ReturnsCopyWithReason()
        {
            byte[] pristine = { 0x2D, 0x2D, 0x62, 0x65, 0x61, 0x6D, 0x3D, 0xFF, 0x0A };

            byte[] result = EndpointerOverlay.Rewrite(pristine, TunedRow, out string reason);

            Assert.AreEqual("conf is not valid UTF-8", reason);
            CollectionAssert.AreEqual(
                pristine,
                result,
                "An unparseable conf is served as the copy, unchanged."
            );
        }

        [Test]
        public void Rewrite_DuplicateKey_ReturnsCopyWithReason()
        {
            byte[] pristine = Bytes(PristineConf + Rule2 + "=0.6\n");

            byte[] result = EndpointerOverlay.Rewrite(pristine, TunedRow, out string reason);

            Assert.AreEqual(Rule2 + " appears more than once", reason);
            CollectionAssert.AreEqual(
                pristine,
                result,
                "An unparseable conf is served as the copy, unchanged."
            );
        }

        [Test]
        public void Rewrite_BadValue_ReturnsCopyWithReason()
        {
            foreach (string bad in new[] { "-0.5", "abc", "1e3", "" })
            {
                byte[] pristine = Bytes("--min-active-rows=200\n" + Rule2 + "=" + bad + "\n");

                byte[] result = EndpointerOverlay.Rewrite(pristine, TunedRow, out string reason);

                Assert.AreEqual($"{Rule2} value '{bad}' is not a non-negative decimal", reason);
                CollectionAssert.AreEqual(
                    pristine,
                    result,
                    $"An unparseable conf is served as the copy, unchanged: '{bad}'"
                );

                byte[] valid = Bytes(PristineConf);
                byte[] rowResult = EndpointerOverlay.Rewrite(
                    valid,
                    new EndpointerOverlay.Row(bad, null, null),
                    out string rowReason
                );

                Assert.AreEqual(
                    $"row {Rule2} value '{bad}' is not a non-negative decimal",
                    rowReason
                );
                CollectionAssert.AreEqual(
                    valid,
                    rowResult,
                    $"A bad row value leaves the copy unchanged: '{bad}'"
                );
            }
        }

        [Test]
        public void Apply_NoConf_WritesNoCopy()
        {
            var result = EndpointerOverlay.Apply(_testDir, TunedRow);

            Assert.AreEqual("no conf/model.conf", result.Reason);
            Assert.AreEqual(ConfPath, result.ConfPath);
            Assert.IsFalse(File.Exists(CopyPath), "With no conf there is nothing to copy.");
        }

        [Test]
        public void Apply_FirstApply_CapturesPreRewriteBytes()
        {
            WriteConf(Bytes(PristineConf));

            var result = EndpointerOverlay.Apply(_testDir, TunedRow);

            Assert.IsNull(result.Reason, "A valid conf must be applied.");
            CollectionAssert.AreEqual(
                Bytes(PristineConf),
                File.ReadAllBytes(CopyPath),
                "The copy must hold the conf as it was before the first rewrite."
            );
            CollectionAssert.AreEqual(
                Bytes(PristineConf.Replace(Rule2 + "=0.5", Rule2 + "=0.35")),
                File.ReadAllBytes(ConfPath),
                "The conf must carry the row."
            );
        }

        [Test]
        public void Apply_TunedThenModelDefault_RestoresPristine()
        {
            WriteConf(Bytes(PristineConf));

            Assert.IsNull(EndpointerOverlay.Apply(_testDir, TunedRow).Reason);
            var result = EndpointerOverlay.Apply(_testDir, EndpointerOverlay.ModelDefault);

            Assert.IsNull(result.Reason, "Model default must be applied.");
            CollectionAssert.AreEqual(
                Bytes(PristineConf),
                File.ReadAllBytes(ConfPath),
                "Model default computes from the copy, so a tuned conf returns to the archive's bytes."
            );
        }

        [Test]
        public void Apply_SteadyState_DoesNotWrite()
        {
            WriteConf(Bytes(PristineConf));
            Assert.IsNull(EndpointerOverlay.Apply(_testDir, TunedRow).Reason);

            var stale = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(ConfPath, stale);

            var result = EndpointerOverlay.Apply(_testDir, TunedRow);

            Assert.IsNull(result.Reason, "The same row must apply again.");
            Assert.AreEqual(
                stale,
                File.GetLastWriteTimeUtc(ConfPath),
                "A conf already holding the effective bytes must not be written."
            );
        }

        [Test]
        public void Apply_LeavesNoTempFile()
        {
            WriteConf(Bytes(PristineConf));

            Assert.IsNull(EndpointerOverlay.Apply(_testDir, TunedRow).Reason);

            CollectionAssert.IsEmpty(
                Directory.GetFiles(
                    _testDir,
                    "*" + EndpointerOverlay.TempSuffix,
                    SearchOption.AllDirectories
                ),
                "Every write must publish its temp file."
            );
        }

        [Test]
        public void Apply_UnparseableCopy_RestoresConfToCopy()
        {
            byte[] copy = Bytes(PristineConf + Rule2 + "=0.6\n");
            File.WriteAllBytes(CopyPath, copy);
            WriteConf(Bytes(PristineConf.Replace(Rule2 + "=0.5", Rule2 + "=0.35")));

            var result = EndpointerOverlay.Apply(_testDir, TunedRow);

            Assert.AreEqual(Rule2 + " appears more than once", result.Reason);
            CollectionAssert.AreEqual(
                copy,
                File.ReadAllBytes(ConfPath),
                "An unparseable copy is the effective conf."
            );
        }

        [Test]
        public void Apply_IoFailure_ReportsReason()
        {
            WriteConf(Bytes(PristineConf));
            Directory.CreateDirectory(ConfPath + EndpointerOverlay.TempSuffix);

            EndpointerOverlay.ApplyResult result = null;
            Assert.DoesNotThrow(() => result = EndpointerOverlay.Apply(_testDir, TunedRow));

            Assert.IsNotNull(result.Reason, "An IO failure must be reported.");
            StringAssert.StartsWith("IO failure: ", result.Reason);
            CollectionAssert.AreEqual(
                Bytes(PristineConf),
                File.ReadAllBytes(ConfPath),
                "A failed write must leave the conf on disk as it was."
            );
        }

        // -------- Presets, choice, resolution, log line --------

        [Test]
        public void Enum_IsExactlyThree()
        {
            CollectionAssert.AreEqual(
                new[] { "Automatic", "ModelDefault", "Responsive" },
                Enum.GetNames(typeof(VoxrEndpointerProfile)),
                "Members are appended, never renumbered or removed."
            );
            Assert.AreEqual(0, (int)VoxrEndpointerProfile.Automatic);
            Assert.AreEqual(1, (int)VoxrEndpointerProfile.ModelDefault);
            Assert.AreEqual(2, (int)VoxrEndpointerProfile.Responsive);
        }

        [Test]
        public void Table_Rows()
        {
            Assert.IsNull(
                EndpointerOverlay.RowOf(VoxrEndpointerProfile.Automatic),
                "Automatic has no row: the listening mode decides."
            );
            Assert.IsNull(
                EndpointerOverlay.RowOf((VoxrEndpointerProfile)99),
                "An undefined value reads as Automatic."
            );

            EndpointerOverlay.Row modelDefault = EndpointerOverlay.RowOf(
                VoxrEndpointerProfile.ModelDefault
            );
            Assert.AreSame(EndpointerOverlay.ModelDefault, modelDefault);
            Assert.IsNull(modelDefault.Rule2);
            Assert.IsNull(modelDefault.Rule3);
            Assert.IsNull(modelDefault.Rule4);

            EndpointerOverlay.Row responsive = EndpointerOverlay.RowOf(
                VoxrEndpointerProfile.Responsive
            );
            Assert.AreSame(EndpointerOverlay.Responsive, responsive);
            Assert.AreEqual("0.45", responsive.Rule2, "The Tuning record's rule2.");
            Assert.IsNull(responsive.Rule3, "rule3 is the archive's own.");
            Assert.IsNull(responsive.Rule4, "rule4 is the archive's own.");
        }

        [Test]
        public void ChoiceField_IsNonPublicEnum()
        {
            FieldInfo field = typeof(VoxrSpeechRecogniser).GetField(
                "endpointerProfile",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            Assert.IsNotNull(field, "The recogniser must carry the endpointerProfile field.");
            Assert.IsFalse(field.IsPublic, "The choice is a serialized field, not public API.");
            Assert.AreEqual(typeof(VoxrEndpointerProfile), field.FieldType);
            Assert.IsNotNull(
                field.GetCustomAttribute<SerializeField>(),
                "The choice must serialize."
            );
            Assert.AreEqual(
                "Endpointer preset, applied at model load; Automatic lets the listening mode decide.",
                field.GetCustomAttribute<TooltipAttribute>()?.tooltip
            );

            foreach (
                PropertyInfo property in typeof(VoxrSpeechRecogniser).GetProperties(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                )
            )
                Assert.AreNotEqual(
                    typeof(VoxrEndpointerProfile),
                    property.PropertyType,
                    $"No public property exposes the choice: {property.Name}"
                );
        }

        [Test]
        public void NoPublicFloat()
        {
            // The pre-feature set, read from Runtime/VoxrSpeechRecogniser.cs at merge-base
            // b255105: the endpointer adds no public float.
            string[] allowlist = { "InputLevel", "CreateSimulatedWords(confidence)" };

            CollectionAssert.AreEquivalent(
                allowlist,
                FloatSurface(typeof(VoxrSpeechRecogniser), typeof(float)),
                "The recogniser's public float members and parameters changed."
            );

            Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(VoxrEndpointerProfile)));

            foreach (Type type in typeof(VoxrSpeechRecogniser).Assembly.GetTypes())
            {
                if (!type.IsVisible)
                    continue;

                bool typeNamed = IsEndpointOrProfile(type.Name);
                foreach (string member in FloatSurface(type, typeof(float), typeof(double)))
                {
                    Assert.IsFalse(
                        typeNamed || IsEndpointOrProfile(member),
                        $"{type.FullName}.{member} exposes or takes a float or double."
                    );
                }
            }
        }

        [Test]
        public void Resolve_Explicit_WinsBothModes()
        {
            var explicitChoices = new[]
            {
                VoxrEndpointerProfile.ModelDefault,
                VoxrEndpointerProfile.Responsive,
            };

            foreach (VoxrEndpointerProfile choice in explicitChoices)
            {
                foreach (
                    VoxrListeningMode mode in new[]
                    {
                        VoxrListeningMode.Continuous,
                        VoxrListeningMode.PushToTalk,
                    }
                )
                {
                    var resolution = EndpointerOverlay.Resolve(choice, mode);

                    Assert.AreEqual(choice, resolution.Preset, $"{choice} in {mode}");
                    Assert.AreSame(EndpointerOverlay.RowOf(choice), resolution.Row);
                    Assert.AreEqual("explicit choice", resolution.Label);
                }
            }
        }

        [Test]
        public void Resolve_AutoContinuous_Responsive()
        {
            foreach (
                VoxrEndpointerProfile choice in new[]
                {
                    VoxrEndpointerProfile.Automatic,
                    (VoxrEndpointerProfile)99,
                }
            )
            {
                var resolution = EndpointerOverlay.Resolve(choice, VoxrListeningMode.Continuous);

                Assert.AreEqual(VoxrEndpointerProfile.Responsive, resolution.Preset);
                Assert.AreSame(EndpointerOverlay.Responsive, resolution.Row);
                Assert.AreEqual("Continuous default", resolution.Label);
            }
        }

        [Test]
        public void Resolve_AutoPushToTalk_ModelDefault()
        {
            var resolution = EndpointerOverlay.Resolve(
                VoxrEndpointerProfile.Automatic,
                VoxrListeningMode.PushToTalk
            );

            Assert.AreEqual(VoxrEndpointerProfile.ModelDefault, resolution.Preset);
            Assert.AreSame(EndpointerOverlay.ModelDefault, resolution.Row);
            Assert.AreEqual("Push-to-talk default", resolution.Label);
        }

        [Test]
        public void LogLine_Applied_ListsValues()
        {
            WriteConf(Bytes(PristineConf));

            var result = EndpointerOverlay.Apply(_testDir, EndpointerOverlay.Responsive);

            Assert.IsNull(result.Reason, "A valid conf must be applied.");
            Assert.AreEqual(
                "[VoxrSpeechRecogniser] Endpointer profile Responsive (explicit choice): "
                    + "rule2=0.45 rule3=1.0 rule4=2.0",
                EndpointerOverlay.LogLine(
                    VoxrEndpointerProfile.Responsive,
                    "explicit choice",
                    result
                )
            );
        }

        [Test]
        public void LogLine_MissingKey_Absent()
        {
            WriteConf(Bytes("--min-active-rows=200\n" + Rule2 + "=0.5\n" + Rule3 + "=1.0\n"));

            var result = EndpointerOverlay.Apply(_testDir, EndpointerOverlay.ModelDefault);

            Assert.IsNull(result.Reason, "A valid conf must be applied.");
            Assert.AreEqual(
                "[VoxrSpeechRecogniser] Endpointer profile ModelDefault (Continuous default): "
                    + "rule2=0.5 rule3=1.0 rule4=absent",
                EndpointerOverlay.LogLine(
                    VoxrEndpointerProfile.ModelDefault,
                    "Continuous default",
                    result
                )
            );
        }

        [Test]
        public void LogLine_NotApplied_NamesReason()
        {
            // A parse failure serves the copy, so the line carries the copy's real values:
            // the last of the two rule2 lines, as Kaldi reads it.
            File.WriteAllBytes(CopyPath, Bytes(PristineConf + Rule2 + "=0.6\n"));
            WriteConf(Bytes(PristineConf));

            var result = EndpointerOverlay.Apply(_testDir, EndpointerOverlay.Responsive);

            Assert.AreEqual(Rule2 + " appears more than once", result.Reason);
            Assert.AreEqual(
                "[VoxrSpeechRecogniser] Endpointer profile Responsive (explicit choice): "
                    + "rule2=0.6 rule3=1.0 rule4=2.0 — not applied: "
                    + Rule2
                    + " appears more than once ("
                    + ConfPath
                    + ")",
                EndpointerOverlay.LogLine(
                    VoxrEndpointerProfile.Responsive,
                    "explicit choice",
                    result
                )
            );
        }

        [Test]
        public void Apply_Tuned_CarriesValues()
        {
            WriteConf(Bytes(PristineConf));

            var result = EndpointerOverlay.Apply(_testDir, TunedRow);

            Assert.IsNull(result.Reason, "A valid conf must be applied.");
            Assert.AreEqual("0.35", result.Rule2, "The row's value, as written.");
            Assert.AreEqual("1.0", result.Rule3, "The archive's own, kept.");
            Assert.AreEqual("2.0", result.Rule4, "The archive's own, kept.");
        }

        [Test]
        public void Apply_ParseFailure_ValuesFromCopy()
        {
            byte[] copy = Bytes(PristineConf + Rule2 + "=0.6\n");
            File.WriteAllBytes(CopyPath, copy);
            WriteConf(Bytes(PristineConf.Replace(Rule2 + "=0.5", Rule2 + "=0.35")));

            var result = EndpointerOverlay.Apply(_testDir, TunedRow);

            Assert.IsNotNull(result.Reason, "An unparseable copy must not be applied.");
            CollectionAssert.AreEqual(copy, File.ReadAllBytes(ConfPath));
            Assert.AreEqual("0.6", result.Rule2, "The copy's last rule2 line wins.");
            Assert.AreEqual("1.0", result.Rule3);
            Assert.AreEqual("2.0", result.Rule4);
        }

        // The public float-typed members and parameters a type declares, as "Name" or
        // "Method(parameter)". Accessors are covered by their properties.
        static List<string> FloatSurface(Type type, params Type[] floatTypes)
        {
            var surface = new List<string>();
            const BindingFlags Declared =
                BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.DeclaredOnly;

            foreach (MemberInfo member in type.GetMembers(Declared))
            {
                switch (member)
                {
                    case FieldInfo field:
                        if (Array.IndexOf(floatTypes, field.FieldType) >= 0)
                            surface.Add(field.Name);
                        break;
                    case PropertyInfo property:
                        if (Array.IndexOf(floatTypes, property.PropertyType) >= 0)
                            surface.Add(property.Name);
                        foreach (ParameterInfo index in property.GetIndexParameters())
                            if (Array.IndexOf(floatTypes, index.ParameterType) >= 0)
                                surface.Add($"{property.Name}({index.Name})");
                        break;
                    case MethodBase method:
                        if (method is MethodInfo info && info.IsSpecialName)
                            break;
                        if (
                            method is MethodInfo returning
                            && Array.IndexOf(floatTypes, returning.ReturnType) >= 0
                        )
                            surface.Add(method.Name);
                        foreach (ParameterInfo parameter in method.GetParameters())
                            if (Array.IndexOf(floatTypes, parameter.ParameterType) >= 0)
                                surface.Add($"{method.Name}({parameter.Name})");
                        break;
                }
            }

            return surface;
        }

        static bool IsEndpointOrProfile(string name) =>
            name.IndexOf("Endpoint", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0;

        string ConfPath => Path.Combine(_testDir, "conf", "model.conf");

        string CopyPath => Path.Combine(_testDir, EndpointerOverlay.ArchiveCopyFileName);

        void WriteConf(byte[] bytes)
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "conf"));
            File.WriteAllBytes(ConfPath, bytes);
        }

        // No preamble: a BOM is in the bytes only when the string starts with U+FEFF.
        static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

        static string Describe(string conf) =>
            conf.Replace("﻿", "<BOM>").Replace("\r", "<CR>").Replace("\n", "<LF>");
    }
}
