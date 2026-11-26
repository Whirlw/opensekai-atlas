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
        public bool AllowPause { get; }
        public bool FailOnZero { get; }
        public bool RestEnabled { get; }
        public float RestSeconds { get; }
        public IReadOnlyList<float> SongRates { get; }
        public const float DEFAULT_REST_SECONDS = 50f;
        public string Label { get; }
        public string Emblem { get; }
        public string Color { get; }

        /// <summary>Which frame the plate wears, one of <see cref="MedalTiers"/>.</summary>
        public int Tier { get; }

        /// <summary>The grade on the plate's right, one of <see cref="MedalGrades"/>; null for none.</summary>
        public string Grade { get; }
        public string ShareText { get; }

        public DanDefinition(int initialLife, DanDamage damage, IEnumerable<string> songs, string label, string emblem, string color,
            int tier = MedalTiers.LOWEST, string grade = null, bool allowPause = true, bool failOnZero = true,
            bool restEnabled = true, float restSeconds = DEFAULT_REST_SECONDS, IEnumerable<float> songRates = null)
        {
            if (initialLife <= 0) throw Error("initialLife");
            InitialLife = initialLife;
            Damage = damage ?? throw Error("damage");
            var links = new List<string>();
            foreach (string link in songs ?? throw Error("songs"))
            {
                if (DanLibrarySong.TryParse(link, out var librarySong))
                {
                    links.Add(librarySong.Reference);
                    continue;
                }
                if (!SonolusServerAddress.TryParse(link, out var server, out var level, out _) ||
                    string.IsNullOrWhiteSpace(level) || level == "." || level == ".." || level.Any(char.IsControl))
                    throw Error($"songs[{links.Count}]");
                links.Add(server.LevelPage(level));
            }
            if (links.Count < 2) throw new DanFieldException("songs", "段位至少需要两首谱面。");
            Songs = links.AsReadOnly();
            AllowPause = allowPause;
            FailOnZero = failOnZero;
            RestEnabled = restEnabled;
            if (!float.IsFinite(restSeconds) || restSeconds < 0) throw Error("restSeconds");
            RestSeconds = restSeconds;
            var rates = songRates?.ToArray() ?? Enumerable.Repeat(1f, links.Count).ToArray();
            if (rates.Length != links.Count) throw Error("songRates");
            for (int i = 0; i < rates.Length; i++)
                if (!(rates[i] >= PracticeStep.MIN_SPEED && rates[i] <= PracticeStep.MAX_SPEED)) throw Error($"songRates[{i}]");
            SongRates = Array.AsReadOnly(rates);
            Label = StyleText(label, "label");
            Emblem = StyleText(emblem, "emblem");
            if (color?.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit)) throw Error("style.color");
            Color = color.ToUpperInvariant();
            Tier = MedalTiers.IsKnown(tier) ? tier : throw Error("style.tier");
            Grade = string.IsNullOrEmpty(grade) ? null : MedalGrades.IsKnown(grade) ? grade : throw Error("style.grade");
            var style = new JObject { ["label"] = Label, ["emblem"] = Emblem, ["color"] = Color };
            // Left out when they say nothing, so a plain plate keeps the text and id it had before they existed.
            if (Tier != MedalTiers.LOWEST) style["tier"] = Tier;
            if (Grade != null) style["grade"] = Grade;
            var body = new JObject
            {
                ["initialLife"] = InitialLife,
                ["damage"] = new JObject { ["perfect"] = damage.Perfect, ["great"] = damage.Great,
                    ["good"] = damage.Good, ["bad"] = damage.Bad, ["miss"] = damage.Miss },
                ["songs"] = new JArray(links),
                ["style"] = style
            };
            // Keep legacy definitions byte-for-byte identical: their identifiers key medals and history.
            if (!AllowPause) body["allowPause"] = false;
            if (!FailOnZero) body["failOnZero"] = false;
            if (!RestEnabled) body["restEnabled"] = false;
            if (RestSeconds != DEFAULT_REST_SECONDS) body["restSeconds"] = RestSeconds;
            if (rates.Any(rate => rate != 1f)) body["songRates"] = new JArray(rates);
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
            CheckFields(body, "", "initialLife", "damage", "songs", "style", "allowPause", "failOnZero", "restEnabled", "restSeconds", "songRates");
            CheckFields(damage, "damage.", "perfect", "great", "good", "bad", "miss");
            CheckFields(style, "style.", "label", "emblem", "color", "tier", "grade");
            int Cost(string name) => Number(damage[name], "damage." + name);
            string Style(string name) => style[name]?.Type == JTokenType.String ? (string)style[name] : throw Error("style." + name);
            bool Flag(string name) => body[name] == null || (body[name].Type == JTokenType.Boolean ? (bool)body[name] : throw Error(name));
            float DecimalValue(JToken value, string field) =>
                value.Type is JTokenType.Float or JTokenType.Integer && float.TryParse(value.ToString(Formatting.None),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : throw Error(field);
            var rates = body["songRates"] == null ? null : body["songRates"] is JArray values
                ? values.Select((item, i) => DecimalValue(item, $"songRates[{i}]")).ToArray() : throw Error("songRates");
            return new DanDefinition(Number(body["initialLife"], "initialLife"),
                new DanDamage(Cost("perfect"), Cost("great"), Cost("good"), Cost("bad"), Cost("miss")),
                songs.Select((s, i) => s.Type == JTokenType.String ? (string)s : throw Error($"songs[{i}]")),
                Style("label"), Style("emblem"), Style("color"),
                style["tier"] == null ? MedalTiers.LOWEST : Number(style["tier"], "style.tier"),
                style["grade"] == null ? null : Style("grade"), Flag("allowPause"), Flag("failOnZero"), Flag("restEnabled"),
                body["restSeconds"] == null ? DEFAULT_REST_SECONDS : DecimalValue(body["restSeconds"], "restSeconds"), rates);
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
