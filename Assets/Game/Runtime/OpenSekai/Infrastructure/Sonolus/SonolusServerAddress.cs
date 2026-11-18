using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSekai.Infrastructure.Sonolus
{
    /// <summary>
    /// Where a Sonolus server lives: its scheme, host and optional path prefix. Endpoints hang off
    /// <c>{BaseUrl}/sonolus/...</c>, and resource URLs that start with <c>/</c> are joined to the
    /// base address (prefix included), as the Sonolus server spec asks, not to the host root.
    /// Pure string work, no Unity types, so it is covered by EditMode tests.
    /// </summary>
    public sealed class SonolusServerAddress : IEquatable<SonolusServerAddress>
    {
        private SonolusServerAddress(string host, string prefix)
        {
            Host = host;
            Prefix = prefix;
            BaseUrl = "https://" + host + prefix;
        }

        /// <summary>Lowercase host, with a port when one was given.</summary>
        public string Host { get; }

        /// <summary>Path prefix with a leading slash and no trailing one; empty for a server at the root.</summary>
        public string Prefix { get; }

        /// <summary><c>https://host[/prefix]</c>, never ending in a slash.</summary>
        public string BaseUrl { get; }

        /// <summary>Stable identity of the server, used to key downloads and saved servers.</summary>
        public string Key => Host + Prefix;

        /// <summary>
        /// Reads what a player typed or pasted: a bare host (<c>untitledcharts.com</c>), an HTTPS
        /// address with or without a path prefix, a share link to one level
        /// (<c>https://host[/prefix]/levels/{name}</c>), a <c>sonolus://</c> link, or an
        /// <c>open.sonolus.com/{host}/...</c> wrapper. A trailing <c>/sonolus</c> (the API root)
        /// is dropped. Plain HTTP is refused.
        /// </summary>
        /// <param name="levelName">The level a share link points at, or null.</param>
        /// <param name="error">Why the input was refused, in Chinese for the UI.</param>
        public static bool TryParse(string input, out SonolusServerAddress address, out string levelName, out string error)
        {
            address = null;
            levelName = null;
            error = null;
            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                error = "请输入服务器地址。";
                return false;
            }

            if (text.Length > 2048)
            {
                error = "地址过长。";
                return false;
            }

            string rest;
            int scheme = text.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                string name = text.Substring(0, scheme).ToLowerInvariant();
                if (name == "http")
                {
                    error = "仅支持 HTTPS 服务器。";
                    return false;
                }

                if (name != "https" && name != "sonolus")
                {
                    error = "不支持的地址类型：" + name + "://";
                    return false;
                }

                rest = text.Substring(scheme + 3);
            }
            else
            {
                rest = text;
            }

            // Query and fragment never belong to the server address.
            int cut = rest.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
            {
                rest = rest.Substring(0, cut);
            }

            int slash = rest.IndexOf('/');
            string host = (slash >= 0 ? rest.Substring(0, slash) : rest).ToLowerInvariant();
            string path = slash >= 0 ? rest.Substring(slash) : string.Empty;
            if (!IsValidHost(host))
            {
                error = "服务器地址无效。";
                return false;
            }

            List<string> segments = SplitPath(path);

            // open.sonolus.com/{host}/{prefix...}/levels/{name} wraps another server's link.
            if (host == "open.sonolus.com" && segments.Count > 0)
            {
                host = segments[0].ToLowerInvariant();
                segments.RemoveAt(0);
                if (!IsValidHost(host))
                {
                    error = "服务器地址无效。";
                    return false;
                }
            }

            // .../levels/{name} is a share link to one level.
            int levels = segments.LastIndexOf("levels");
            if (levels >= 0 && levels == segments.Count - 2)
            {
                levelName = Uri.UnescapeDataString(segments[levels + 1]);
                segments.RemoveRange(levels, 2);
            }

            if (segments.Count > 0 && segments[segments.Count - 1] == "sonolus")
            {
                segments.RemoveAt(segments.Count - 1);
            }

            var prefix = new StringBuilder();
            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                {
                    error = "服务器地址无效。";
                    return false;
                }

                prefix.Append('/').Append(segment);
            }

            address = new SonolusServerAddress(host, prefix.ToString());
            return true;
        }

        public static SonolusServerAddress Parse(string input)
        {
            if (!TryParse(input, out SonolusServerAddress address, out _, out string error))
            {
                throw new FormatException(error);
            }

            return address;
        }

        /// <summary>
        /// <c>{BaseUrl}/sonolus{path}</c> with the query appended; values are URL-encoded and
        /// empty values are left out.
        /// </summary>
        /// <param name="path">Starts with a slash, e.g. <c>/levels/list</c>.</param>
        public string Endpoint(string path, IEnumerable<KeyValuePair<string, string>> query = null)
        {
            var url = new StringBuilder(BaseUrl).Append("/sonolus").Append(path);
            char separator = '?';
            if (query != null)
            {
                foreach (KeyValuePair<string, string> pair in query)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    {
                        continue;
                    }

                    url.Append(separator).Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value));
                    separator = '&';
                }
            }

            return url.ToString();
        }

        /// <summary>
        /// Absolute URL of a resource the server named. An absolute HTTPS URL (a CDN, say) is used
        /// as given; one that starts with <c>/</c> is joined to <see cref="BaseUrl"/>; anything
        /// else (plain HTTP, other schemes) is refused with null.
        /// </summary>
        public string Resolve(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();
            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                return "https:" + url;
            }

            if (url.StartsWith("/", StringComparison.Ordinal))
            {
                return BaseUrl + url;
            }

            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (url.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return null;
            }

            return BaseUrl + "/" + url;
        }

        /// <summary>The page on the server's website that shows one level, for sharing.</summary>
        public string LevelPage(string levelName) => BaseUrl + "/levels/" + Uri.EscapeDataString(levelName ?? string.Empty);

        public bool Equals(SonolusServerAddress other) => other != null && other.Key == Key;

        public override bool Equals(object obj) => Equals(obj as SonolusServerAddress);

        public override int GetHashCode() => Key.GetHashCode();

        public override string ToString() => BaseUrl;

        private static List<string> SplitPath(string path)
        {
            var segments = new List<string>();
            foreach (string part in path.Split('/'))
            {
                if (part.Length > 0)
                {
                    segments.Add(part);
                }
            }

            return segments;
        }

        private static bool IsValidHost(string host)
        {
            if (string.IsNullOrEmpty(host) || host.Length > 253 || host.IndexOf('@') >= 0)
            {
                return false;
            }

            int colon = host.LastIndexOf(':');
            string name = host;
            if (colon >= 0)
            {
                string port = host.Substring(colon + 1);
                if (port.Length == 0 || port.Length > 5 || !int.TryParse(port, out int number) || number <= 0 || number > 65535)
                {
                    return false;
                }

                name = host.Substring(0, colon);
            }

            if (name.Length == 0 || name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '.';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
