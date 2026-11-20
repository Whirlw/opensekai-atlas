using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Sekai.EditorTools
{
    /// <summary>
    /// Compares the serialized keys found in the prefabs under
    /// <c>Assets/Game/AssetBundles</c> against the fields the C# components declare today.
    ///
    /// A field that was renamed keeps its old key in the YAML, where it is silently ignored on
    /// load, so the prefab shows null at runtime while the sources look right. This audit lists
    /// those stale keys so the mismatch can be fixed in bulk instead of one symptom at a time.
    /// </summary>
    public static class PrefabFieldAudit
    {
        // The bundles are the ones that silently keep old field names; the note prefabs moved to
        // Resources after that same class of bug shipped to the device, so they are audited too.
        private static readonly string[] _roots =
        {
            "Assets/Game/AssetBundles",
            "Assets/Game/Resources/live/note"
        };
        private const string REPORT_PATH = "Logs/prefab-field-audit.txt";

        private static readonly HashSet<string> _unityManagedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier"
        };

        private static readonly Regex _componentStart = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex _scriptGuid = new Regex(@"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly Regex _directKey = new Regex(@"^  ([A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Compiled);

        [MenuItem("OpenSekai/Audit/Prefab Serialized Fields")]
        public static void RunFromMenu()
        {
            string report = Run();
            Debug.Log(report);
        }

        public static string Run()
        {
            var prefabList = new List<string>();
            for (int i = 0; i < _roots.Length; i++)
            {
                if (Directory.Exists(_roots[i]))
                {
                    prefabList.AddRange(Directory.GetFiles(_roots[i], "*.prefab", SearchOption.AllDirectories));
                }
            }

            string[] prefabs = prefabList.ToArray();
            Array.Sort(prefabs, StringComparer.Ordinal);

            var report = new StringBuilder();
            int staleTotal = 0;
            int missingTotal = 0;
            int componentsWithStale = 0;

            foreach (string prefab in prefabs)
            {
                string text = File.ReadAllText(prefab);
                var entries = ParseComponents(text);

                var prefabLines = new List<string>();
                foreach (ComponentEntry entry in entries)
                {
                    if (entry.Guid == null)
                    {
                        continue;
                    }

                    Type type = ResolveType(entry.Guid);
                    if (type == null)
                    {
                        prefabLines.Add("    <unknown script guid " + entry.Guid + ">");
                        continue;
                    }

                    HashSet<string> expected = CollectSerializedNames(type);
                    List<string> stale = entry.Keys.Where(key => !expected.Contains(key)).ToList();
                    List<string> missing = expected.Where(name => !entry.Keys.Contains(name)).ToList();

                    if (stale.Count == 0 && missing.Count == 0)
                    {
                        continue;
                    }

                    if (stale.Count > 0)
                    {
                        componentsWithStale++;
                        staleTotal += stale.Count;
                    }

                    missingTotal += missing.Count;

                    prefabLines.Add("  " + type.Name);
                    if (stale.Count > 0)
                    {
                        prefabLines.Add("    stale  : " + string.Join(", ", stale));
                    }

                    if (missing.Count > 0)
                    {
                        prefabLines.Add("    missing: " + string.Join(", ", missing));
                    }
                }

                if (prefabLines.Count == 0)
                {
                    continue;
                }

                report.AppendLine(prefab.Replace('\\', '/'));
                foreach (string line in prefabLines)
                {
                    report.AppendLine(line);
                }

                report.AppendLine();
            }

            var header = new StringBuilder();
            header.AppendLine("# Prefab serialized field audit");
            header.AppendLine("# prefabs scanned: " + prefabs.Length);
            header.AppendLine("# components with stale keys: " + componentsWithStale);
            header.AppendLine("# stale keys: " + staleTotal);
            header.AppendLine("# missing keys (field exists, prefab has no value): " + missingTotal);
            header.AppendLine();

            string result = header.ToString() + report.ToString();
            string directory = Path.GetDirectoryName(REPORT_PATH);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(REPORT_PATH, result);
            return result;
        }

        private static List<ComponentEntry> ParseComponents(string text)
        {
            var entries = new List<ComponentEntry>();
            ComponentEntry current = null;
            bool inComponent = false;

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                Match start = _componentStart.Match(trimmed);
                if (start.Success)
                {
                    current = null;
                    inComponent = start.Groups[1].Value == "114";
                    if (inComponent)
                    {
                        current = new ComponentEntry();
                        entries.Add(current);
                    }

                    continue;
                }

                if (!inComponent || current == null)
                {
                    continue;
                }

                Match script = _scriptGuid.Match(trimmed);
                if (script.Success)
                {
                    current.Guid = script.Groups[1].Value;
                    continue;
                }

                Match key = _directKey.Match(trimmed);
                if (key.Success)
                {
                    string name = key.Groups[1].Value;
                    if (!_unityManagedKeys.Contains(name))
                    {
                        current.Keys.Add(name);
                    }
                }
            }

            return entries;
        }

        private static Type ResolveType(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            return script == null ? null : script.GetClass();
        }

        private static HashSet<string> CollectSerializedNames(Type type)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            // Unity serializes the private fields of base classes too, but reflection only returns a
            // derived type's own private fields, so walk the hierarchy explicitly. Without this the
            // inherited fields of shared views report as stale keys.
            for (Type current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(object); current = current.BaseType)
            {
                FieldInfo[] declared = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declared.Length; i++)
                {
                    FieldInfo field = declared[i];
                    if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                    {
                        continue;
                    }

                    bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true);
                    if (!serialized || field.IsDefined(typeof(NonSerializedAttribute), inherit: true))
                    {
                        continue;
                    }

                    names.Add(field.Name);
                }
            }

            return names;
        }

        private sealed class ComponentEntry
        {
            public string Guid;
            public readonly List<string> Keys = new List<string>();
        }
    }
}
