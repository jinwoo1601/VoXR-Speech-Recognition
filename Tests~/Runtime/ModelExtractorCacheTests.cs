// ============================================================================
// Purpose:  PlayMode tests for the model cache's two-tier freshness stamp — the cheap
//           source-identity token and the archive hash behind it
// Layer:    Tests.Runtime
// Owns:     ModelExtractorCacheTests (public class)
// Depends:  ModelExtractor, VoxrBridgeErrorCode
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoXR.Tests.Runtime
{
    // Issue #145: the cache used to key on the archive's leaf file name alone, so a
    // re-tuned model shipped under the same name was silently ignored on any device
    // that had already run the app. These tests pin the stamped-hash contract —
    // changed bytes re-extract, unchanged bytes do not.
    public class ModelExtractorCacheTests
    {
        const string ModelName = "testmodel";

        string _baseDir;
        List<string> _errors;

        [SetUp]
        public void SetUp()
        {
            _baseDir = Path.Combine(
                Application.temporaryCachePath,
                "VoxrTestCache_" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_baseDir);
            _errors = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_baseDir))
                Directory.Delete(_baseDir, true);
        }

        [UnityTest]
        public IEnumerator ChangedArchive_ReExtractsAndUpdatesStamp()
        {
            var first = Extract(BuildModelArchive("--beam=13"));
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            StringAssert.Contains(
                "--beam=13",
                ReadModelConf(path),
                "The first archive's decoder config must land in the extracted model."
            );
            string firstStamp = ReadStamp(path);

            var second = Extract(BuildModelArchive("--beam=11"));
            while (!second.IsCompleted)
                yield return null;

            Assert.IsNotNull(
                second.Result,
                $"Re-extraction must succeed. Errors: [{ErrorSummary}]"
            );
            StringAssert.Contains(
                "--beam=11",
                ReadModelConf(second.Result),
                "A changed archive must re-extract — the retuned model.conf is the whole "
                    + "point of issue #145."
            );
            Assert.AreNotEqual(
                firstStamp,
                ReadStamp(second.Result),
                "The stamp must track the new archive's hash, or the next launch would "
                    + "re-extract all over again."
            );
        }

        [UnityTest]
        public IEnumerator UnchangedArchive_KeepsExistingExtraction()
        {
            // The same byte array both times: identical bytes must hash identically.
            byte[] archive = BuildModelArchive("--beam=13");

            var first = Extract(archive);
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            string firstStamp = ReadStamp(path);

            // A re-extraction deletes the directory, so a sentinel inside it is the
            // reliable witness — far more so than a directory timestamp.
            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            var second = Extract(archive);
            while (!second.IsCompleted)
                yield return null;

            Assert.AreEqual(path, second.Result, "The cached path must be returned again.");
            Assert.IsTrue(
                File.Exists(sentinel),
                "An unchanged archive must not re-extract — the sentinel proves the "
                    + "cached directory survived untouched."
            );
            Assert.AreEqual(firstStamp, ReadStamp(path), "An untouched cache keeps its stamp.");
            Assert.IsEmpty(_errors, $"A cache hit must raise no error: [{ErrorSummary}]");
        }

        [UnityTest]
        public IEnumerator MissingStamp_ReExtractsExactlyOnce()
        {
            byte[] archive = BuildModelArchive("--beam=13");

            var first = Extract(archive);
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");

            // Simulate an install extracted before stamping existed.
            File.Delete(Path.Combine(path, ModelExtractor.StampFileName));
            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            var upgrade = Extract(archive);
            while (!upgrade.IsCompleted)
                yield return null;

            Assert.IsNotNull(
                upgrade.Result,
                $"The upgrade extraction must succeed. Errors: [{ErrorSummary}]"
            );
            Assert.IsFalse(
                File.Exists(sentinel),
                "A missing stamp must read as a mismatch and re-extract the model."
            );
            Assert.IsEmpty(_errors, $"The upgrade path must raise no error: [{ErrorSummary}]");
            Assert.IsTrue(
                File.Exists(Path.Combine(upgrade.Result, ModelExtractor.StampFileName)),
                "The re-extraction must leave a stamp behind."
            );

            // Second sentinel: the upgrade must cost one extraction, not one per launch.
            File.WriteAllText(sentinel, "sentinel");

            var steadyState = Extract(archive);
            while (!steadyState.IsCompleted)
                yield return null;

            Assert.IsTrue(
                File.Exists(sentinel),
                "Once stamped, the same archive must stop re-extracting."
            );
        }

        [UnityTest]
        public IEnumerator MissingArchive_WithValidCache_ReturnsCacheWithoutError()
        {
            var first = Extract(BuildModelArchive("--beam=13"));
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");

            var withoutArchive = Extract(null);
            while (!withoutArchive.IsCompleted)
                yield return null;

            Assert.AreEqual(
                path,
                withoutArchive.Result,
                "An unreadable archive must not cost the caller a valid cache — freshness "
                    + "is merely unverifiable."
            );
            Assert.IsEmpty(
                _errors,
                $"Serving the cache must stay silent, as it was before stamping: [{ErrorSummary}]"
            );
        }

        [UnityTest]
        public IEnumerator MissingArchive_WithoutCache_RaisesModelLoadFailed()
        {
            var task = Extract(null);
            while (!task.IsCompleted)
                yield return null;

            Assert.IsNull(task.Result, "A missing archive with no cache cannot yield a model.");
            Assert.AreEqual(1, _errors.Count, $"Expected exactly one failure: [{ErrorSummary}]");
            StringAssert.Contains(
                VoxrBridgeErrorCode.ModelLoadFailed.ToString(),
                _errors[0],
                "The failure code callers key on must be unchanged."
            );
        }

        [UnityTest]
        public IEnumerator CorruptArchive_FailsValidationAndLeavesNoCache()
        {
            var task = Extract(BuildModelArchive(null));
            while (!task.IsCompleted)
                yield return null;

            Assert.IsNull(task.Result, "An archive missing conf/model.conf must not validate.");
            Assert.AreEqual(1, _errors.Count, $"Expected exactly one failure: [{ErrorSummary}]");
            StringAssert.Contains(
                VoxrBridgeErrorCode.ModelLoadFailed.ToString(),
                _errors[0],
                "A corrupt archive must surface ModelLoadFailed."
            );
            Assert.IsFalse(
                Directory.Exists(Path.Combine(_baseDir, ModelName)),
                "A failed extraction must not publish a model directory."
            );
            Assert.IsFalse(
                Directory.Exists(Path.Combine(_baseDir, ".tmp_" + ModelName)),
                "A failed extraction must not leave its temp directory behind."
            );
        }

        [UnityTest]
        public IEnumerator CorruptArchive_WithValidCache_KeepsExistingModel()
        {
            var first = Extract(BuildModelArchive("--beam=13"));
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            string firstStamp = ReadStamp(path);

            // Different bytes, so the stamp mismatches and a re-extraction is attempted; no
            // conf/model.conf, so that replacement then fails structural validation.
            var corrupt = Extract(BuildModelArchive(null));
            while (!corrupt.IsCompleted)
                yield return null;

            Assert.IsNull(corrupt.Result, "A corrupt replacement archive cannot yield a model.");
            Assert.AreEqual(1, _errors.Count, $"Expected exactly one failure: [{ErrorSummary}]");
            StringAssert.Contains(
                VoxrBridgeErrorCode.ModelLoadFailed.ToString(),
                _errors[0],
                "A corrupt archive must surface ModelLoadFailed."
            );
            Assert.IsTrue(
                Directory.Exists(path),
                "The working model must survive a corrupt replacement — deleting the cache "
                    + "before the replacement exists leaves the device with no model at all."
            );
            Assert.IsTrue(
                ModelExtractor.ValidateModelDirectory(path),
                "The surviving cache must still be structurally valid, not a half-emptied shell."
            );
            StringAssert.Contains(
                "--beam=13",
                ReadModelConf(path),
                "The surviving cache must still hold the original model's contents."
            );
            Assert.AreEqual(
                firstStamp,
                ReadStamp(path),
                "The surviving cache keeps its own stamp, so a later good archive still "
                    + "reads as a mismatch and re-extracts."
            );
            Assert.IsFalse(
                Directory.Exists(Path.Combine(_baseDir, ".tmp_" + ModelName)),
                "A failed extraction must not leave its temp directory behind."
            );
        }

        [UnityTest]
        public IEnumerator ThrowingArchiveSource_WithValidCache_ReturnsCacheWithoutError()
        {
            var first = Extract(BuildModelArchive("--beam=13"));
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");

            // Throws at the call site rather than returning a faulted task, which pins that
            // the invocation itself — not just the await — sits inside the fallback's guard.
            var throwing = ExtractFrom(() => throw new IOException("locked"));
            while (!throwing.IsCompleted)
                yield return null;

            Assert.AreEqual(
                path,
                throwing.Result,
                "A read that throws must degrade to the same fallback as one returning null: "
                    + "a locked or permission-denied archive leaves freshness merely "
                    + "unverifiable, not the cache unusable."
            );
            Assert.IsEmpty(
                _errors,
                $"Serving the cache must stay silent, as the null path does: [{ErrorSummary}]"
            );
        }

        [UnityTest]
        public IEnumerator ThrowingArchiveSource_WithoutCache_RaisesModelLoadFailed()
        {
            // The faulted-task shape, matching production's async ReadStreamingAsset.
            var task = ExtractFrom(() => Task.FromException<byte[]>(new IOException("locked")));
            while (!task.IsCompleted)
                yield return null;

            Assert.IsNull(task.Result, "An unreadable archive with no cache cannot yield a model.");
            Assert.AreEqual(1, _errors.Count, $"Expected exactly one failure: [{ErrorSummary}]");
            StringAssert.Contains(
                VoxrBridgeErrorCode.ModelLoadFailed.ToString(),
                _errors[0],
                "The failure code callers key on must be unchanged."
            );
            StringAssert.Contains(
                "could not be read",
                _errors[0],
                "An unreadable archive must report as unreadable — a file that is present "
                    + "but locked is a different problem from one that was never shipped."
            );
            StringAssert.DoesNotContain(
                "not found in StreamingAssets",
                _errors[0],
                "The missing-archive wording stays pinned to the genuinely-missing case."
            );
        }

        [UnityTest]
        public IEnumerator MissingArchive_WithInvalidCache_SweepsCacheAndTemp()
        {
            // An unusable cache (am/ alone fails validation) plus a temp directory left
            // behind by an interrupted extraction — both dead weight on device storage.
            string cachePath = Path.Combine(_baseDir, ModelName);
            Directory.CreateDirectory(Path.Combine(cachePath, "am"));

            string tempPath = Path.Combine(_baseDir, ".tmp_" + ModelName);
            Directory.CreateDirectory(tempPath);
            File.WriteAllText(Path.Combine(tempPath, "partial.bin"), "partial");

            var task = Extract(null);
            while (!task.IsCompleted)
                yield return null;

            Assert.IsNull(
                task.Result,
                "A missing archive with no valid cache cannot yield a model."
            );
            Assert.AreEqual(1, _errors.Count, $"Expected exactly one failure: [{ErrorSummary}]");
            StringAssert.Contains(
                VoxrBridgeErrorCode.ModelLoadFailed.ToString(),
                _errors[0],
                "The failure code callers key on must be unchanged."
            );
            Assert.IsFalse(
                Directory.Exists(cachePath),
                "A cache that cannot validate is worthless and must be swept, as the "
                    + "pre-stamp code did on this path."
            );
            Assert.IsFalse(
                Directory.Exists(tempPath),
                "A stale temp must be swept too — otherwise a partial unpack sits on device "
                    + "storage indefinitely."
            );
        }

        // Issue #153: hashing the archive to answer "is the cache fresh?" meant reading the
        // whole ~39 MB of it on every launch — ~719 ms of a 1303 ms time-to-voice-ready on
        // a Quest 3. The stamp gained a second tier, a cheap source-identity token, and
        // these tests pin the two-tier contract: a matching token serves the cache with no
        // archive I/O at all; anything else falls back to the hash, which still decides
        // re-extraction exactly as issue #145 left it. The token may only ever
        // over-invalidate — a moved token costs one hash, never a re-extraction, and never
        // serves a cache the hash would have rejected.

        [UnityTest]
        public IEnumerator MatchingSourceToken_SkipsArchiveRead()
        {
            byte[] archive = BuildModelArchive("--beam=13");

            var first = ExtractWithToken(() => Task.FromResult(archive), () => "token-a");
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");

            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            // Deliberately different bytes: were the archive read at all, the hash would
            // mismatch and take the sentinel with it. Reaching the cache anyway is only
            // possible if the token answered the question on its own.
            var source = new CountingArchiveSource(BuildModelArchive("--beam=11"));

            var second = ExtractWithToken(source.Read, () => "token-a");
            while (!second.IsCompleted)
                yield return null;

            Assert.AreEqual(path, second.Result, "The cached path must be returned again.");
            Assert.AreEqual(
                0,
                source.Reads,
                "A matching token must serve the cache without touching the archive — the "
                    + "avoided read is the entire point of issue #153."
            );
            Assert.IsTrue(
                File.Exists(sentinel),
                "The fast path must not re-extract; the sentinel proves the cached directory "
                    + "survived untouched."
            );
            Assert.IsEmpty(_errors, $"A cache hit must raise no error: [{ErrorSummary}]");
        }

        [UnityTest]
        public IEnumerator MovedSourceToken_UnchangedBytes_RefreshesTokenWithoutReExtracting()
        {
            byte[] archive = BuildModelArchive("--beam=13");

            var first = ExtractWithToken(() => Task.FromResult(archive), () => "token-a");
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            string firstHash = ReadStampHash(path);

            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            // A reinstall of the same build moves the token without moving a single byte.
            var moved = new CountingArchiveSource(archive);

            var second = ExtractWithToken(moved.Read, () => "token-b");
            while (!second.IsCompleted)
                yield return null;

            Assert.AreEqual(path, second.Result, "The cached path must be returned again.");
            Assert.IsTrue(
                File.Exists(sentinel),
                "A moved token must cost a hash, never a re-extraction: the bytes the cache "
                    + "was built from have not changed."
            );
            Assert.AreEqual(
                1,
                moved.Reads,
                "The moved token must fall through to the hash — that fallback is what keeps "
                    + "the token from ever deciding freshness on its own."
            );
            Assert.AreEqual(
                firstHash,
                ReadStampHash(path),
                "The recorded hash describes bytes that did not change, so it must not either."
            );
            StringAssert.Contains(
                "src:token-b",
                ReadStamp(path),
                "The refreshed stamp must record the new token in place."
            );

            // The refresh is only worth anything if the next launch is actually cheap again.
            File.WriteAllText(sentinel, "sentinel");
            var settled = new CountingArchiveSource(archive);

            var third = ExtractWithToken(settled.Read, () => "token-b");
            while (!third.IsCompleted)
                yield return null;

            Assert.AreEqual(
                0,
                settled.Reads,
                "The in-place refresh must restore the fast path, not merely avoid breaking "
                    + "it — otherwise every launch after a reinstall keeps paying the hash."
            );
            Assert.IsTrue(File.Exists(sentinel), "The settled launch must not re-extract either.");
        }

        [UnityTest]
        public IEnumerator MovedSourceToken_ChangedBytes_ReExtracts()
        {
            var first = ExtractWithToken(
                () => Task.FromResult(BuildModelArchive("--beam=13")),
                () => "token-a"
            );
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            string firstHash = ReadStampHash(path);

            var second = ExtractWithToken(
                () => Task.FromResult(BuildModelArchive("--beam=11")),
                () => "token-b"
            );
            while (!second.IsCompleted)
                yield return null;

            Assert.IsNotNull(
                second.Result,
                $"Re-extraction must succeed. Errors: [{ErrorSummary}]"
            );
            StringAssert.Contains(
                "--beam=11",
                ReadModelConf(second.Result),
                "Adding the token tier must not cost issue #145's guarantee: changed bytes "
                    + "still re-extract."
            );
            Assert.AreNotEqual(
                firstHash,
                ReadStampHash(second.Result),
                "The stamp must track the new archive's hash, or the next launch would "
                    + "re-extract all over again."
            );
            StringAssert.Contains(
                "src:token-b",
                ReadStamp(second.Result),
                "The re-extraction must stamp the token that came with the new bytes."
            );
        }

        [UnityTest]
        public IEnumerator LegacyStamp_UpgradesInPlaceWithoutReExtracting()
        {
            byte[] archive = BuildModelArchive("--beam=13");

            // The five-argument form writes what issue #145 wrote: a hash and nothing else.
            var legacy = Extract(archive);
            while (!legacy.IsCompleted)
                yield return null;

            string path = legacy.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            string legacyStamp = ReadStamp(path);
            StringAssert.DoesNotContain(
                "src:",
                legacyStamp,
                "A token-less extraction must keep writing the one-line stamp, byte for byte "
                    + "what installs in the field already carry."
            );

            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            var upgrade = ExtractWithToken(() => Task.FromResult(archive), () => "token-a");
            while (!upgrade.IsCompleted)
                yield return null;

            Assert.AreEqual(path, upgrade.Result, "The cached path must be returned again.");
            Assert.IsTrue(
                File.Exists(sentinel),
                "Existing installs must upgrade to the token tier for the price of one hash — "
                    + "re-extracting 39 MB to write a second stamp line would be absurd."
            );
            StringAssert.Contains(
                legacyStamp,
                ReadStamp(path),
                "The upgraded stamp must keep the hash it already had; the bytes did not "
                    + "change just because the format gained a line."
            );
            StringAssert.Contains(
                "src:token-a",
                ReadStamp(path),
                "The upgrade is pointless unless the token is what the next launch reads."
            );
            Assert.IsEmpty(_errors, $"The upgrade path must raise no error: [{ErrorSummary}]");
        }

        [UnityTest]
        public IEnumerator NullSourceToken_FallsBackToHashing()
        {
            // A token provider can fail transiently — an unstattable source yields null —
            // and that must land on exactly the pre-#153 behaviour, not on a degraded one.
            byte[] archive = BuildModelArchive("--beam=13");

            var first = ExtractWithToken(() => Task.FromResult(archive), () => null);
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");
            StringAssert.DoesNotContain(
                "src:",
                ReadStamp(path),
                "A null token records nothing: writing one would claim an identity the "
                    + "provider never established."
            );

            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            var source = new CountingArchiveSource(archive);

            var second = ExtractWithToken(source.Read, () => null);
            while (!second.IsCompleted)
                yield return null;

            Assert.AreEqual(path, second.Result, "The cached path must be returned again.");
            Assert.AreEqual(
                1,
                source.Reads,
                "Without a token the archive must be read and hashed, exactly as it was "
                    + "before the fast path existed."
            );
            Assert.IsTrue(
                File.Exists(sentinel),
                "An unchanged archive must still hit the cache on the hash alone."
            );
            StringAssert.DoesNotContain(
                "src:",
                ReadStamp(path),
                "A null token must leave the stamp's format alone rather than erasing or "
                    + "inventing a token line."
            );
            Assert.IsEmpty(_errors, $"A cache hit must raise no error: [{ErrorSummary}]");
        }

        [UnityTest]
        public IEnumerator MissingStampToken_WithMatchingToken_StillHashes()
        {
            byte[] archive = BuildModelArchive("--beam=13");

            var first = ExtractWithToken(() => Task.FromResult(archive), () => "token-a");
            while (!first.IsCompleted)
                yield return null;

            string path = first.Result;
            Assert.IsNotNull(path, $"First extraction must succeed. Errors: [{ErrorSummary}]");

            // The adversarial stamp: a token with no hash beside it. Nothing in production
            // writes this, which is exactly why it has to be constructed by hand — it is the
            // shape a truncated write or a hand-edited stamp could leave behind.
            File.WriteAllText(Path.Combine(path, ModelExtractor.StampFileName), "src:token-a");

            string sentinel = Path.Combine(path, "marker.txt");
            File.WriteAllText(sentinel, "sentinel");

            var source = new CountingArchiveSource(archive);

            var second = ExtractWithToken(source.Read, () => "token-a");
            while (!second.IsCompleted)
                yield return null;

            Assert.IsNotNull(
                second.Result,
                $"The extraction must still succeed. Errors: [{ErrorSummary}]"
            );
            Assert.AreEqual(
                1,
                source.Reads,
                "A stamp carrying no hash describes a cache whose bytes were never verified "
                    + "against anything, so the token alone must never serve it."
            );
            Assert.IsFalse(
                File.Exists(sentinel),
                "With no recorded hash to match, the cache reads as stale and is rebuilt — "
                    + "the over-invalidating branch, which is the one to take when unsure."
            );
            StringAssert.Contains(
                "src:token-a",
                ReadStamp(second.Result),
                "The rebuilt cache must carry a complete stamp again, so the next launch is "
                    + "cheap rather than repeating this."
            );
        }

        Task<string> Extract(byte[] archiveBytes) =>
            ExtractFrom(() => Task.FromResult(archiveBytes));

        // The seam taken with an arbitrary source, so a throwing read can be injected.
        Task<string> ExtractFrom(Func<Task<byte[]>> archiveSource) =>
            ModelExtractor.ExtractModelAsync(
                ModelName,
                _baseDir,
                archiveSource,
                ModelName + ".zip",
                (code, msg) => _errors.Add($"{code}: {msg}")
            );

        // The same seam with a source-identity token injected. The nine issue-#145 tests go
        // on taking the five-argument overload, so they keep pinning the token-less path.
        Task<string> ExtractWithToken(Func<Task<byte[]>> archiveSource, Func<string> sourceToken) =>
            ModelExtractor.ExtractModelAsync(
                ModelName,
                _baseDir,
                archiveSource,
                ModelName + ".zip",
                (code, msg) => _errors.Add($"{code}: {msg}"),
                sourceToken
            );

        string ErrorSummary => string.Join("; ", _errors);

        static string ReadModelConf(string modelPath) =>
            File.ReadAllText(Path.Combine(modelPath, "conf", "model.conf"));

        // The stamp's raw text, so assertions can weigh the presence or absence of the
        // token line rather than only the hash it sits beside.
        static string ReadStamp(string modelPath) =>
            File.ReadAllText(Path.Combine(modelPath, ModelExtractor.StampFileName));

        static string ReadStampHash(string modelPath)
        {
            foreach (string rawLine in ReadStamp(modelPath).Split('\n'))
            {
                // Trimmed so the helper cannot start lying if the stamp ever picks up \r.
                string line = rawLine.Trim();
                if (line.Length > 0 && !line.StartsWith("src:", StringComparison.Ordinal))
                    return line;
            }

            return null;
        }

        // An archive source that records how often it was actually invoked, so a test can
        // assert zero archive I/O — the cache path coming back proves nothing on its own.
        sealed class CountingArchiveSource
        {
            readonly byte[] _archiveBytes;

            public CountingArchiveSource(byte[] archiveBytes) => _archiveBytes = archiveBytes;

            public int Reads { get; private set; }

            public Task<byte[]> Read()
            {
                Reads++;
                return Task.FromResult(_archiveBytes);
            }
        }

        // Builds a VOSK-shaped archive in memory. The entries sit under a root folder
        // because real VOSK archives have one and the extractor strips the first path
        // segment; graph/ needs a file inside it because directory entries are skipped.
        // A null modelConfContents omits conf/model.conf, producing a corrupt archive.
        static byte[] BuildModelArchive(string modelConfContents)
        {
            using var stream = new MemoryStream();

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "model-root/am/final.mdl", "stub");
                WriteEntry(archive, "model-root/conf/mfcc.conf", "stub");
                if (modelConfContents != null)
                    WriteEntry(archive, "model-root/conf/model.conf", modelConfContents);
                WriteEntry(archive, "model-root/graph/HCLG.fst", "stub");
            }

            return stream.ToArray();
        }

        static void WriteEntry(ZipArchive archive, string entryName, string contents)
        {
            var entry = archive.CreateEntry(entryName);
            // Pinned so two archives with the same contents hash the same: the entry
            // timestamp would otherwise default to "now" and vary between builds.
            entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using var writer = new StreamWriter(entry.Open());
            writer.Write(contents);
        }
    }
}
