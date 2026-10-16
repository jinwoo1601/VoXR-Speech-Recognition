// ============================================================================
// Purpose:  EditMode tests for the endpointer overlay's rewrite rules, pristine copy
//           and atomic writes
// Layer:    Tests.Editor
// Owns:     EndpointerOverlayTests (public class)
// Depends:  EndpointerOverlay
// ============================================================================
using System;
using System.IO;
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
