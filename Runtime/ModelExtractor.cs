// ============================================================================
// Purpose:  Extracts ZIP-compressed VOSK models from StreamingAssets to persistent
//           storage, re-extracting whenever the archive's contents change — gated
//           behind a cheap source-identity check so an unchanged source is never read
// Layer:    Runtime
// Owns:     ModelExtractor (internal static class)
// Depends:  VoxrBridgeErrorCode
// ============================================================================
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VoXR
{
    internal static class ModelExtractor
    {
        const string ModelCacheFolder = "VoxrModels";
        internal const string StampFileName = ".voxr-model-stamp";
        const string SourceTokenPrefix = "src:";

        internal static Task<string> ExtractModelAsync(
            string modelRelativePath,
            Action<VoxrBridgeErrorCode, string> onError)
        {
            string archiveRelativePath = modelRelativePath + ".zip";

            return ExtractModelAsync(
                Path.GetFileName(modelRelativePath),
                Path.Combine(Application.persistentDataPath, ModelCacheFolder),
                () => ReadStreamingAsset(archiveRelativePath),
                archiveRelativePath,
                onError,
                () => ComputeSourceToken(archiveRelativePath)
            );
        }

        internal static async Task<string> ExtractModelAsync(
            string modelName,
            string basePath,
            Func<Task<byte[]>> archiveSource,
            string archiveDescription,
            Action<VoxrBridgeErrorCode, string> onError,
            Func<string> sourceTokenSource = null
        )
        {
            string finalPath = Path.Combine(basePath, modelName);
            string tempPath = Path.Combine(basePath, $".tmp_{modelName}");

            try
            {
                Directory.CreateDirectory(basePath);

                // Invoked synchronously, before the first await, so it runs on the main
                // thread where Application.dataPath and streamingAssetsPath are legal reads.
                string sourceToken = sourceTokenSource?.Invoke();

                // Tier 1 — source identity. Hashing the ~39 MB archive cost a Quest 3
                // ~719 ms of a 1303 ms time-to-voice-ready on every single launch (issue
                // #153), and when the source file is demonstrably the one this cache was
                // built from, that read buys nothing. The stamp must also carry a hash: a
                // stamp with no recorded hash describes a cache whose bytes were never
                // verified by anything, and serving it on the token alone would trust a
                // freshness claim nobody ever made.
                if (
                    sourceToken != null
                    && Directory.Exists(finalPath)
                    && ValidateModelDirectory(finalPath)
                )
                {
                    ReadStamp(finalPath, out string cachedKey, out string cachedToken);

                    // Returning here leaves any stale .tmp_<model> from an interrupted
                    // extraction untouched, exactly as the tier-2 cache hit below always
                    // has. A device that takes this path every launch therefore keeps that
                    // temp directory until something forces a re-extraction and sweeps it.
                    if (cachedKey != null && cachedToken == sourceToken)
                    {
                        // The one route out of here that reaches no await, and a completed
                        // task would let InitialiseAsync run vosk_bridge_init and raise
                        // OnModelReady inside the caller's own Initialise(), so a caller
                        // subscribing after that documented fire-and-forget call never hears
                        // the event. One yield forces async completion — not a #154 poll loop.
                        await Task.Yield();
                        return finalPath;
                    }
                }

                byte[] archiveBytes = null;
                string readFailure = null;

                try
                {
                    archiveBytes = await archiveSource();
                }
                catch (Exception ex)
                {
                    // A read that throws is no more fatal than one that returns null: both
                    // leave the cache's freshness unverifiable, and a valid cache is still a
                    // usable model. Degrade to the same fallback instead of failing outright.
                    readFailure = ex.Message;
                }

                if (archiveBytes == null)
                {
                    // Without the archive the cache's freshness cannot be checked, but a
                    // valid cache is still a usable model — and before stamping existed
                    // an unreadable archive was harmless whenever the cache was valid.
                    // Keep serving it silently rather than regressing into an error.
                    if (Directory.Exists(finalPath) && ValidateModelDirectory(finalPath))
                        return finalPath;

                    // No replacement is coming down this path, so restore the disk hygiene
                    // the pre-stamp code performed here: the cache has necessarily failed
                    // validation and is worthless, and a stale temp is pure dead weight.
                    if (Directory.Exists(finalPath))
                        Directory.Delete(finalPath, true);

                    if (Directory.Exists(tempPath))
                        Directory.Delete(tempPath, true);

                    string failureMessage = readFailure != null
                        ? $"Model archive could not be read: {archiveDescription} ({readFailure})"
                        : $"Model archive not found in StreamingAssets: {archiveDescription}";

                    onError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed, failureMessage);
                    return null;
                }

                string sourceKey = await Task.Run(() => ComputeArchiveKey(archiveBytes));

                // Tier 2 — archive hash. A missing stamp reads as a mismatch, so a pre-stamp
                // install re-extracts exactly once and is stamped from then on. The stale
                // cache is deliberately NOT deleted here: it stays as the working model
                // until its replacement is extracted, validated and stamped, so a corrupt
                // archive costs an error rather than the model the device already had.
                if (Directory.Exists(finalPath) && ValidateModelDirectory(finalPath))
                {
                    ReadStamp(finalPath, out string cachedKey, out string cachedToken);

                    if (cachedKey == sourceKey)
                    {
                        // The bytes are unchanged but the token moved — or predates tokens
                        // entirely, as every stamp issue #145 wrote does. Record it now so
                        // the next launch takes tier 1 instead of paying for this hash all
                        // over again; re-extraction would be pure waste, the cache is right.
                        // A null token means the provider failed transiently, so leave any
                        // recorded token alone rather than erasing a working fast path.
                        if (sourceToken != null && cachedToken != sourceToken)
                        {
                            try
                            {
                                WriteStamp(finalPath, sourceKey, sourceToken);
                            }
                            catch
                            {
                                // Swallowed deliberately: this whole body sits inside a catch
                                // that reports ModelLoadFailed and returns null, so an
                                // unwritable stamp would turn a perfectly good cache hit into
                                // a model-load failure. A failed refresh costs the next launch
                                // one hash and nothing else.
                            }
                        }

                        return finalPath;
                    }
                }

                // Clean up stale temp from interrupted extraction
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);

                await Task.Run(() =>
                {
                    using var stream = new MemoryStream(archiveBytes);
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        // Skip directory entries
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        // Strip the top-level folder from the archive if present.
                        // VOSK archives contain a root folder (e.g., "vosk-model-small-en-us-0.15/").
                        // We want the contents directly under tempPath.
                        string entryPath = entry.FullName;
                        int separatorIndex = entryPath.IndexOf('/');
                        if (separatorIndex >= 0)
                            entryPath = entryPath.Substring(separatorIndex + 1);

                        if (string.IsNullOrEmpty(entryPath))
                            continue;

                        string destinationPath = Path.GetFullPath(Path.Combine(tempPath, entryPath));
                        string fullTempPath = Path.GetFullPath(tempPath) + Path.DirectorySeparatorChar;

                        if (!destinationPath.StartsWith(fullTempPath, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"Zip entry escapes target directory: {entry.FullName}");

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                });

                if (!ValidateModelDirectory(tempPath))
                {
                    if (Directory.Exists(tempPath))
                        Directory.Delete(tempPath, true);

                    onError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                        "Extracted model failed structural validation. The archive may be corrupt.");
                    return null;
                }

                // Stamp inside the temp directory so the atomic rename below is what
                // publishes it: a stamped cache can only ever appear complete.
                WriteStamp(tempPath, sourceKey, sourceToken);

                // Surrender the old cache only now that its replacement is complete.
                if (Directory.Exists(finalPath))
                    Directory.Delete(finalPath, true);

                // Atomic rename
                Directory.Move(tempPath, finalPath);
                return finalPath;
            }
            catch (Exception ex)
            {
                try
                {
                    if (Directory.Exists(tempPath))
                        Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Best-effort cleanup
                }

                onError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                    $"Model extraction failed: {ex.Message}");
                return null;
            }
        }

        internal static bool ValidateModelDirectory(string path)
        {
            if (!Directory.Exists(path))
                return false;

            bool hasModel = File.Exists(Path.Combine(path, "am", "final.mdl"));
            bool hasConf = File.Exists(Path.Combine(path, "conf", "mfcc.conf"));
            bool hasModelConf = File.Exists(Path.Combine(path, "conf", "model.conf"));
            bool hasGraph = Directory.Exists(Path.Combine(path, "graph"));

            return hasModel && hasConf && hasModelConf && hasGraph;
        }

        // SHA-256 over the raw archive bytes: deterministic across platforms and
        // needs no dependency beyond the BCL.
        internal static string ComputeArchiveKey(byte[] archiveBytes)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(archiveBytes);

            var builder = new StringBuilder("sha256:", 7 + hash.Length * 2);
            foreach (byte b in hash)
                builder.Append(b.ToString("x2"));

            return builder.ToString();
        }

        // A cheap stand-in for the archive's identity, so an unchanged source can be
        // recognised without reading 39 MB of it. The token identifies *the file that
        // contains the archive*, whatever that file turns out to be: on Android the archive
        // is packed inside a container and cannot change without an install, and an install
        // always replaces the container. That is what makes a token this cheap sound.
        // (Device-verified: the app's own UID can stat and read its container, and
        // reinstalling a byte-identical build moves both the path and the mtime.
        // versionCode was tested and rejected — a rebuilt APK and a four-month-old build
        // both reported versionCode=1.)
        //
        // The container is derived, never assumed to be base.apk. Split Application Binary
        // and Play Asset Delivery both move StreamingAssets into an OBB or an install-time
        // asset pack, and a model shipped in a new one of those leaves base.apk untouched —
        // stamping base.apk's identity would then serve a stale cache, which is the one
        // failure direction this design is not allowed to have.
        //
        // That direction is deliberately one-sided: this over-invalidates, never under. A
        // token that moves while the bytes stay the same costs one extra hash and no
        // re-extraction; a token cannot fail to move when the bytes change.
        internal static string ComputeSourceToken(string archiveRelativePath)
        {
            try
            {
                string sourcePath = ResolveArchiveContainerPath(
                    Application.streamingAssetsPath,
                    Application.dataPath,
                    archiveRelativePath
                );

                if (string.IsNullOrEmpty(sourcePath))
                    return null;

                var info = new FileInfo(sourcePath);
                if (!info.Exists)
                    return null;

                return $"{sourcePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            }
            catch
            {
                // Any failure degrades to a null token, which means "hash it" — precisely
                // the pre-#153 behaviour, correct but slow.
                return null;
            }
        }

        // Android reports streamingAssetsPath as a jar URL naming the container and the
        // entry inside it, e.g.
        //   jar:file:///data/app/~~abc123/com.example.app-1/base.apk!/assets
        // so everything left of the '!' is the container's real file path — base.apk for an
        // ordinary build, the OBB or split apk when the assets were delivered separately.
        // Elsewhere (Editor, standalone) the path is an ordinary directory and the archive
        // file itself is the thing to stat.
        //
        // Kept free of Unity statics so the parsing is testable off-device. A shape this
        // does not recognise falls back rather than throwing or guessing: the fallback is
        // stat-ed like any other candidate, and a token that fails to resolve degrades to
        // the hash path, so a bad parse costs performance and never correctness.
        internal static string ResolveArchiveContainerPath(
            string streamingAssetsPath,
            string fallbackPath,
            string archiveRelativePath
        )
        {
            const string JarPrefix = "jar:file://";

            if (string.IsNullOrEmpty(streamingAssetsPath))
                return fallbackPath;

            if (!streamingAssetsPath.StartsWith(JarPrefix, StringComparison.Ordinal))
                return Path.Combine(streamingAssetsPath, archiveRelativePath);

            int separatorIndex = streamingAssetsPath.IndexOf('!');
            if (separatorIndex < 0)
                return fallbackPath;

            string container = streamingAssetsPath.Substring(
                JarPrefix.Length,
                separatorIndex - JarPrefix.Length
            );

            if (container.Length == 0)
                return fallbackPath;

            return Uri.UnescapeDataString(container);
        }

        // Both fields come back null when the stamp is missing or unreadable: an unreadable
        // stamp must mean "re-extract", never an exception. A one-line stamp written before
        // tokens existed therefore parses as (hash, null), which is exactly the signal tier
        // 2's in-place refresh keys on.
        static void ReadStamp(string modelPath, out string archiveKey, out string sourceToken)
        {
            archiveKey = null;
            sourceToken = null;

            try
            {
                string stampPath = Path.Combine(modelPath, StampFileName);
                if (!File.Exists(stampPath))
                    return;

                foreach (string rawLine in File.ReadAllLines(stampPath))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0)
                        continue;

                    if (line.StartsWith(SourceTokenPrefix, StringComparison.Ordinal))
                        sourceToken ??= line.Substring(SourceTokenPrefix.Length);
                    else
                        archiveKey ??= line;
                }
            }
            catch
            {
                // A stamp that is half-read is no more trustworthy than one that is missing.
                archiveKey = null;
                sourceToken = null;
            }
        }

        // Joined with a literal "\n" rather than Environment.NewLine so the format is
        // deterministic across platforms, and so a token-less stamp stays byte-identical to
        // what issue #145 wrote.
        static void WriteStamp(string modelPath, string archiveKey, string sourceToken)
        {
            string contents =
                sourceToken == null
                    ? archiveKey
                    : archiveKey + "\n" + SourceTokenPrefix + sourceToken;

            File.WriteAllText(Path.Combine(modelPath, StampFileName), contents);
        }

        static async Task<byte[]> ReadStreamingAsset(string relativePath)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            if (Application.platform == RuntimePlatform.Android)
            {
                // On Android, StreamingAssets is inside the APK — must use UnityWebRequest
                using var request = UnityWebRequest.Get(fullPath);
                var operation = request.SendWebRequest();

                // Await a completion callback rather than polling. Task.Yield() reposts the
                // continuation to Unity's synchronisation context, which reruns it inside
                // the same frame instead of releasing it: measured at 1 frame advanced
                // across 147.8 ms of polling, max frame delta 177.2 ms — roughly 16 dropped
                // frames at 90 Hz (issue #154). AsyncOperation.completed invokes its
                // delegate immediately if the operation has already finished, so there is
                // no missed-completion race; TrySetResult keeps a double invocation from
                // throwing. Note this removes the polling stall, not the array copy — the
                // downloadHandler.data read below is still ~19 ms of main-thread work for
                // the ~39 MB archive.
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                operation.completed += _ => completion.TrySetResult(true);
                await completion.Task;

                if (request.result != UnityWebRequest.Result.Success)
                    return null;

                return request.downloadHandler.data;
            }

            // On Editor / standalone, direct file access works
            if (!File.Exists(fullPath))
                return null;

            return await Task.Run(() => File.ReadAllBytes(fullPath));
        }
    }
}
