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
        /// <summary>The local chart could not be read.</summary>
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
        private static readonly Dictionary<string, DateTime> _refreshAfter = new();

        internal static async UniTask<ChartRankCache.Verdict> ReadInstalledAsync(SongPackageSource source, SongPackage package)
        {
            string key = ChartRankCache.Key(source);
            var known = ChartRankCache.Read(key);
            if (known != null || key == null || !key.StartsWith("sbuga-", StringComparison.Ordinal)) return known;
            if (SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var server, out var name, out _))
                await SonolusLevelInstaller.Shared.RestoreSbugaRankAsync(server, name, package);
            return ChartRankCache.Read(key);
        }

        // The library calls this only when the player chooses 检查 or 刷新Ranked状态.
        internal static UniTask<RankCheck> CheckAsync(SongPackageSource source, SongPackage package, string hash)
        {
            string key = ChartRankCache.Key(source);
            if (key == null) return UniTask.FromResult(RankCheck.Known);
            if (string.IsNullOrEmpty(hash))
                return UniTask.FromResult(new RankCheck(RankCheckOutcome.Unreadable, "缺少谱面文件，无法核验"));
            return CheckRemoteAsync(key, hash, package.Manifest.playLevel, known => FetchVerdictAsync(key, source, package, hash, known));
        }

        internal static async UniTask<RankCheck> CheckRemoteAsync(
            string key, string hash, int level, Func<ChartRankCache.Verdict, UniTask<ChartRankCache.Verdict>> fetchVerdict)
        {
            var now = DateTime.UtcNow;
            if (_refreshAfter.TryGetValue(key, out var next) && now < next)
                return new RankCheck(RankCheckOutcome.RetryLater, "刚收到服务器响应，请 " + Math.Ceiling((next - now).TotalMinutes) + " 分钟后再试");
            if (!_active.Add(key))
                return new RankCheck(RankCheckOutcome.RetryLater, "正在核验 Ranked 状态");
            _refreshAfter.Remove(key);
            bool networkFailed = false;
            try
            {
                var known = ChartRankCache.Read(key);
                ChartRankCache.Verdict verdict;
                try { verdict = await fetchVerdict(known); }
                catch (Exception e) when (e is InvalidDataException or JsonException or FormatException or InvalidOperationException
                    or CryptographicException or SonolusException { IsInvalidResponse: true })
                {
                    verdict = ChartRankCache.FromUnreadableResponse(hash, level);
                }
                if (ReferenceEquals(known, ChartRankCache.Read(key))) ChartRankCache.Save(key, verdict);
                return RankCheck.Known;
            }
            catch (SonolusException e)
            {
                networkFailed = e.IsNetworkFailure;
                return new RankCheck(RankCheckOutcome.RetryLater, e.Message.TrimEnd('。'));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return new RankCheck(RankCheckOutcome.Unreadable, "无法读取本地谱面文件");
            }
            finally
            {
                if (!networkFailed) _refreshAfter[key] = DateTime.UtcNow.AddMinutes(10);
                _active.Remove(key);
            }
        }

        private static async UniTask<ChartRankCache.Verdict> FetchVerdictAsync(
            string key, SongPackageSource source, SongPackage package, string hash, ChartRankCache.Verdict known)
        {
            bool readingDetails = true;
            try
            {
                if (key.StartsWith("os-", StringComparison.Ordinal))
                {
                    string id = key.Substring(3);
                    using var page = UnityWebRequest.Get("https://opensekai.com/chart?id=" + id);
                    page.timeout = 8;
                    await SonolusClient.SendAsync(page, 1024 * 1024, null, default);
                    var verdict = ChartRankCache.ParseDetail(page.downloadHandler.text, id);
                    if (verdict == null) throw new InvalidDataException("未能识别上架状态");
                    // A publisher may replace the file under the same ID. Bind each fresh positive lookup to its bytes.
                    readingDetails = false;
                    verdict.ScoreKey = verdict.Ranked ? await PreviewHashAsync(id) : hash;
                    return verdict;
                }

                if (!SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var address, out var name, out _))
                    throw new InvalidDataException("无法识别 Sonolus 关卡地址");
                var details = await new SonolusClient(address).GetLevelAsync(name, CancellationToken.None);
                if (details.Level.Name != name) throw new InvalidDataException("服务器返回了其他谱面");
                readingDetails = false;
                string verified = await VerifyLocalAsync(package, details.Level);
                if (verified == null && known?.DataHash != null && known.DataHash == details.Level.Data?.Hash)
                    verified = known.ScoreKey;
                return ChartRankCache.FromLevel(address, details.Level, verified);
            }
            catch (SonolusException e) when (readingDetails && (e.Status == 404 || e.Status == 410))
            {
                return new ChartRankCache.Verdict { ScoreKey = hash };
            }
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

        internal static Uri PreviewRedirect(UnityWebRequest.Result result, long status, string error, string location)
        {
            // With redirectLimit = 0 Unity may report an intentional 302 stop as a connection error.
            // Preserve an HTTP refusal as a retryable lookup, regardless of any Location header.
            if (status >= 400)
                throw new SonolusException(SonolusClient.DescribeStatus(status), status);
            bool redirect = status is 301 or 302 or 303 or 307 or 308;
            if (!redirect && result == UnityWebRequest.Result.ConnectionError)
                throw SonolusException.ConnectionFailure(error);
            if (!redirect || !SongPackageSource.TryHttps(location, out var uri) || !uri.IsDefaultPort
                || uri.Host != "chartview.unipjsk.com" || uri.AbsolutePath != "/preview")
                throw new InvalidDataException("未取得谱面预览");
            return uri;
        }

        private static async UniTask<string> PreviewHashAsync(string id)
        {
            // Read only the redirect. The preview editor, jacket and audio are not needed.
            using var preview = UnityWebRequest.Get("https://opensekai.com/chart_preview?id=" + id);
            preview.redirectLimit = 0;
            preview.timeout = 8;
            var operation = preview.SendWebRequest();
            while (!operation.isDone) await UniTask.Yield();
            var uri = PreviewRedirect(preview.result, preview.responseCode, preview.error, preview.GetResponseHeader("Location"));
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
