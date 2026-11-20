using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Packages;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>Persistent download tracking; only extraction/validation runs on a worker.</summary>
    public sealed class ServerChartDownloads : MonoBehaviour
    {
        public static ServerChartDownloads Current { get; private set; }
        public event Action<string> StatusChanged;
#pragma warning disable CS0067 // Raised by the Android-only import path.
        public event Action Imported;
#pragma warning restore CS0067
        private readonly HashSet<string> _attempted = new HashSet<string>();
        private Task _import;
        private float _progress;
        private string _lastStatus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Current != null) return;
            var host = new GameObject("ServerChartDownloads");
            DontDestroyOnLoad(host);
            Current = host.AddComponent<ServerChartDownloads>();
        }

        /// <summary>
        /// Opens a chart website in the system WebView (its own Android activity, with a back
        /// button in its bar). Downloads started there are picked up and imported on return.
        /// </summary>
        /// <param name="url">HTTPS page to open: the address of a server the player added.</param>
        /// <param name="title">Shown in the browser's bar.</param>
        public static void Open(string url, string title = null)
        {
            Initialize();
            Current._lastStatus = null;
            if (string.IsNullOrEmpty(url))
            {
                Current.Report("没有要打开的网址。");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Current._attempted.Clear(); // Retained archives can be retried by opening Server again.
                using var plugin = new AndroidJavaClass("com.opensekai.MiniWebView");
                plugin.CallStatic("open", url, title ?? string.Empty);
            }
            catch (Exception e) { Current.Report("无法打开谱面服务器：" + e.Message); }
#else
            Application.OpenURL(url);
            Current.Report("内置网页下载仅支持 Android；已在系统浏览器中打开，下载的曲包请用曲库“导入”。");
#endif
        }

        private IEnumerator Start()
        {
            while (true)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { Poll(); }
                catch (Exception e) { Report("下载状态查询失败：" + e.Message); }
#endif
                yield return new WaitForSecondsRealtime(1f);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void Poll()
        {
            if (_import != null && !_import.IsCompleted)
            {
                Report("正在解压导入 " + Mathf.RoundToInt(Volatile.Read(ref _progress) * 100) + "%");
                return;
            }
            using var plugin = new AndroidJavaClass("com.opensekai.MiniWebView");
            foreach (JObject item in JArray.Parse(plugin.CallStatic<string>("query")))
            {
                string id = (string)item["id"];
                int status = (int)item["status"];
                long bytes = (long?)item["bytes"] ?? 0;
                long total = (long?)item["total"] ?? -1;
                if (bytes > SafeZipExtractor.MAX_TOTAL_BYTES || total > SafeZipExtractor.MAX_TOTAL_BYTES)
                {
                    plugin.CallStatic("cancel", id);
                    Report("曲包下载超过 2 GiB，已取消。");
                    continue;
                }
                if (status == 8 && !_attempted.Contains(id))
                {
                    _attempted.Add(id);
                    _progress = 0;
                    _import = ImportAsync(id, (string)item["path"]);
                    return; // Serialize imports and shared library-cache updates.
                }
                if (status == 16 && _attempted.Add(id))
                {
                    Report("谱面下载失败（" + item["reason"] + "），请在服务器中重新下载。");
                    plugin.CallStatic("cancel", id);
                }
                else if (status == 4) Report("下载已暂停，等待网络恢复（" + item["reason"] + "）");
                else if (status == 1 || status == 2)
                    Report(total > 0 ? $"谱面下载中 {bytes * 100 / total}%" : $"谱面下载中 {bytes / 1024} KiB");
            }
        }

        private async Task ImportAsync(string id, string path)
        {
            try
            {
                string downloads = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Downloads")) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(path).StartsWith(downloads, StringComparison.Ordinal))
                    throw new InvalidDataException("下载文件位于应用下载目录之外。");
                // Resolve Shared on Unity's thread before starting work (its paths use Unity APIs).
                var manager = SongPackageManager.Shared;
                await manager.ImportDownloadedZipAsync(path, id, value => Volatile.Write(ref _progress, value));
                using var plugin = new AndroidJavaClass("com.opensekai.MiniWebView");
                plugin.CallStatic("acknowledge", id);
                Imported?.Invoke();
                Report("谱面已自动导入曲库");
                try { File.Delete(path); } catch (IOException e) { Debug.LogWarning(e.Message); }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerChartDownloads] " + e);
                Report("自动导入失败：" + e.Message + "。重新打开服务器可重试。");
            }
        }
#endif

        private void Report(string message)
        {
            if (_lastStatus == message) return;
            _lastStatus = message;
            StatusChanged?.Invoke(message);
            Debug.Log("[ServerChartDownloads] " + message);
        }

        private void OnDestroy() { if (Current == this) Current = null; }
    }
}
