// ============================================================================
// Purpose:  Command-line front end that writes an overlaid model for the harness
//           through the package's own EndpointerOverlay source
// Layer:    NativeBridge.Harness.Confgen
// Owns:     Program (internal static class)
// Depends:  EndpointerOverlay, VoxrEndpointerProfile, VoxrListeningMode
// ============================================================================
//
// Usage:
//   confgen --model <dir> --out <dir> (--profile <name> [--mode Continuous|PushToTalk] | --rule2 <v> [--rule3 <v>] [--rule4 <v>])
//
// Copies --model to --out when --out does not exist (an existing --out is the cached
// path and is not copied over), applies the row to --out's conf/model.conf, and prints
// "sha256 <64 lowercase hex>" of the resulting conf followed by its --endpoint.rule
// lines. The row is the preset --profile resolves to in --mode (default Continuous),
// as the recogniser resolves it at load, or the --rule values. A name is an exact
// enum member name; each value is ASCII [0-9]+(.[0-9]+)?. Exit 0 on success; 1 when
// the copy fails or the row is not applied; 2 on a usage problem, before anything is
// written.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using VoXR;

namespace VoXR.Confgen
{
    internal static class Program
    {
        const string UsageLine =
            "usage: confgen --model <dir> --out <dir> (--profile <name> [--mode Continuous|PushToTalk] | --rule2 <v> [--rule3 <v>] [--rule4 <v>])";

        static readonly string[] Flags =
        {
            "--model",
            "--out",
            "--rule2",
            "--rule3",
            "--rule4",
            "--profile",
            "--mode",
        };

        static int Main(string[] args)
        {
            // Every stdout line ends in "\n" on every platform: the sweep compares runs
            // byte for byte, and hash.py reads line 1.
            Console.Out.NewLine = "\n";

            string[] values = new string[Flags.Length];
            for (int i = 0; i < args.Length; i++)
            {
                int flag = Array.IndexOf(Flags, args[i]);
                if (flag < 0)
                    return Usage($"unknown flag '{args[i]}'");

                if (values[flag] != null)
                    return Usage($"repeated flag {Flags[flag]}");

                if (i + 1 >= args.Length || args[i + 1].Length == 0)
                    return Usage($"missing value for {Flags[flag]}");

                values[flag] = args[++i];
            }

            for (int flag = 0; flag < 2; flag++)
            {
                if (values[flag] == null)
                    return Usage($"missing flag {Flags[flag]}");
            }

            bool profileGiven = values[5] != null;
            bool rulesGiven = values[2] != null || values[3] != null || values[4] != null;

            if (profileGiven && rulesGiven)
                return Usage("--profile and --rule2/--rule3/--rule4 are exclusive");

            if (!profileGiven && !rulesGiven)
                return Usage("missing flag --profile or --rule2");

            if (rulesGiven && values[2] == null)
                return Usage("--rule3 and --rule4 need --rule2");

            if (values[6] != null && !profileGiven)
                return Usage("--mode needs --profile");

            for (int flag = 2; flag < 5; flag++)
            {
                if (values[flag] != null && !EndpointerOverlay.IsDecimal(values[flag]))
                    return Usage(
                        $"{Flags[flag]} value '{values[flag]}' is not a non-negative decimal"
                    );
            }

            // Exact member names only: an integer, or a name in another case, is refused.
            EndpointerOverlay.Row row = null;
            if (profileGiven)
            {
                if (Array.IndexOf(Enum.GetNames(typeof(VoxrEndpointerProfile)), values[5]) < 0)
                    return Usage($"--profile '{values[5]}' is not a profile name");

                var mode = VoxrListeningMode.Continuous;
                if (values[6] != null)
                {
                    if (Array.IndexOf(Enum.GetNames(typeof(VoxrListeningMode)), values[6]) < 0)
                        return Usage($"--mode '{values[6]}' is not a listening mode name");

                    mode = Enum.Parse<VoxrListeningMode>(values[6]);
                }

                row = EndpointerOverlay
                    .Resolve(Enum.Parse<VoxrEndpointerProfile>(values[5]), mode)
                    .Row;
            }

            string model = values[0];
            if (!Directory.Exists(model))
                return Usage($"--model directory not found: {model}");

            string modelFull = FullDirectoryPath(model);
            string outFull = FullDirectoryPath(values[1]);

            if (string.Equals(outFull, modelFull, StringComparison.OrdinalIgnoreCase))
                return Usage($"--out {outFull} is the same directory as --model {modelFull}");

            if (IsInside(outFull, modelFull))
                return Usage($"--out {outFull} is inside --model {modelFull}");

            if (IsInside(modelFull, outFull))
                return Usage($"--model {modelFull} is inside --out {outFull}");

            if (!Directory.Exists(outFull))
            {
                try
                {
                    CopyDirectory(modelFull, outFull);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"confgen: copy failed: {e.Message}");
                    return 1;
                }
            }

            if (row == null)
                row = new EndpointerOverlay.Row(values[2], values[3], values[4]);

            EndpointerOverlay.ApplyResult result = EndpointerOverlay.Apply(outFull, row);

            // A row that was not applied fails the run: a sweep must never decode the
            // pristine conf under a candidate's name.
            if (result.Reason != null)
            {
                Console.Error.WriteLine(
                    $"confgen: not applied: {result.Reason} ({result.ConfPath})"
                );
                return 1;
            }

            byte[] conf = File.ReadAllBytes(result.ConfPath);
            Console.Out.WriteLine("sha256 " + Convert.ToHexStringLower(SHA256.HashData(conf)));

            foreach (string line in Encoding.UTF8.GetString(conf).Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("--endpoint.rule", StringComparison.Ordinal))
                    Console.Out.WriteLine(trimmed);
            }

            return 0;
        }

        static int Usage(string problem)
        {
            Console.Error.WriteLine($"confgen: {problem}");
            Console.Error.WriteLine(UsageLine);
            return 2;
        }

        static string FullDirectoryPath(string path) =>
            Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        static bool IsInside(string path, string directory) =>
            path.StartsWith(
                directory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );

        static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (
                string directory in Directory.GetDirectories(
                    source,
                    "*",
                    SearchOption.AllDirectories
                )
            )
                Directory.CreateDirectory(
                    Path.Combine(target, Path.GetRelativePath(source, directory))
                );

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
