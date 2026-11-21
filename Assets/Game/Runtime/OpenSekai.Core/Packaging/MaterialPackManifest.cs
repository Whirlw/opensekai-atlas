using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenSekai.MaterialPacks
{
    public sealed class MaterialAudioSlot
    {
        public string File { get; set; }
        public string Hash { get; set; }
        public int Channels { get; set; }
        public int Frames { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
    }

    public sealed class MaterialPackGroup
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<string, MaterialAudioSlot> Slots { get; set; } = new();
    }

    public sealed class InstalledMaterialPack
    {
        public string PackId { get; set; }
        public string Digest { get; set; }
        public string Version { get; set; }
        public List<MaterialPackGroup> Groups { get; set; } = new();
        public string GroupReference(string groupId) => PackId + "/" + Digest + "/" + groupId;
    }

    public static class MaterialPackManifest
    {
        private static readonly Regex _id = new(@"\A[a-z][a-z0-9.-]*\z");
        private static readonly Regex _reserved = new(@"^(con|prn|aux|nul|com[1-9¹²³]|lpt[1-9¹²³])(\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static InstalledMaterialPack Parse(string json, string digest,
            IReadOnlyDictionary<string, (string hash, MaterialPackWave wave)> media)
        {
            JObject root;
            try
            {
                using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw Error(root, "清单末尾有多余内容");
            }
            catch (JsonException e) { throw new InvalidDataException("pack.json：" + e.Message, e); }
            if (Integer(root["schemaVersion"]) != 1) throw Error(root, "不支持 schemaVersion " + root["schemaVersion"]);
            Fields(root, "schemaVersion", "packId", "version", "groups", "extensions");
            var pack = new InstalledMaterialPack { PackId = Id(root, "packId"), Version = Required(root, "version"), Digest = digest };
            CheckPath(pack.PackId);
            if (root["groups"] is not JArray groups || groups.Count == 0) throw Error(root, "需要非空 groups 数组");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken element in groups)
            {
                if (element is not JObject group) throw Error(element, "需要资源组对象");
                Fields(group, "id", "category", "displayName", "slots", "extensions");
                var result = new MaterialPackGroup { Id = Id(group, "id") };
                if (!ids.Add(result.Id)) throw Error(group, "重复组 ID " + result.Id);
                if (group["displayName"] != null) result.DisplayName = Required(group, "displayName");
                if (Required(group, "category") != "audio") throw Error(group, "当前只支持 audio 类别");
                if (group["slots"] is not JObject slots || !slots.HasValues) throw Error(group, "需要非空 slots 对象");
                foreach (var property in slots.Properties())
                {
                    if (!MaterialPackAudio.HasSlot(property.Name) || property.Value is not JObject slot)
                        throw Error(property, "未知槽位或槽位值无效");
                    Fields(slot, "file", "loopFrames", "extensions");
                    string name = Required(slot, "file");
                    if (!media.TryGetValue(name, out var info)) throw Error(slot, "找不到 WAV " + name);
                    int start = 0, end = info.wave.Frames;
                    if (slot.TryGetValue("loopFrames", out var range))
                    {
                        if (!MaterialPackAudio.UsesLoop(property.Name) || range is not JArray frames || frames.Count != 2)
                            throw Error(range, "此槽位需要有效的循环区间");
                        start = Integer(frames[0]);
                        end = Integer(frames[1]);
                        if (start < 0 || start >= end || end > info.wave.Frames) throw Error(range, name + " 的循环采样帧越界");
                    }
                    result.Slots.Add(property.Name, new MaterialAudioSlot { File = name, Hash = info.hash,
                        Channels = info.wave.Channels, Frames = info.wave.Frames, Start = start, End = end });
                }
                pack.Groups.Add(result);
            }
            return pack;
        }

        internal static void CheckPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 512 || !path.IsNormalized() ||
                path.Any(c => c < 32 || "\\:\"*?<>|".Contains(c)))
                throw new InvalidDataException("无效的资源包路径 " + path);
            foreach (string part in path.Split('/'))
                if (part.Length == 0 || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) || _reserved.IsMatch(part))
                    throw new InvalidDataException("无效的资源包路径 " + path);
        }

        private static void Fields(JObject obj, params string[] allowed)
        {
            foreach (var field in obj.Properties())
                if (!allowed.Contains(field.Name) || field.Name == "extensions" && field.Value.Type != JTokenType.Object)
                    throw Error(field, "未知字段或无效的扩展数据");
        }

        private static string Id(JObject obj, string key)
        {
            string value = Required(obj, key);
            if (!_id.IsMatch(value)) throw Error(obj[key], "无效 ID");
            return value;
        }

        private static string Required(JObject obj, string key) => obj[key]?.Type == JTokenType.String
            && !string.IsNullOrWhiteSpace((string)obj[key]) ? (string)obj[key] : throw Error(obj, "需要字符串 " + key);

        private static int Integer(JToken value) => value?.Type == JTokenType.Integer && int.TryParse(value.ToString(), out int number)
            ? number : throw Error(value, "需要整数");

        private static InvalidDataException Error(JToken token, string message) =>
            new("pack.json " + token?.Path + "：" + message);
    }
}
