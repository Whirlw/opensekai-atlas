using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    // Install beside ScreenshotSession.cs in the patched OpenSekai project.
    [InitializeOnLoad]
    internal static class PackEditorTimeline
    {
        private const string FolderKey = "OpenSekai.PackEditorTimeline.Folder";
        private const string Marker = "opensekai-timeline.txt";
        private static double nextScan;

        static PackEditorTimeline()
        {
            EditorApplication.update += Poll;
            EditorApplication.quitting += Disconnect;
        }

        [MenuItem("OpenSekai/Screenshots/Connect Pack Editor")]
        private static void Connect()
        {
            string root = EditorUtility.OpenFolderPanel("选择与资源包编辑器共用的定帧目录", "", "");
            if (string.IsNullOrEmpty(root)) return;
            if (root != SessionState.GetString(FolderKey, "")) Disconnect();
            File.WriteAllText(Path.Combine(root, Marker), "OpenSekai timeline v1");
            SessionState.SetString(FolderKey, root);
            Debug.Log("[Screenshots] Waiting for the pack editor in " + root);
        }

        [MenuItem("OpenSekai/Screenshots/Disconnect Pack Editor")]
        private static void Disconnect()
        {
            string root = SessionState.GetString(FolderKey, "");
            SessionState.EraseString(FolderKey);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                try { File.Delete(Path.Combine(root, Marker)); }
                catch (IOException error) { Debug.LogWarning(error.Message); }
                catch (UnauthorizedAccessException error) { Debug.LogWarning(error.Message); }
            }
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextScan) return;
            nextScan = EditorApplication.timeSinceStartup + 0.25;
            string root = SessionState.GetString(FolderKey, "");
            if (string.IsNullOrEmpty(root) || ScreenshotSession.IsBusy || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            try
            {
                foreach (string dir in Directory.EnumerateDirectories(root, "atlas-session-*"))
                {
                    foreach (string ready in Directory.EnumerateFiles(dir, "shot-*.ready"))
                    {
                        string id = Path.GetFileNameWithoutExtension(ready).Substring("shot-".Length);
                        if (!Guid.TryParseExact(id, "D", out _)) continue;
                        File.Delete(ready);
                        Start(dir, id);
                        return;
                    }
                }
            }
            catch (DirectoryNotFoundException) { if (!Directory.Exists(root)) Disconnect(); }
            catch (Exception error)
            {
                Disconnect();
                Debug.LogWarning("[Screenshots] Pack editor disconnected: " + error.Message);
            }
        }

        private static void Start(string dir, string id)
        {
            try
            {
                string path = Path.Combine(dir, "request.json");
                ShotRequest request = ShotRequest.Load(path);
                if (request.editorPreview?.id != id || request.quitWhenDone || request.shots.Count != 1 ||
                    request.width != 1920 || request.height != 1080)
                    throw new InvalidDataException("Invalid pack editor timeline request.");
                var shot = request.shots[0];
                if (shot.times?.Count != 1 || shot.to.HasValue || shot.from.HasValue || !string.IsNullOrEmpty(shot.name))
                    throw new InvalidDataException("Expected a single timeline frame.");
                ScreenshotSession.Start(path);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(dir, "request.status.json"),
                    JsonConvert.SerializeObject(new { status = "failed", message = error.Message }));
            }
        }
    }
}
