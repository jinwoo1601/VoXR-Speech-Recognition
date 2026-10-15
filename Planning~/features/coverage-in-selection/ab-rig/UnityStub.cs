// The parser's UnityEngine dependency (Debug), grown to the superset a recogniser stage compiles
// against (feat-joined-rig-recogniser, *Staging*); compile-only in a parser stage. Per project
// memory `grammar-ab-rig`.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    // No == overload: a reference compare is a live object's compare, and the rig never destroys
    // one behind a reference it still holds.
    public class Object
    {
        public string name { get; set; }
    }

    public enum SendMessageOptions
    {
        RequireReceiver = 0,
        DontRequireReceiver = 1,
    }

    public class MonoBehaviour : Object
    {
        // Set by the rig's player loop (JoinedRecogniser), as Unity sets it on enable and disable.
        public bool isActiveAndEnabled { get; internal set; }

        // Unity's messages reach a method by name whatever its visibility; a parameterless one only.
        public void SendMessage(string methodName, SendMessageOptions options)
        {
            for (Type t = GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(
                    methodName,
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly,
                    null,
                    Type.EmptyTypes,
                    null
                );
                if (m != null)
                {
                    m.Invoke(this, BindingFlags.DoNotWrapExceptions, null, null, null);
                    return;
                }
            }
            if (options == SendMessageOptions.RequireReceiver)
                Debug.LogError("SendMessage " + methodName + " has no receiver!");
        }
    }

    public class ScriptableObject : Object { }

    internal static class Time
    {
        public static float time { get; internal set; }
    }

    internal static class Debug
    {
        // Every LogError and failed Assert, in order, for the recogniser drive's guard.
        internal static readonly List<string> RigErrors = new List<string>();

        internal static bool isDebugBuild => true;

        internal static void Log(object m) { }

        internal static void LogWarning(object m) { }

        internal static void LogError(object m) => Record($"[error] {m}");

        internal static void LogError(object m, Object context) => Record($"[error] {m}");

        internal static void Assert(bool condition, string message)
        {
            if (!condition)
                Record($"[assert] {message}");
        }

        static void Record(string line)
        {
            Console.Error.WriteLine(line);
            RigErrors.Add(line);
        }
    }

    internal sealed class AddComponentMenuAttribute : Attribute
    {
        internal AddComponentMenuAttribute(string menuName) { }
    }

    internal sealed class SerializeFieldAttribute : Attribute { }

    internal sealed class TooltipAttribute : Attribute
    {
        internal TooltipAttribute(string tooltip) { }
    }

    internal sealed class HeaderAttribute : Attribute
    {
        internal HeaderAttribute(string header) { }
    }

    internal sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get; set; }
    }
}

namespace UnityEngine.Serialization
{
    internal sealed class FormerlySerializedAsAttribute : Attribute
    {
        internal FormerlySerializedAsAttribute(string oldName) { }
    }
}
