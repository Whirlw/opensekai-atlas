using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using UnityEngine.Networking;

namespace OpenSekai.Infrastructure.Packages
{
    internal static class ChartRankLookup
    {
        private static readonly HashSet<string> _active = new();
        private static readonly Dictionary<string, DateTime> _retryAfter = new();

        internal static async UniTask<ChartRankCache.Verdict> ReadInstalledAsync(SongPackageSource source, SongPackage package)
        {
            string key = ChartRankCache.Key(source);
            var known = ChartRankCache.Read(key);
            if (known != null || key == null || !key.StartsWith("sbuga-", StringComparison.Ordinal)) return known;
            if (SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var server, out var name, out _))
                await SonolusLevelInstaller.Shared.RestoreSbugaRankAsync(server, name, package);
            return ChartRankCache.Read(key);
        }

        // Only the source panel requests a lookup. Import, library browsing and settlement stay offline.
        internal static async UniTask<string> CheckAsync(SongPackageSource source, SongPackage package, string hash)
        {
            string key = ChartRankCache.Key(source);
            if (key == null || string.IsNullOrEmpty(hash)) return "Unranked";
            int level = package.Manifest.playLevel;
            var known = await ReadInstalledAsync(source, package);
            if (key.StartsWith("sbuga-", StringComparison.Ordinal) && known?.Matches(hash, level) == true) return "Ranked";
            var now = DateTime.UtcNow;
            if (known != null && (!known.Ranked || known.ScoreKey == hash) && now - known.CheckedUtc < TimeSpan.FromDays(7))
                return Label(known, hash, level);
            if (_active.Contains(key))
            {
                await UniTask.WaitUntil(() => !_active.Contains(key));
                return Label(ChartRankCache.Read(key), hash, level);
            }
            if (_retryAfter.TryGetValue(key, out var next) && now < next) return "稍后可重试核验";
            _active.Add(key);
            _retryAfter[key] = now.AddMinutes(10);
            bool readingDetails = true;
            try
            {
                ChartRankCache.Verdict verdict;
                if (key.StartsWith("os-", StringComparison.Ordinal))
                {
                    string id = key.Substring(3);
                    using var page = UnityWebRequest.Get("https://opensekai.com/chart?id=" + id);
                    page.timeout = 8;
                    await SonolusClient.SendAsync(page, 1024 * 1024, null, default);
                    verdict = ChartRankCache.ParseDetail(page.downloadHandler.text, id);
                    if (verdict == null) return "未能识别上架状态";
                    // A publisher may replace the file under the same ID. Bind each fresh positive lookup to its bytes.
                    readingDetails = false;
                    verdict.ScoreKey = verdict.Ranked ? await PreviewHashAsync(id) : hash;
                }
                else
                {
                    if (!SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var address, out var name, out _)) return "Unranked";
                    var details = await new SonolusClient(address).GetLevelAsync(name, CancellationToken.None);
                    if (details.Level.Name != name) return "服务器返回了其他谱面";
                    readingDetails = false;
                    string verified = await VerifyLocalAsync(package, details.Level);
                    if (verified == null && known?.DataHash != null && known.DataHash == details.Level.Data?.Hash)
                        verified = known.ScoreKey;
                    verdict = ChartRankCache.FromLevel(address, details.Level, verified);
                }
                if (ReferenceEquals(known, ChartRankCache.Read(key))) ChartRankCache.Save(key, verdict);
                return Label(ChartRankCache.Read(key), hash, level);
            }
            catch (SonolusException e) when (readingDetails && (e.Status == 404 || e.Status == 410))
            {
                if (ReferenceEquals(known, ChartRankCache.Read(key)))
                    ChartRankCache.Save(key, new ChartRankCache.Verdict { ScoreKey = hash, CheckedUtc = now });
                return Label(ChartRankCache.Read(key), hash, level);
            }
            catch (Exception e) when (e is SonolusException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException
                or FormatException or InvalidOperationException or CryptographicException)
            {
                return "暂时无法核验 · " + (known == null ? "待确认" : Label(known, hash, level));
            }
            finally { _active.Remove(key); }
        }

        internal static string Label(ChartRankCache.Verdict verdict, string hash, int level) =>
            verdict == null ? "待确认" : verdict.Matches(hash, level) ? "Ranked" : "Unranked";

        internal static async UniTask<string> VerifyLocalAsync(SongPackage package, SonolusLevel level)
        {
            if (string.IsNullOrEmpty(level.Data?.Hash)) return null;
            return await UniTask.RunOnThreadPool(() =>
            {
                using var file = File.OpenRead(package.ScorePath);
                using var sha = SHA1.Create();
                string actual = Hex(sha.ComputeHash(file));
                if (!string.Equals(actual, level.Data.Hash, StringComparison.OrdinalIgnoreCase)) return null;
                file.Position = 0;
                using var sha256 = SHA256.Create();
                return Hex(sha256.ComputeHash(file));
            });
        }

        private static string Hex(byte[] digest) => BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();

        private static async UniTask<string> PreviewHashAsync(string id)
        {
            // Read only the redirect. The preview editor, jacket and audio are not needed.
            using var preview = UnityWebRequest.Get("https://opensekai.com/chart_preview?id=" + id);
            preview.redirectLimit = 0;
            preview.timeout = 8;
            var operation = preview.SendWebRequest();
            while (!operation.isDone) await UniTask.Yield();
            string target = preview.GetResponseHeader("Location");
            if (!SongPackageSource.TryHttps(target, out var uri) || !uri.IsDefaultPort
                || uri.Host != "chartview.unipjsk.com" || uri.AbsolutePath != "/preview")
                throw new InvalidDataException("未取得谱面预览");
            foreach (string field in uri.Query.TrimStart('?').Split('&'))
            {
                if (!field.StartsWith("cfg=", StringComparison.Ordinal)) continue;
                string encoded = Uri.UnescapeDataString(field.Substring(4)).Replace('-', '+').Replace('_', '/');
                var config = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='))));
                string url = (string)(config["j"] ?? config["s"]);
                if (!SongPackageSource.TryHttps(url, out var asset) || !asset.IsDefaultPort ||
                    asset.Host != "tzi998.oss-cn-shanghai.aliyuncs.com" || !asset.AbsolutePath.StartsWith("/ojskcommunity/", StringComparison.Ordinal)) break;
                using var chart = UnityWebRequest.Get(asset.AbsoluteUri);
                chart.timeout = 15;
                await SonolusClient.SendAsync(chart, 32 * 1024 * 1024, null, default);
                byte[] bytes = chart.downloadHandler.data;
                return await UniTask.RunOnThreadPool(() => { using var sha = SHA256.Create(); return Hex(sha.ComputeHash(bytes)); });
            }
            throw new InvalidDataException("缺少公开的谱面文件");
        }
    }
}
