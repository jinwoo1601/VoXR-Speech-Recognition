// ============================================================================
// Purpose:  EditMode tests of the Editor's voxr-frontend.dll load probe and the plugin's import settings
// Layer:    Tests.Editor
// Owns:     FrontEndLoadTests (public class)
// Depends:  EditorMicBackend, FrontEndNative, VoxrBridgeErrorCode
// ============================================================================
#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using VoXR.Native;

namespace VoXR.Tests.Editor
{
    public class FrontEndLoadTests
    {
        const string PluginPath = "Packages/com.jinwoo1601.voxr/Runtime/Plugins/x86_64/voxr-frontend.dll";

        static void AssertProbeFails(Func<int> abiVersion)
        {
            var errors = new List<(VoxrBridgeErrorCode code, string message)>();

            bool ok = EditorMicBackend.ProbeFrontEnd(abiVersion, (c, m) => errors.Add((c, m)));

            Assert.IsFalse(ok);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(VoxrBridgeErrorCode.ModelLoadFailed, errors[0].code);
            StringAssert.Contains("voxr-frontend.dll", errors[0].message);
            StringAssert.Contains("Runtime/Plugins/x86_64/", errors[0].message);
        }

        [Test]
        public void ProbeFrontEnd_DllNotFound_FailsNamingDllAndFolder() =>
            AssertProbeFails(() => throw new DllNotFoundException("voxr-frontend"));

        [Test]
        public void ProbeFrontEnd_EntryPointNotFound_FailsNamingDllAndFolder() =>
            AssertProbeFails(() => throw new EntryPointNotFoundException("voxr_fe_abi_version"));

        [Test]
        public void ProbeFrontEnd_UnexpectedVersion_FailsNamingDllAndFolder() => AssertProbeFails(() => 2);

        [Test]
        public void ProbeFrontEnd_LoadedDll_Succeeds()
        {
            var errors = new List<string>();

            bool ok = EditorMicBackend.ProbeFrontEnd(FrontEndNative.voxr_fe_abi_version, (c, m) => errors.Add(m));

            Assert.IsTrue(ok);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void Plugin_EditorWindowsX64Only()
        {
            var importer = AssetImporter.GetAtPath(PluginPath) as PluginImporter;

            Assert.IsNotNull(importer, $"no PluginImporter at {PluginPath}");
            Assert.IsTrue(importer.GetCompatibleWithEditor());
            Assert.AreEqual("Windows", importer.GetEditorData("OS"));
            Assert.AreEqual("x86_64", importer.GetEditorData("CPU"));
            Assert.IsFalse(importer.GetCompatibleWithAnyPlatform());
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.Android));
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64));
        }
    }
}
#endif
