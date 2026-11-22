using OpenSekai.Shared.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using UnityEngine;

namespace OpenSekai.App.Server
{
    /// <summary>How the game talks to a server.</summary>
    public enum ServerKind
    {
        /// <summary>A Sonolus server, browsed with the game's own pages.</summary>
        Sonolus,

        /// <summary>A chart website, opened in the system WebView; its downloads import themselves.</summary>
        WebView
    }

    /// <summary>One saved server.</summary>
    public sealed class ServerEntry
    {
        public ServerEntry(string id, ServerKind kind, string name, string address)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Address = address;
        }

        public string Id { get; }
        public ServerKind Kind { get; }
        public string Name { get; internal set; }

        /// <summary>For Sonolus, the server's base address; for a website, the HTTPS page it opens on.</summary>
        public string Address { get; }

        internal JObject AdvancedSearch { get; set; }

        /// <summary>The host shown under the name.</summary>
        public string DisplayAddress
        {
            get
            {
                if (Uri.TryCreate(Address, UriKind.Absolute, out Uri uri))
                {
                    return uri.Host + uri.AbsolutePath.TrimEnd('/');
                }

                return Address;
            }
        }
    }

    /// <summary>
    /// The servers the player added. The game ships with none: the list starts empty and every
    /// server is one the player chose. Saved as JSON in the app's data folder; read and written by
    /// hand rather than by reflection so IL2CPP stripping cannot break it.
    /// </summary>
    public sealed class ServerRegistry
    {
        private const string FILE_NAME = "servers.json";

        private static ServerRegistry _shared;
        private readonly string _path;
        private readonly List<ServerEntry> _servers = new List<ServerEntry>();

        public ServerRegistry(string directory)
        {
            _path = Path.Combine(directory, FILE_NAME);
            Load();
        }

        public static ServerRegistry Shared => _shared ?? (_shared = new ServerRegistry(Application.persistentDataPath));

        public event Action Changed;

        private void RaiseChanged() => Changed?.Invoke();

        public IReadOnlyList<ServerEntry> Servers => _servers;

        public ServerEntry Find(string id) => _servers.Find(server => server.Id == id);

        /// <summary>
        /// Checks and adds a server. Sonolus addresses are normalized (share links and
        /// <c>sonolus://</c> links reduce to their server); a website must be HTTPS.
        /// </summary>
        /// <param name="error">Why it was refused, for the UI.</param>
        public ServerEntry TryAdd(ServerKind kind, string name, string address, out string error)
        {
            error = null;
            string normalized;
            if (kind == ServerKind.Sonolus)
            {
                if (!SonolusServerAddress.TryParse(address, out SonolusServerAddress parsed, out _, out error))
                {
                    return null;
                }

                normalized = parsed.BaseUrl;
            }
            else
            {
                normalized = NormalizeWebAddress(address, out error);
                if (normalized == null)
                {
                    return null;
                }
            }

            foreach (ServerEntry existing in _servers)
            {
                if (existing.Kind == kind && SameAddress(existing.Address, normalized))
                {
                    error = "已经添加过这个服务器：" + existing.Name;
                    return null;
                }
            }

            string title = string.IsNullOrWhiteSpace(name) ? HostOf(normalized) : name.Trim();
            if (title.Length > 40)
            {
                title = title.Substring(0, 40);
            }

            var entry = new ServerEntry("user-" + Guid.NewGuid().ToString("N").Substring(0, 12), kind, title, normalized);
            var updated = new List<ServerEntry>(_servers) { entry };
            if (!TrySave(updated, out error))
            {
                return null;
            }

            _servers.Add(entry);
            RaiseChanged();
            return entry;
        }

        public bool TryRename(ServerEntry entry, string name, out string error)
        {
            int index = _servers.IndexOf(entry);
            error = null;
            if (index < 0 || string.IsNullOrWhiteSpace(name))
            {
                error = index < 0 ? "服务器已不在列表中。" : "请输入服务器名称。";
                return false;
            }

            string title = name.Trim();
            if (entry.Name == title)
            {
                return true;
            }

            var updated = new List<ServerEntry>(_servers);
            updated[index] = new ServerEntry(entry.Id, entry.Kind, title, entry.Address) { AdvancedSearch = entry.AdvancedSearch };
            if (!TrySave(updated, out error))
            {
                return false;
            }

            entry.Name = title;
            RaiseChanged();
            return true;
        }

        public bool TryRemove(ServerEntry entry, out string error)
        {
            int index = _servers.IndexOf(entry);
            if (index < 0)
            {
                error = "服务器已不在列表中。";
                return false;
            }

            var updated = new List<ServerEntry>(_servers);
            updated.RemoveAt(index);
            if (!TrySave(updated, out error))
            {
                return false;
            }

            _servers.RemoveAt(index);
            RaiseChanged();
            return true;
        }

        internal bool TrySaveAdvancedSearch(ServerEntry entry, string searchType,
            IReadOnlyList<KeyValuePair<string, string>> query, out string error)
        {
            error = null;
            int index = _servers.IndexOf(entry);
            if (index < 0 || entry.Kind != ServerKind.Sonolus) return true;

            JObject search = null;
            if (query != null)
            {
                var values = new JObject();
                foreach (var field in query)
                {
                    if (field.Key != "type") values[field.Key] = field.Value;
                }

                search = new JObject { ["type"] = searchType, ["values"] = values };
            }

            if (JToken.DeepEquals(entry.AdvancedSearch, search)) return true;

            var updated = new List<ServerEntry>(_servers);
            updated[index] = new ServerEntry(entry.Id, entry.Kind, entry.Name, entry.Address) { AdvancedSearch = search };
            if (!TrySave(updated, out error)) return false;

            entry.AdvancedSearch = search;
            return true;
        }

        /// <summary>An HTTPS address with its query and fragment kept, or null with the reason.</summary>
        internal static string NormalizeWebAddress(string address, out string error)
        {
            error = null;
            string text = (address ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                error = "请输入网址。";
                return null;
            }

            if (text.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                text = "https://" + text;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host))
            {
                error = "网址无效。";
                return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                error = "网页服务器仅支持 HTTPS。";
                return null;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                error = "网址不能包含账号信息。";
                return null;
            }

            return uri.AbsoluteUri;
        }

        private static string HostOf(string address) =>
            Uri.TryCreate(address, UriKind.Absolute, out Uri uri) ? uri.Host : address;

        private static bool SameAddress(string a, string b) =>
            string.Equals(a?.TrimEnd('/'), b?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

        private void Load()
        {
            try
            {
                if (File.Exists(_path) && JToken.Parse(File.ReadAllText(_path)) is JObject root)
                {
                    if (root["servers"] is JArray servers)
                    {
                        foreach (JToken token in servers)
                        {
                            if (!(token is JObject value))
                            {
                                continue;
                            }

                            string id = (string)value["id"];
                            string address = (string)value["address"];
                            string name = (string)value["name"];
                            ServerKind kind = string.Equals((string)value["kind"], "webview", StringComparison.OrdinalIgnoreCase)
                                ? ServerKind.WebView
                                : ServerKind.Sonolus;
                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(address))
                            {
                                _servers.Add(new ServerEntry(id, kind, string.IsNullOrEmpty(name) ? HostOf(address) : name, address)
                                {
                                    AdvancedSearch = kind == ServerKind.Sonolus ? value["advancedSearch"] as JObject : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Servers] Saved server list unreadable, starting empty: " + exception.Message);
            }
        }

        private bool TrySave(IReadOnlyList<ServerEntry> entries, out string error)
        {
            var servers = new JArray();
            foreach (ServerEntry server in entries)
            {
                var value = new JObject
                {
                    ["id"] = server.Id,
                    ["kind"] = server.Kind == ServerKind.WebView ? "webview" : "sonolus",
                    ["name"] = server.Name,
                    ["address"] = server.Address
                };
                if (server.AdvancedSearch != null) value["advancedSearch"] = server.AdvancedSearch.DeepClone();
                servers.Add(value);
            }

            var root = new JObject
            {
                ["version"] = 1,
                ["servers"] = servers
            };

            try
            {
                AtomicFile.WriteAllText(_path, root.ToString(Formatting.Indented));
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Servers] Could not save the server list: " + exception.Message);
                error = "无法保存服务器列表，请检查应用存储后重试。";
                return false;
            }
        }
    }
}
