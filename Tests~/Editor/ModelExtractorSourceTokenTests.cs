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

        [Test]
        public void JarPath_ResolvesToApkContainer()
        {
            Assert.AreEqual(
                "/data/app/~~abc123/com.example.app-1/base.apk",
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file:///data/app/~~abc123/com.example.app-1/base.apk!/assets",
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
            // "C:" alone is drive-relative ("C:Project\..."), so the root separator is
            // spelled out: nothing here should depend on that quirk.
            string streamingAssets = Path.Combine(
                "C:" + Path.DirectorySeparatorChar,
                "Project",
                "Assets",
                "StreamingAssets"
            );

            Assert.AreEqual(
                Path.Combine(streamingAssets, ArchiveRelativePath),
                ModelExtractor.ResolveArchiveContainerPath(streamingAssets, ArchiveRelativePath),
                "Off Android there is no container: the archive is a loose file, and its own "
                    + "size and mtime are the identity to stamp."
            );
        }

        [Test]
        public void MalformedJarPath_WithoutSeparator_ResolvesToNothing()
        {
            Assert.IsNull(
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file:///data/app/~~abc123/com.example.app-1/base.apk",
                    ArchiveRelativePath
                ),
                "A jar shape the parser cannot read must yield no container at all: the caller "
                    + "turns a null container into a full read and hash, whereas any non-null "
                    + "guess would be stat-ed like a parsed one and could stamp the cache "
                    + "against a file nobody verified holds the archive."
            );
        }

        [Test]
        public void UnparseableJarPath_ResolvesToNothingEvenWhenItNamesARealContainer()
        {
            Assert.IsNull(
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file:///data/app/~~abc123/com.example.app-1/"
                        + "main.1.com.example.app.obb",
                    ArchiveRelativePath
                ),
                "Null is the contract for every shape the parser cannot read — not some other "
                    + "path, and not a plausible one. Whatever is returned gets stat-ed and "
                    + "stamped, so a container that merely looks right is still unverified: "
                    + "this is the case where the old Application.dataPath fallback stat-ed "
                    + "clean on Android, dataPath being the APK itself."
            );
        }

        [Test]
        public void EmptyStreamingAssetsPath_ResolvesToNothing()
        {
            Assert.IsNull(
                ModelExtractor.ResolveArchiveContainerPath(null, ArchiveRelativePath),
                "A path Unity never supplied cannot be parsed into anything, and nothing may "
                    + "be substituted for it — the launch pays a hash instead."
            );
        }

        [Test]
        public void JarPath_EmptyContainer_ResolvesToNothing()
        {
            Assert.IsNull(
                ModelExtractor.ResolveArchiveContainerPath(
                    "jar:file://!/assets",
                    ArchiveRelativePath
                ),
                "An empty container names no file to stat, so it must resolve to nothing "
                    + "rather than let some other file stand in for the identity of nothing."
            );
        }
    }
}
