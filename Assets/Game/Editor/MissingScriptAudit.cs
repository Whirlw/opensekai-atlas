using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    public static class MissingScriptAudit
    {
        private static readonly Regex ScriptReference = new Regex(
            @"^  m_Script: \{fileID: (?<id>-?\d+)(?:, guid: (?<guid>[a-f0-9]{32}), type: \d+)?\}",
            RegexOptions.Multiline);

        // An existing missing script is recorded as data for comparison.
        [MenuItem("OpenSekai/Dev/Audit Missing Scripts")]
        public static void Run()
        {
            const string output = "TestResults/missing-scripts.tsv";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var lines = new List<string> { "asset\tkind\tguid\tfileID\tcount" };
            var scripts = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
            var paths = AssetDatabase.GetAllAssetPaths().OrderBy(path => path, StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path))
                    continue;

                string extension = Path.GetExtension(path);
                int missingComponents;
                switch (extension)
                {
                    case ".unity":
                        var scene = EditorSceneManager.OpenPreviewScene(path);
                        try
                        {
                            missingComponents = scene.GetRootGameObjects().Sum(CountMissingComponents);
                        }
                        finally
                        {
                            EditorSceneManager.ClosePreviewScene(scene);
                        }
                        break;
                    case ".prefab":
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        missingComponents = prefab == null ? 0 : CountMissingComponents(prefab);
                        break;
                    case ".asset":
                        missingComponents = 0;
                        break;
                    default:
                        continue;
                }

                int unresolved = 0;
                foreach (var reference in UnresolvedScripts(path, scripts).GroupBy(reference => reference))
                {
                    lines.Add($"{path}\tscript\t{reference.Key.Guid}\t{reference.Key.FileId}\t{reference.Count()}");
                    unresolved += reference.Count();
                }

                if (missingComponents > unresolved)
                    lines.Add($"{path}\tcomponent\t\t\t{missingComponents - unresolved}");
            }

            File.WriteAllLines(output, lines);
            Debug.Log($"Missing script audit recorded {lines.Count - 1} rows in {output}.");
        }

        private static int CountMissingComponents(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
        }

        private static IEnumerable<(string Guid, long FileId)> UnresolvedScripts(
            string path, Dictionary<string, HashSet<long>> scripts)
        {
            using var reader = new StreamReader(path);
            var header = new char[5];
            if (reader.ReadBlock(header, 0, header.Length) != header.Length || new string(header) != "%YAML")
                yield break;

            foreach (Match match in ScriptReference.Matches(reader.ReadToEnd()))
            {
                long fileId = long.Parse(match.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture);
                string guid = match.Groups["guid"].Value;
                if (!scripts.TryGetValue(guid, out HashSet<long> ids))
                {
                    ids = new HashSet<long>();
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        foreach (MonoScript script in AssetDatabase.LoadAllAssetsAtPath(scriptPath).OfType<MonoScript>())
                        {
                            if (script.GetClass() != null &&
                                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string _, out long id))
                                ids.Add(id);
                        }
                    }
                    scripts.Add(guid, ids);
                }

                if (!ids.Contains(fileId))
                    yield return (guid, fileId);
            }
        }
    }
}
