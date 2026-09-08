// ============================================================================
// Purpose:  EditMode verification of the source-identity token's container resolution
// Layer:    Tests.Editor
// Owns:     ModelExtractorSourceTokenTests (public class)
// Depends:  ModelExtractor
// ============================================================================
using System.IO;
using NUnit.Framework;
using VoXR;

namespace VoXR.Tests.Editor
{
    // Issue #153: the cache's fast path stamps the identity of the file that *contains* the
    // model archive. Deriving that container from streamingAssetsPath rather than assuming
    // base.apk is what keeps Split Application Binary and Play Asset Delivery honest — the
    // archive then rides in an OBB or an asset pack that base.apk's mtime knows nothing
    // about. The parsing is pure string work, so it is pinned here off-device.
    public class ModelExtractorSourceTokenTests
    {
        const string ArchiveRelativePath = "vosk-model-small-en-us-0.15.zip";
        const string FallbackPath = "/data/app/fallback/base.apk";

        [Test]
        public void JarPath_ResolvesToApkContainer()
        {
            Assert.AreEqual(
                "/data/app/~~abc123/com.example.app-1/base.apk",
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file:///data/app/~~abc123/com.example.app-1/base.apk!/assets",
                    FallbackPath,
                    ArchiveRelativePath
                ),
                "An ordinary Android build must stamp the apk the assets are packed into, "
                    + "which is the file an install replaces."
            );
        }

        [Test]
        public void JarPath_SplitBinary_ResolvesToTheSplitContainer()
        {
            Assert.AreEqual(
                "/data/app/~~abc123/com.example.app-1/split_config.arm64_v8a.apk",
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file:///data/app/~~abc123/com.example.app-1/"
                        + "split_config.arm64_v8a.apk!/assets",
                    FallbackPath,
                    ArchiveRelativePath
                ),
                "The whole reason the container is derived: a model shipped in a new split "
                    + "or OBB leaves base.apk untouched, so stamping base.apk would serve a "
                    + "stale cache — the one failure direction this design forbids."
            );
        }

        [Test]
        public void PlainPath_ResolvesToTheArchiveFileItself()
        {
            string streamingAssets = Path.Combine("C:", "Project", "Assets", "StreamingAssets");

            Assert.AreEqual(
                Path.Combine(streamingAssets, ArchiveRelativePath),
                ModelExtractor.ResolveArchiveContainerPath(
                    streamingAssets,
                    FallbackPath,
                    ArchiveRelativePath
                ),
                "Off Android there is no container: the archive is a loose file, and its own "
                    + "size and mtime are the identity to stamp."
            );
        }

        [Test]
        public void MalformedJarPath_WithoutSeparator_FallsBack()
        {
            Assert.AreEqual(
                FallbackPath,
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file:///data/app/~~abc123/com.example.app-1/base.apk",
                    FallbackPath,
                    ArchiveRelativePath
                ),
                "An unrecognised shape must fall back whole rather than throw or hand back a "
                    + "truncated path — a token that fails to resolve costs a hash, never "
                    + "correctness."
            );
        }

        [Test]
        public void EmptyStreamingAssetsPath_FallsBack()
        {
            Assert.AreEqual(
                FallbackPath,
                ModelExtractor.ResolveArchiveContainerPath(null, FallbackPath, ArchiveRelativePath),
                "A path Unity never supplied cannot be parsed into anything; the fallback is "
                    + "stat-ed like any other candidate and degrades to the hash if it misses."
            );
        }

        [Test]
        public void JarPath_EmptyContainer_FallsBack()
        {
            Assert.AreEqual(
                FallbackPath,
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file://!/assets",
                    FallbackPath,
                    ArchiveRelativePath
                ),
                "An empty container names no file to stat, so it must fall back rather than "
                    + "stamp the identity of nothing."
            );
        }
    }
}
