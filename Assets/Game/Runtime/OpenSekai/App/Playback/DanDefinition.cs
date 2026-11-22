using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.Playback
{
    public sealed class DanFieldException : FormatException
    {
        public string Field { get; }
        internal DanFieldException(string field, string message) : base(message) => Field = field;
    }

    public sealed class DanDefinition
    {
        public const string PREFIX = "OpenSekai-Dan/1";
        public string Id { get; }
        public int InitialLife { get; }
        public DanDamage Damage { get; }
        public IReadOnlyList<string> Songs { get; }
        public string Label { get; }
        public string Emblem { get; }
        public string Color { get; }
        public string ShareText { get; }

        public DanDefinition(int initialLife, DanDamage damage, IEnumerable<string> songs, string label, string emblem, string color)
        {
            if (initialLife <= 0) throw Error("initialLife");
            InitialLife = initialLife;
            Damage = damage ?? throw Error("damage");
            var links = new List<string>();
            foreach (string link in songs ?? throw Error("songs"))
            {
                if (!SonolusServerAddress.TryParse(link, out var server, out var level, out _) ||
                    string.IsNullOrWhiteSpace(level) || level == "." || level == ".." || level.Any(char.IsControl))
                    throw Error($"songs[{links.Count}]");
                links.Add(server.LevelPage(level));
            }
            if (links.Count < 2) throw new DanFieldException("songs", "段位至少需要两首谱面。");
            Songs = links.AsReadOnly();
            Label = StyleText(label, "label");
            Emblem = StyleText(emblem, "emblem");
            if (color?.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit)) throw Error("style.color");
            Color = color.ToUpperInvariant();
            var body = new JObject
            {
                ["initialLife"] = InitialLife,
                ["damage"] = new JObject { ["perfect"] = damage.Perfect, ["great"] = damage.Great,
                    ["good"] = damage.Good, ["bad"] = damage.Bad, ["miss"] = damage.Miss },
                ["songs"] = new JArray(links),
                ["style"] = new JObject { ["label"] = Label, ["emblem"] = Emblem, ["color"] = Color }
            };
            ShareText = PREFIX + "\n" + body.ToString(Formatting.None);
            using var sha = SHA256.Create();
            Id = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(ShareText))).Replace("-", "").ToLowerInvariant();
        }

        public static DanDefinition Parse(string text)
        {
            int newline = text?.IndexOf('\n') ?? -1;
            if (newline < 0 || text.Substring(0, newline).Trim() != PREFIX) throw Error("version");
            JObject body;
            try { body = ProfileBackupArchive.Parse(text.Substring(newline + 1)); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            { throw new DanFieldException("json", ex.Message); }
            if (body["damage"] is not JObject damage) throw Error("damage");
            if (body["style"] is not JObject style) throw Error("style");
            if (body["songs"] is not JArray songs) throw Error("songs");
            CheckFields(body, "", "initialLife", "damage", "songs", "style");
            CheckFields(damage, "damage.", "perfect", "great", "good", "bad", "miss");
            CheckFields(style, "style.", "label", "emblem", "color");
            int Cost(string name) => Number(damage[name], "damage." + name);
            string Style(string name) => style[name]?.Type == JTokenType.String ? (string)style[name] : throw Error("style." + name);
            return new DanDefinition(Number(body["initialLife"], "initialLife"),
                new DanDamage(Cost("perfect"), Cost("great"), Cost("good"), Cost("bad"), Cost("miss")),
                songs.Select((s, i) => s.Type == JTokenType.String ? (string)s : throw Error($"songs[{i}]")),
                Style("label"), Style("emblem"), Style("color"));
        }

        private static void CheckFields(JObject obj, string prefix, params string[] fields)
        {
            foreach (var property in obj.Properties())
                if (!fields.Contains(property.Name)) throw Error(prefix + property.Name);
        }

        private static int Number(JToken value, string field) =>
            value?.Type == JTokenType.Integer && int.TryParse(value.ToString(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int result) ? result : throw Error(field);

        private static string StyleText(string value, string field) =>
            !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl) ? value.Trim() : throw Error("style." + field);

        private static DanFieldException Error(string field) => new(field, "段位字段无效：" + field);
    }
}
