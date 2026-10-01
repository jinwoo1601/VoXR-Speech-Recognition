// The parser's ONLY UnityEngine dependency. Everything else in the staged sources is
// plain C#. Per project memory `grammar-ab-rig`.
using System;

namespace UnityEngine
{
    internal static class Debug
    {
        internal static void Log(object m) { }
        internal static void LogWarning(object m) { }
        internal static void LogError(object m) => Console.Error.WriteLine($"[error] {m}");
    }
}
