using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Pure manifest transform used by the build rule and by the verification menu, so the rule can
    /// be checked without running an Android build.
    /// </summary>
    public static class RecordingPermissionStripper
    {
        /// <summary>Permissions that only the recording/video-export module needs.</summary>
        public static readonly string[] RecordingPermissions =
        {
            "android.permission.READ_EXTERNAL_STORAGE",
            "android.permission.WRITE_EXTERNAL_STORAGE",
            "android.permission.READ_MEDIA_IMAGES",
            "android.permission.READ_MEDIA_VIDEO",
            "android.permission.READ_MEDIA_AUDIO",
            "android.permission.MANAGE_EXTERNAL_STORAGE"
        };

        /// <summary>
        /// Removes plain recording-permission declarations and reports which ones were present.
        /// Declarations carrying tools:node="remove" are kept on purpose: the manifest merger uses
        /// them to strip permissions contributed by Unity's own generator and by AAR plugins.
        /// </summary>
        public static List<string> Strip(string manifest, out string result)
        {
            var removed = new List<string>();
            result = manifest ?? string.Empty;
            if (string.IsNullOrEmpty(result))
            {
                return removed;
            }

            string[] lines = result.Replace("\r\n", "\n").Split('\n');
            var kept = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                if (line.IndexOf("uses-permission", StringComparison.Ordinal) >= 0
                    && line.IndexOf("node=\"remove\"", StringComparison.Ordinal) < 0)
                {
                    string hit = null;
                    foreach (string permission in RecordingPermissions)
                    {
                        if (line.IndexOf(permission, StringComparison.Ordinal) >= 0)
                        {
                            hit = permission;
                            break;
                        }
                    }

                    if (hit != null)
                    {
                        removed.Add(hit);
                        continue;
                    }
                }

                kept.Add(line);
            }

            result = string.Join("\n", kept);
            return removed;
        }
    }

    /// <summary>
    /// Lightweight builds must not ask for gallery/storage permissions that only the recording
    /// module uses. Runs on the generated Gradle project manifest.
    /// </summary>
    public sealed class RecordingPermissionsBuildRule : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            try
            {
                // The callback receives the launcher module, but plugin manifests (Assets/Plugins/Android)
                // are merged into unityLibrary, so every manifest in the Gradle project must be processed.
                DirectoryInfo gradleRoot = Directory.GetParent(path) ?? new DirectoryInfo(path);
                if (!gradleRoot.Exists)
                {
                    Debug.LogWarning("[RecordingRule] Gradle project not found: " + gradleRoot.FullName);
                    return;
                }

                string[] manifests = Directory.GetFiles(gradleRoot.FullName, "AndroidManifest.xml", SearchOption.AllDirectories);
                int touched = 0;
                foreach (string manifestPath in manifests)
                {
                    string original = File.ReadAllText(manifestPath);
                    string stripped;
                    var removed = RecordingPermissionStripper.Strip(original, out stripped);
                    if (removed.Count == 0)
                    {
                        continue;
                    }

                    File.WriteAllText(manifestPath, stripped);
                    touched++;
                    Debug.Log("[RecordingRule] Removed recording permissions from "
                        + manifestPath.Substring(gradleRoot.FullName.Length).TrimStart(Path.DirectorySeparatorChar)
                        + ": " + string.Join(", ", removed));
                }

                Debug.Log("[RecordingRule] Scanned " + manifests.Length + " manifests, stripped " + touched + ".");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[RecordingRule] Could not strip recording permissions: " + exception.Message);
            }
        }
    }

    public static class RecordingPermissionCheck
    {
        [MenuItem("OpenSekai/Dev/Verify Recording Permission Rule")]
        public static void Verify()
        {
            string source = Path.Combine(Application.dataPath, "Plugins/Android/AndroidManifest.xml");
            if (!File.Exists(source))
            {
                throw new InvalidOperationException("Manifest not found: " + source);
            }

            string original = File.ReadAllText(source);
            string stripped;
            var removed = RecordingPermissionStripper.Strip(original, out stripped);

            int plainDeclarations = 0;
            foreach (string permission in RecordingPermissionStripper.RecordingPermissions)
            {
                foreach (Match match in Regex.Matches(original, "<uses-permission\\b[^>]*" + Regex.Escape(permission) + "[^>]*>"))
                {
                    if (match.Value.IndexOf("node=\"remove\"", StringComparison.Ordinal) < 0)
                    {
                        plainDeclarations++;
                    }
                }
            }

            int removeMarkers = 0;
            foreach (string permission in new[] { "android.permission.READ_EXTERNAL_STORAGE", "android.permission.WRITE_EXTERNAL_STORAGE" })
            {
                if (Regex.IsMatch(original, "<uses-permission\\b[^>]*" + Regex.Escape(permission) + "[^>]*node=\"remove\""))
                {
                    removeMarkers++;
                }
            }

            Debug.Log("[RecordingCheck] plainDeclarations=" + plainDeclarations
                + " removeMarkers=" + removeMarkers
                + " strippedByRule=" + removed.Count
                + " beforeBytes=" + original.Length
                + " afterBytes=" + stripped.Length);
            if (plainDeclarations != 0 || removeMarkers != 2)
            {
                throw new InvalidOperationException("[RecordingCheck] manifest must declare storage permissions only as tools:node=\"remove\"");
            }
        }
    }
}
