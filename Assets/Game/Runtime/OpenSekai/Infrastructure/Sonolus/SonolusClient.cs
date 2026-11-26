using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenSekai.Infrastructure.Sonolus
{
    /// <summary>Why a request to a Sonolus server failed, worded for the player.</summary>
    public sealed class SonolusException : Exception
    {
        public SonolusException(string message, long status = 0) : base(message)
        {
            Status = status;
        }

        /// <summary>HTTP status when available, otherwise 0.</summary>
        public long Status { get; }

        internal bool IsNetworkFailure { get; private set; }
        internal bool IsInvalidResponse { get; private set; }

        internal static SonolusException ConnectionFailure(string error) =>
            new SonolusException("无法连接服务器，请检查网络（" + error + "）。") { IsNetworkFailure = true };

        internal static SonolusException InvalidResponse(string message) =>
            new SonolusException(message) { IsInvalidResponse = true };
    }

    /// <summary>
    /// Reads a Sonolus server's level endpoints anonymously: the server card, the level home page
    /// with its sections and search forms, paged lists and searches, one level's details, its
    /// community comments and leaderboards, and resource bytes. Every call can be cancelled and
    /// times out; failures surface as <see cref="SonolusException"/> with a Chinese message.
    /// Requests run on Unity's thread through <see cref="UnityWebRequest"/>.
    /// </summary>
    public sealed class SonolusClient
    {
        /// <summary>Asked of the server for text it localizes; servers fall back to English.</summary>
        public const string LOCALIZATION = "zhs";

        private const int JSON_TIMEOUT_SECONDS = 20;
        private const int DOWNLOAD_TIMEOUT_SECONDS = 180;
        private const int MAX_JSON_BYTES = 8 * 1024 * 1024;

        public SonolusClient(SonolusServerAddress address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        public SonolusServerAddress Address { get; }

#if UNITY_EDITOR
        /// <summary>
        /// Installed by the editor's sample content: gives the local file that answers a request, or
        /// null to send it. Null in normal use.
        /// </summary>
        internal static Func<string, string> CaptureResponder { get; set; }
#endif

        public async UniTask<SonolusServerInfo> GetServerInfoAsync(CancellationToken cancellationToken)
        {
            return SonolusServerInfo.From(await GetJsonAsync("/info", null, cancellationToken));
        }

        public async UniTask<SonolusLevelsInfo> GetLevelsInfoAsync(CancellationToken cancellationToken)
        {
            return SonolusLevelsInfo.From(await GetJsonAsync("/levels/info", null, cancellationToken));
        }

        /// <param name="query">Search parameters (<c>type</c> and the form's fields); null lists everything.</param>
        /// <param name="page">Zero-based page for numbered paging.</param>
        /// <param name="cursor">For cursor paging, the cursor of the previous page; null for the first.</param>
        public async UniTask<SonolusPage<SonolusLevel>> ListLevelsAsync(
            IEnumerable<KeyValuePair<string, string>> query, int page, string cursor, CancellationToken cancellationToken)
        {
            var parameters = new List<KeyValuePair<string, string>>();
            if (query != null)
            {
                parameters.AddRange(query);
            }

            AddPaging(parameters, page, cursor);
            JObject value = await GetJsonAsync("/levels/list", parameters, cancellationToken) as JObject ?? new JObject();
            return new SonolusPage<SonolusLevel>(SonolusLevel.ListFrom(value["items"]), (int)Json.Number(value, "pageCount"), Json.String(value, "cursor"));
        }

        public async UniTask<SonolusLevelDetails> GetLevelAsync(string name, CancellationToken cancellationToken)
        {
            SonolusLevelDetails details = SonolusLevelDetails.From(await GetJsonAsync("/levels/" + Escape(name), null, cancellationToken));
            if (details == null)
            {
                throw SonolusException.InvalidResponse("服务器返回的谱面详情无效。");
            }

            return details;
        }

        /// <summary>The comments a level's community page leads with.</summary>
        public async UniTask<IReadOnlyList<SonolusComment>> GetTopCommentsAsync(string name, CancellationToken cancellationToken)
        {
            JObject value = await GetJsonAsync("/levels/" + Escape(name) + "/community/info", null, cancellationToken) as JObject;
            return SonolusComment.ListFrom(value?["topComments"]);
        }

        public async UniTask<SonolusPage<SonolusComment>> ListCommentsAsync(
            string name, int page, string cursor, CancellationToken cancellationToken)
        {
            var parameters = new List<KeyValuePair<string, string>>();
            AddPaging(parameters, page, cursor);
            JObject value = await GetJsonAsync("/levels/" + Escape(name) + "/community/comments/list", parameters, cancellationToken) as JObject
                ?? new JObject();
            return new SonolusPage<SonolusComment>(SonolusComment.ListFrom(value["comments"]), (int)Json.Number(value, "pageCount"), Json.String(value, "cursor"));
        }

        public async UniTask<IReadOnlyList<SonolusRecord>> GetTopRecordsAsync(
            string name, string leaderboard, CancellationToken cancellationToken)
        {
            JObject value = await GetJsonAsync(
                "/levels/" + Escape(name) + "/leaderboards/" + Escape(leaderboard), null, cancellationToken) as JObject;
            return SonolusRecord.ListFrom(value?["topRecords"]);
        }

        public async UniTask<SonolusPage<SonolusRecord>> ListRecordsAsync(
            string name, string leaderboard, int page, string cursor, CancellationToken cancellationToken)
        {
            var parameters = new List<KeyValuePair<string, string>>();
            AddPaging(parameters, page, cursor);
            JObject value = await GetJsonAsync(
                "/levels/" + Escape(name) + "/leaderboards/" + Escape(leaderboard) + "/records/list", parameters, cancellationToken) as JObject
                ?? new JObject();
            return new SonolusPage<SonolusRecord>(SonolusRecord.ListFrom(value["records"]), (int)Json.Number(value, "pageCount"), Json.String(value, "cursor"));
        }

        /// <summary>
        /// Downloads a resource the server named into memory, refusing anything larger than
        /// <paramref name="maxBytes"/> (checked against the declared length first, then the bytes
        /// actually received).
        /// </summary>
        /// <param name="progress">0–1 while the length is known; -1 when it is not.</param>
        public async UniTask<byte[]> DownloadAsync(
            SonolusSrl resource, long maxBytes, Action<float> progress, CancellationToken cancellationToken)
        {
            string url = resource != null ? Address.Resolve(resource.Url) : null;
            if (url == null)
            {
                throw new SonolusException("服务器没有提供该资源，或资源地址不是 HTTPS。");
            }

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = DOWNLOAD_TIMEOUT_SECONDS;
                await SendAsync(request, maxBytes, progress, cancellationToken);
                return request.downloadHandler.data;
            }
        }

        internal async UniTask<(AudioClip Clip, byte[] Data)> DownloadAudioAsync(
            SonolusSrl resource, long maxBytes, Action<float> progress, CancellationToken cancellationToken)
        {
            string url = resource != null ? Address.Resolve(resource.Url) : null;
            if (url == null) throw new SonolusException("服务器没有提供有效的 HTTPS 音频地址。");

            AudioType type = await ProbeAudioTypeAsync(url, maxBytes, cancellationToken);
            if (type == AudioType.UNKNOWN)
                throw new SonolusException("音乐格式无法识别（仅支持 MP3 / OGG / WAV）。");

            using var request = UnityWebRequestMultimedia.GetAudioClip(url, type);
            request.timeout = DOWNLOAD_TIMEOUT_SECONDS;
            await SendAsync(request, maxBytes, progress, cancellationToken);
            byte[] data = request.downloadHandler.data;
            if (AudioTypeOf(data) != type)
                throw new SonolusException("服务器返回的音乐与文件头不一致，请重试。");

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null) throw new SonolusException("无法解码服务器音频。");
            return (clip, data);
        }

        // Sonolus audio URLs are often content hashes. FMOD cannot reliably infer the format
        // from an extensionless URL with AudioType.UNKNOWN, especially on mobile.
        private static async UniTask<AudioType> ProbeAudioTypeAsync(
            string url, long maxBytes, CancellationToken cancellationToken)
        {
            var header = new AudioHeaderDownload();
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, header, null);
            request.timeout = JSON_TIMEOUT_SECONDS;
            try
            {
                await SendAsync(request, maxBytes, null, cancellationToken);
            }
            catch (SonolusException) when (header.StoppedAfterHeader
                && request.responseCode >= 200 && request.responseCode < 300)
            {
                // The handler declines the remaining body once it has the signature.
            }

            return header.Type;
        }

        internal static AudioType AudioTypeOf(byte[] header) => SonolusLevelInstaller.AudioExtension(header) switch
        {
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".wav" => AudioType.WAV,
            _ => AudioType.UNKNOWN
        };

        private sealed class AudioHeaderDownload : DownloadHandlerScript
        {
            private readonly byte[] _header = new byte[12];
            private int _length;

            internal AudioHeaderDownload() : base(new byte[4096]) { }

            internal AudioType Type => _length == _header.Length ? AudioTypeOf(_header) : AudioType.UNKNOWN;
            internal bool StoppedAfterHeader { get; private set; }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (dataLength == 0) return true;
                int copied = Math.Min(_header.Length - _length, dataLength);
                Array.Copy(data, 0, _header, _length, copied);
                _length += copied;
                StoppedAfterHeader = copied < dataLength;
                return !StoppedAfterHeader;
            }
        }

        private async UniTask<JToken> GetJsonAsync(
            string path, List<KeyValuePair<string, string>> query, CancellationToken cancellationToken)
        {
            var parameters = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("localization", LOCALIZATION) };
            if (query != null)
            {
                parameters.AddRange(query);
            }

            string url = Address.Endpoint(path, parameters);
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = JSON_TIMEOUT_SECONDS;
                request.SetRequestHeader("Accept", "application/json");
                await SendAsync(request, MAX_JSON_BYTES, null, cancellationToken);
                string text = request.downloadHandler.text;
                try
                {
                    return JToken.Parse(text);
                }
                catch (Exception)
                {
                    throw SonolusException.InvalidResponse("服务器返回的不是 Sonolus 数据，请确认地址。");
                }
            }
        }

        internal static async UniTask SendAsync(
            UnityWebRequest request, long maxBytes, Action<float> progress, CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            string localAnswer = CaptureResponder?.Invoke(request.url);
            if (localAnswer != null)
            {
                request.url = localAnswer;
            }
#endif
            request.redirectLimit = 8;
            request.SetRequestHeader("User-Agent", "OpenSekaiAtlas/" + Application.version);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                ulong received = request.downloadedBytes;
                if ((long)received > maxBytes)
                {
                    request.Abort();
                    throw SonolusException.InvalidResponse("资源超过大小限制（" + FormatBytes(maxBytes) + "）。");
                }

                string declared = request.GetResponseHeader("Content-Length");
                if (declared != null && long.TryParse(declared, NumberStyles.Integer, CultureInfo.InvariantCulture, out long length))
                {
                    if (length > maxBytes)
                    {
                        request.Abort();
                        throw SonolusException.InvalidResponse("资源超过大小限制（" + FormatBytes(maxBytes) + "）。");
                    }

                    progress?.Invoke(length > 0 ? Mathf.Clamp01((float)received / length) : -1f);
                }
                else
                {
                    progress?.Invoke(-1f);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                throw SonolusException.ConnectionFailure(request.error);
            }

            if (request.result == UnityWebRequest.Result.ProtocolError || request.responseCode >= 400)
            {
                throw new SonolusException(DescribeStatus(request.responseCode), request.responseCode);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw SonolusException.InvalidResponse("请求失败：" + request.error);
            }

            if (request.downloadedBytes > (ulong)maxBytes || request.downloadHandler.nativeData.Length > maxBytes)
            {
                throw SonolusException.InvalidResponse("资源超过大小限制（" + FormatBytes(maxBytes) + "）。");
            }

            progress?.Invoke(1f);
        }

        internal static string DescribeStatus(long status)
        {
            switch (status)
            {
                case 401:
                case 403:
                    return "该内容需要登录服务器，暂不支持。";
                case 404:
                    return "服务器上找不到该内容（404）。";
                case 429:
                    return "请求太频繁，请稍后再试（429）。";
                default:
                    return status >= 500 ? "服务器出错了（" + status + "），请稍后再试。" : "请求失败（HTTP " + status + "）。";
            }
        }

        internal static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
            {
                return (bytes / (1024d * 1024d)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            }

            return (bytes / 1024d).ToString("0", CultureInfo.InvariantCulture) + " KB";
        }

        private static void AddPaging(List<KeyValuePair<string, string>> parameters, int page, string cursor)
        {
            if (!string.IsNullOrEmpty(cursor))
            {
                parameters.Add(new KeyValuePair<string, string>("cursor", cursor));
            }
            else
            {
                parameters.Add(new KeyValuePair<string, string>("page", Math.Max(0, page).ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new SonolusException("谱面名称为空。");
            }

            return Uri.EscapeDataString(value);
        }
    }
}
