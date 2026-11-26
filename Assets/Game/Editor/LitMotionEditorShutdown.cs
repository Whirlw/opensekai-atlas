using System.Reflection;
using LitMotion;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    internal static class LitMotionEditorShutdown
    {
        [InitializeOnLoadMethod]
        private static void Register() => EditorApplication.quitting += ReleaseAllocators;

        private static void ReleaseAllocators()
        {
            // LitMotion v2.0.2 installs its editor cleanup only on beforeAssemblyReload.
            // The editor can quit without a final reload. Reuse the owner's idempotent cleanup.
            var factory = typeof(LMotion).Assembly.GetType("LitMotion.RewindableAllocatorFactory");
            var dispose = factory?.GetMethod("Dispose", BindingFlags.Static | BindingFlags.NonPublic);
            if (dispose == null)
            {
                Debug.LogWarning("LitMotion's allocator cleanup changed. Review the editor shutdown hook.");
                return;
            }
            dispose.Invoke(null, null);
        }
    }
}
