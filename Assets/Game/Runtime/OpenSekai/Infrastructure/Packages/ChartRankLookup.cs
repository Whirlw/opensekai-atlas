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
    internal enum RankCheckOutcome
    {
        /// <summary>A verdict is on record, or the source has none to give.</summary>
        Known,
        /// <summary>The server could not be reached or refused for now; the same check may work later.</summary>
        RetryLater,
        /// <summary>The answer or the local chart could not be read; checking again will fail the same way.</summary>
        Unreadable
    }

    internal readonly struct RankCheck
    {
        internal static readonly RankCheck Known = new(RankCheckOutcome.Known, null);

        internal RankCheck(RankCheckOutcome outcome, string reason)
        {
            Outcome = outcome;
            Reason = reason;
        }

        internal RankCheckOutcome Outcome { get; }

        /// <summary>Why no verdict came back, worded for the player; null when <see cref="Outcome"/> is Known.</summary>
        internal string Reason { get; }
    }

    internal static class ChartRankLookup
    {
        internal const string RANKED = "Ranked";
        internal const string UNRANKED = "Unranked";
        internal const string PENDING = "待确认";
        private static readonly HashSet<string> _active = new();
        private static readonly Dictionary<string, DateTime> _retryAfter = new();
        private static readonly Dictionary<string, string> _unreadable = new();

        internal static async UniTask<ChartRankCache.Verdict> ReadInstalledAsync(SongPackageSource source, SongPackage package)
        {
            string key = ChartRankCache.Key(source);
            var known = ChartRankCache.Read(key);
            if (known != null || key == null || !key.StartsWith("sbuga-", StringComparison.Ordinal)) return known;
            if (SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var server, out var name, out _))
                await SonolusLevelInstaller.Shared.RestoreSbugaRankAsync(server, name, package);
            return ChartRankCache.Read(key);
        }

        // Only the library's 检查 and 详情 buttons request a lookup. Import, browsing and settlement stay offline.
        internal static async UniTask<RankCheck> CheckAsync(SongPackageSource source, SongPackage package, string hash)
        {
            string key = ChartRankCache.Key(source);
            if (key == null) return RankCheck.Known;
            if (string.IsNullOrEmpty(hash)) return Unreadable(key, "缺少谱面文件，无法核验");
            int level = package.Manifest.playLevel;
            ChartRankCache.Verdict known;
            try { known = await ReadInstalledAsync(source, package); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Unreadable(key, "无法读取本地谱面文件"); }
            if (key.StartsWith("sbuga-", StringComparison.Ordinal) && known?.Matches(hash, level) == true) return RankCheck.Known;
            var now = DateTime.UtcNow;
            if (known != null && (!known.Ranked || known.ScoreKey == hash) && now - known.CheckedUtc < TimeSpan.FromDays(7))
                return RankCheck.Known;
            if (_active.Contains(key))
            {
                await UniTask.WaitUntil(() => !_active.Contains(key));
                return ChartRankCache.Read(key) != null ? RankCheck.Known : new RankCheck(RankCheckOutcome.RetryLater, "上次核验没有结果，请再试一次");
            }
            if (_retryAfter.TryGetValue(key, out var next) && now < next)
                return new RankCheck(RankCheckOutcome.RetryLater, "刚核验过，请 " + Math.Max(1, (int)Math.Ceiling((next - now).TotalMinutes)) + " 分钟后再试");
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
                    if (verdict == null) return Unreadable(key, "未能识别上架状态");
                    // A publisher may replace the file under the same ID. Bind each fresh positive lookup to its bytes.
                    readingDetails = false;
                    verdict.ScoreKey = verdict.Ranked ? await PreviewHashAsync(id) : hash;
                }
                else
                {
                    if (!SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var address, out var name, out _))
                        return Unreadable(key, "无法识别 Sonolus 关卡地址");
                    var details = await new SonolusClient(address).GetLevelAsync(name, CancellationToken.None);
                    if (details.Level.Name != name) return Unreadable(key, "服务器返回了其他谱面");
                    readingDetails = false;
                    string verified = await VerifyLocalAsync(package, details.Level);
                    if (verified == null && known?.DataHash != null && known.DataHash == details.Level.Data?.Hash)
                        verified = known.ScoreKey;
                    verdict = ChartRankCache.FromLevel(address, details.Level, verified);
                }
                if (ReferenceEquals(known, ChartRankCache.Read(key))) ChartRankCache.Save(key, verdict);
                return RankCheck.Known;
            }
            catch (SonolusException e) when (readingDetails && (e.Status == 404 || e.Status == 410))
            {
                if (ReferenceEquals(known, ChartRankCache.Read(key)))
                    ChartRankCache.Save(key, new ChartRankCache.Verdict { ScoreKey = hash, CheckedUtc = now });
                return RankCheck.Known;
            }
            // Server and network failures can pass on their own; the rest will fail the same way next time.
            catch (SonolusException e) { return new RankCheck(RankCheckOutcome.RetryLater, e.Message.TrimEnd('。')); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Unreadable(key, "无法读取本地谱面文件"); }
            catch (InvalidDataException e) { return Unreadable(key, e.Message); }
            catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or CryptographicException)
            {
                return Unreadable(key, "无法解析发布方返回的数据");
            }
            finally { _active.Remove(key); }
        }

        /// <summary>Why the last lookup of this chart could not be read, until the app restarts; null when it could.</summary>
        internal static string UnreadableReason(string key) =>
            key != null && _unreadable.TryGetValue(key, out string reason) ? reason : null;

        private static RankCheck Unreadable(string key, string reason)
        {
            _unreadable[key] = reason;
            return new RankCheck(RankCheckOutcome.Unreadable, reason);
        }

        internal static string Label(ChartRankCache.Verdict verdict, string hash, int level) =>
            verdict == null ? PENDING : verdict.Matches(hash, level) ? RANKED : UNRANKED;

        internal static string CachedLabel(SongPackageItem item) => item.RankKey == null ? UNRANKED
            : Label(ChartRankCache.Read(item.RankKey), item.ScoreKey, item.Entry.Manifest.playLevel);

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
