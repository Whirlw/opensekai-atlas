using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenSekai.Charts
{
    /// <summary>NextRUSH LevelData, retaining named entities and their native fields.</summary>
    public sealed class NextRushChart
    {
        public const int MAX_JSON_CHARACTERS = 32 * 1024 * 1024;
        public const int MAX_ENTITIES = 100000;
        public const double MAX_ABSOLUTE_TIME_SECONDS = 1000000;
        public double BgmOffset { get; }
        public IReadOnlyList<NextRushEntity> Entities { get; }
        public IReadOnlyDictionary<string, NextRushEntity> NamedEntities { get; }

        private NextRushChart(double offset, List<NextRushEntity> entities, Dictionary<string, NextRushEntity> named)
        {
            BgmOffset = offset;
            Entities = entities.AsReadOnly();
            NamedEntities = new System.Collections.ObjectModel.ReadOnlyDictionary<string, NextRushEntity>(named);
        }

        public static NextRushChart Parse(string json)
        {
            if (json == null || json.Length > MAX_JSON_CHARACTERS)
                throw new InvalidDataException("NextRUSH+ 谱面为空或超过大小限制。");
            using var input = new StringReader(json);
            using var reader = new JsonTextReader(input) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("NextRUSH+ 谱面包含多余的 JSON 内容。");
            double offset = NextRushEntity.ReadNumber(root["bgmOffset"], "bgmOffset");
            if (!(root["entities"] is JArray array) || array.Count > MAX_ENTITIES)
                throw new InvalidDataException("NextRUSH+ entities 无效或数量超过限制。");
            var entities = new List<NextRushEntity>(array.Count);
            var named = new Dictionary<string, NextRushEntity>(StringComparer.Ordinal);
            foreach (var token in array)
            {
                if (!(token is JObject entity)) throw new InvalidDataException("NextRUSH+ 实体必须为对象。");
                var parsed = new NextRushEntity(entity);
                entities.Add(parsed);
                if (parsed.Name != null)
                {
                    if (named.ContainsKey(parsed.Name)) throw new InvalidDataException("NextRUSH+ 实体名称重复：" + parsed.Name);
                    named.Add(parsed.Name, parsed);
                }
            }
            return new NextRushChart(offset, entities, named);
        }
    }

    public sealed class NextRushEntity
    {
        private readonly Dictionary<string, JObject> _data = new Dictionary<string, JObject>(StringComparer.Ordinal);
        public string Name { get; }
        public string Archetype { get; }
        public IEnumerable<string> Fields => _data.Keys;

        internal NextRushEntity(JObject entity)
        {
            if (entity["archetype"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)entity["archetype"]))
                throw new InvalidDataException("NextRUSH+ 实体缺少 archetype。");
            Archetype = (string)entity["archetype"];
            if (entity["name"] != null && entity["name"].Type != JTokenType.String)
                throw new InvalidDataException("NextRUSH+ 实体 name 必须为字符串。");
            Name = (string)entity["name"];
            if (Name != null && Name.Length == 0) throw new InvalidDataException("NextRUSH+ 实体 name 不能为空。");
            if (!(entity["data"] is JArray data)) throw new InvalidDataException("NextRUSH+ 实体缺少 data。");
            foreach (var token in data)
            {
                if (!(token is JObject field) || field["name"]?.Type != JTokenType.String)
                    throw new InvalidDataException("NextRUSH+ 实体字段无效。");
                string key = (string)field["name"];
                if (string.IsNullOrEmpty(key) || _data.ContainsKey(key)) throw new InvalidDataException("NextRUSH+ 实体字段重复或为空：" + key);
                bool value = field["value"] != null, reference = field["ref"] != null;
                if (value == reference) throw new InvalidDataException("NextRUSH+ 字段必须含 value 或 ref：" + key);
                if (value) ReadNumber(field["value"], key);
                if (reference && field["ref"].Type != JTokenType.String)
                    throw new InvalidDataException("NextRUSH+ ref 必须为字符串：" + key);
                _data.Add(key, field);
            }
        }

        public bool Has(string field) => _data.ContainsKey(field);
        public double Number(string field, double fallback = 0)
        {
            return _data.TryGetValue(field, out var data) ? ReadNumber(data["value"], field) : fallback;
        }
        public string Reference(string field)
        {
            if (!_data.TryGetValue(field, out var data)) return null;
            if (data["ref"]?.Type != JTokenType.String) throw new InvalidDataException("NextRUSH+ 字段需要实体引用：" + field);
            return (string)data["ref"];
        }
        internal static double ReadNumber(JToken token, string field)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw new InvalidDataException("NextRUSH+ 字段需要数值：" + field);
            double result = (double)token;
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new InvalidDataException("NextRUSH+ 数值必须有限：" + field);
            return result;
        }
    }
}
