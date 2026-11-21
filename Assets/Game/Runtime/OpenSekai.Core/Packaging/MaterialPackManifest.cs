using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenSekai.MaterialPacks
{
    public abstract class MaterialMediaSlot
    {
        public string File { get; set; }
        public string Hash { get; set; }
    }

    public sealed class MaterialAudioSlot : MaterialMediaSlot
    {
        public int Channels { get; set; }
        public int Frames { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
    }

    public sealed class MaterialPackGroup
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; } = "audio";
        public MaterialBackgroundScene Background { get; set; }
        public MaterialComboLayout Layout { get; set; }
        public Dictionary<string, MaterialAudioSlot> Slots { get; set; } = new();
        public Dictionary<string, MaterialSpriteSlot> Sprites { get; set; } = new();
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
            IReadOnlyDictionary<string, (string hash, MaterialPackWave wave)> media,
            IReadOnlyDictionary<string, (string hash, int width, int height)> images = null)
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
                var result = new MaterialPackGroup { Id = Id(group, "id") };
                if (!ids.Add(result.Id)) throw Error(group, "重复组 ID " + result.Id);
                if (group["displayName"] != null) result.DisplayName = Required(group, "displayName");
                result.Category = Required(group, "category");
                if (result.Category is not ("audio" or "combo" or "judgment" or "hitFx" or "notes" or "lane" or "hud" or "resultFx" or "background")) throw Error(group, "不支持的类别 " + result.Category);
                if (result.Category == "background")
                {
                    Fields(group, "id", "category", "displayName", "layers", "preferVideo", "extensions");
                    if (group["layers"] is not JArray layers || layers.Count is < 1 or > MaterialBackgroundScene.MAX_LAYERS)
                        throw Error(group, "背景需要 1–256 个图层");
                    if (group["preferVideo"] != null && group["preferVideo"].Type != JTokenType.Boolean)
                        throw Error(group, "preferVideo 需要布尔值");
                    result.Background = new MaterialBackgroundScene { PreferVideo = (bool?)group["preferVideo"] ?? true,
                        Layers = layers.Select(layer => ParseBackgroundLayer(layer, images, false)).ToArray() };
                    pack.Groups.Add(result);
                    continue;
                }
                Fields(group, "id", "category", "displayName", "slots", "layout", "templateSet", "extensions");
                int? templateSet = null;
                if (group.TryGetValue("templateSet", out var template))
                {
                    templateSet = Integer(template);
                    if (result.Category != "hitFx" || templateSet is not (1 or 2))
                        throw Error(template, "templateSet 仅适用于 hitFx 组，可选 1 或 2");
                }
                if (group.TryGetValue("layout", out var layout))
                {
                    if (result.Category != "combo" || layout is not JObject obj) throw Error(layout, "需要 COMBO 布局对象");
                    Fields(obj, "position", "scale", "digitsPosition", "burstPosition", "glowPosition");
                    result.Layout = new MaterialComboLayout { Position = Pair(obj, "position"), Scale = Pair(obj, "scale"),
                        DigitsPosition = Pair(obj, "digitsPosition"), BurstPosition = Pair(obj, "burstPosition"), GlowPosition = Pair(obj, "glowPosition") };
                    if (!result.Layout.IsValid()) throw Error(layout, "COMBO 布局数值越界");
                }
                if (group["slots"] is not JObject slots || !slots.HasValues && result.Layout == null) throw Error(group, "需要槽位或 COMBO 布局");
                foreach (var property in slots.Properties())
                {
                    if (result.Category != "audio")
                    {
                        var sprite = ParseSprite(result.Category, property, images);
                        // Store the group default on each primary slot so per-slot overrides carry their template.
                        if (templateSet.HasValue && !MaterialHitFxSlots.IsTrail(property.Name)) sprite.TemplateSet = templateSet;
                        result.Sprites.Add(property.Name, sprite);
                        continue;
                    }
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

        internal static bool HasSpriteSlot(string category, string slot) => category switch
        {
            "combo" => MaterialComboSlots.HasSlot(slot),
            "judgment" => MaterialJudgmentSlots.HasSlot(slot),
            "notes" => MaterialNoteSlots.HasSlot(slot),
            "resultFx" => MaterialResultFxSlots.HasSlot(slot),
            "hud" => MaterialHudSlots.HasSlot(slot),
            "lane" => MaterialLaneSlots.HasSlot(slot),
            "hitFx" => MaterialHitFxSlots.HasSlot(slot),
            _ => false
        };

        private static MaterialSpriteSlot ParseSprite(string category, JProperty property, IReadOnlyDictionary<string, (string hash, int width, int height)> images)
        {
            bool known = HasSpriteSlot(category, property.Name);
            if (!known || property.Value is not JObject slot)
                throw Error(property, category + " 槽位或槽位值无效");
            if (MaterialSpriteSlot.IsLayout(property.Name))
            {
                Fields(slot, "position", "scale", "extensions");
                var layout = new MaterialSpriteSlot { Position = Pair(slot, "position"), Scale = Pair(slot, "scale") };
                if (!layout.IsValid(property.Name)) throw Error(slot, "布局数值无效");
                return layout;
            }
            if (category == "hitFx")
            {
                if (!MaterialHitFxSlots.IsTrail(property.Name) && slot["kind"]?.Type == JTokenType.String && (string)slot["kind"] == "sequence")
                    return ParseSequence(property.Name, slot, images);
                if (MaterialHitFxSlots.IsTrail(property.Name)) Fields(slot, "file", "extensions");
                else Fields(slot, "file", "scale", "position", "extensions");
            }
            else if (category == "resultFx")
            {
                if (MaterialResultFxSlots.IsEvent(property.Name))
                {
                    if (Required(slot, "kind") != "sequence") throw Error(slot, "结算演出需要 sequence");
                    return ParseSequence(property.Name, slot, images);
                }
                if (MaterialResultFxSlots.IsTexture(property.Name)) Fields(slot, "file", "extensions");
                else if (property.Name == MaterialResultFxSlots.BACKGROUND) Fields(slot, "file", "rect", "pivot", "size", "scale", "position", "extensions");
                else Fields(slot, "file", "rect", "pivot", "size", "scale", "extensions");
            }
            else if (category == "hud")
            {
                if (MaterialHudSlots.IsRing(property.Name)) Fields(slot, "file", "scale", "position", "extensions");
                else Fields(slot, "file", "rect", "pivot", "size", "scale", "position", "border", "drawMode", "extensions");
            }
            else if (category == "lane") Fields(slot, "file", "rect", "pivot", "size", "scale", "position", "extensions");
            else if (category == "notes")
            {
                if (MaterialNoteSlots.IsLine(property.Name)) Fields(slot, "file", "scale", "position", "extensions");
                else Fields(slot, "file", "rect", "pivot", "scale", "position", "extensions");
            }
            else Fields(slot, "file", "rect", "pivot", "scale", "size", "position", "offset", "advance", "opacity", "extensions");
            string file = Required(slot, "file");
            if (images == null || !images.TryGetValue(file, out var image)) throw Error(slot, "找不到 PNG " + file);
            var sprite = new MaterialSpriteSlot { File = file, Hash = image.hash, Width = image.width, Height = image.height,
                Rect = new[] { 0, 0, image.width, image.height } };
            if (slot["rect"] is JToken rect)
            {
                if (rect is not JArray items || items.Count != 4) throw Error(rect, "需要 [x,y,w,h]");
                if (category == "notes") sprite.NoteSlice = items.Select(Number).ToArray();
                else sprite.Rect = items.Select(Integer).ToArray();
            }
            if (slot["drawMode"] != null) sprite.DrawMode = Required(slot, "drawMode");
            sprite.Border = Numbers(slot, "border", 4);
            sprite.Pivot = Pair(slot, "pivot");
            sprite.Scale = Pair(slot, "scale");
            sprite.Size = Pair(slot, "size");
            sprite.Position = Pair(slot, "position");
            sprite.Offset = Pair(slot, "offset");
            if (slot["opacity"] != null) sprite.Opacity = Number(slot["opacity"]);
            if (slot["advance"] != null) sprite.Advance = Number(slot["advance"]);
            if (!sprite.IsValid(property.Name)) throw Error(slot, "切片或布局数值越界");
            return sprite;

        }

        private static MaterialBackgroundLayer ParseBackgroundLayer(JToken token, IReadOnlyDictionary<string, (string hash, int width, int height)> images, bool mask)
        {
            if (token is not JObject obj) throw Error(token, "需要背景图层对象");
            string type = mask ? "image" : Required(obj, "type");
            if (mask) Fields(obj, "file", "rect", "size", "scale", "position", "pivot", "rotation", "depth", "cutoff", "extensions");
            else if (type == "image") Fields(obj, "type", "file", "rect", "size", "scale", "position", "pivot", "rotation", "depth", "fit", "color", "mask", "flipX", "flipY", "extensions");
            else if (type == "jacket") Fields(obj, "type", "size", "scale", "position", "pivot", "rotation", "depth", "fit", "color", "mask", "flipX", "flipY", "extensions");
            else throw Error(obj, "图层类型需要 image 或 jacket");
            var layer = new MaterialBackgroundLayer { Type = type, Size = Pair(obj, "size"), Scale = Pair(obj, "scale"),
                Position = Pair(obj, "position"), Pivot = Pair(obj, "pivot"), Fit = obj["fit"] == null ? null : Required(obj, "fit"),
                Depth = obj["depth"] == null ? 0 : Number(obj["depth"]), Rotation = Numbers(obj, "rotation", 3),
                FlipX = Flag(obj, "flipX"), FlipY = Flag(obj, "flipY"), Color = Numbers(obj, "color", 4), Cutoff = obj["cutoff"] == null ? .5f : Number(obj["cutoff"]) };
            if (type == "image")
            {
                string path = Required(obj, "file");
                if (images == null || !images.TryGetValue(path, out var image)) throw Error(obj, "找不到 PNG " + path);
                layer.Image = new MaterialSpriteSlot { File = path, Hash = image.hash, Width = image.width, Height = image.height,
                    Rect = obj["rect"] == null ? new[] { 0, 0, image.width, image.height } : Integers(obj, "rect", 4), Pivot = layer.Pivot };
            }
            if (obj["mask"] is JArray masks)
            {
                if (masks.Count is < 1 or > 16) throw Error(masks, "一层可使用 1–16 个遮罩");
                layer.Masks = masks.Select(m => ParseBackgroundLayer(m, images, true)).ToArray();
            }
            else if (obj["mask"] != null) layer.Masks = new[] { ParseBackgroundLayer(obj["mask"], images, true) };
            if (!layer.IsValid(mask)) throw Error(obj, "背景图层切片或布局数值越界");
            return layer;
        }

        private static bool Flag(JObject obj, string field) => obj[field] == null ? false :
            obj[field].Type == JTokenType.Boolean ? (bool)obj[field] : throw Error(obj[field], "需要布尔值");

        private static float[] Numbers(JObject obj, string field, int count)
        {
            if (obj[field] == null) return null;
            if (obj[field] is not JArray array || array.Count != count) throw Error(obj[field], "需要 " + count + " 个数值");
            return array.Select(Number).ToArray();
        }

        private static MaterialSpriteSlot ParseSequence(string key, JObject slot,
            IReadOnlyDictionary<string, (string hash, int width, int height)> images)
        {
            Fields(slot, "kind", "layers", "position", "scale", "extensions");
            if (slot["layers"] is not JArray items || items.Count is < 1 or > 8) throw Error(slot, "序列需要 1–8 层");
            var layers = new List<MaterialEffectLayer>();
            foreach (var item in items)
            {
                if (item is not JObject layer) throw Error(item, "需要序列层对象");
                Fields(layer, "file", "anchor", "size", "scale", "offset", "pivot", "frames", "loopFrameRange", "extensions");
                string anchor = MaterialResultFxSlots.IsEvent(key) || key == "hitFx.screen.damage" ? "center" : "hit";
                if (layer["anchor"] != null && Required(layer, "anchor") != anchor) throw Error(layer, "此槽的锚点为 " + anchor);
                string file = Required(layer, "file");
                if (images == null || !images.TryGetValue(file, out var image)) throw Error(layer, "找不到 PNG " + file);
                if (layer["frames"] is not JArray frames || frames.Count is < 1 or > 256) throw Error(layer, "需要 1–256 帧");
                var parsed = new MaterialEffectLayer { File = file, Hash = image.hash, Width = image.width, Height = image.height,
                    Pivot = Pair(layer, "pivot"), Offset = Pair(layer, "offset"), Size = Pair(layer, "size"), Scale = Pair(layer, "scale"),
                    Frames = frames.Select(frame =>
                    {
                        if (frame is not JObject f) throw Error(frame, "需要帧对象");
                        Fields(f, "rect", "durationMs");
                        return new MaterialEffectFrame { Rect = Integers(f, "rect", 4), DurationMs = Integer(f["durationMs"]) };
                    }).ToArray(),
                    LoopFrameRange = layer["loopFrameRange"] == null ? null : Integers(layer, "loopFrameRange", 2) };
                if (!parsed.IsValid(key)) throw Error(layer, "序列帧、循环或布局越界");
                layers.Add(parsed);
            }
            var first = layers[0];
            var result = new MaterialSpriteSlot { File = first.File, Hash = first.Hash, Width = first.Width, Height = first.Height,
                Rect = new[] { 0, 0, first.Width, first.Height }, Layers = layers.ToArray(),
                Position = Pair(slot, "position"), Scale = Pair(slot, "scale") };
            if (!result.IsValid(key)) throw Error(slot, "序列布局越界");
            return result;
        }

        private static int[] Integers(JObject obj, string field, int count)
        {
            if (obj[field] is not JArray array || array.Count != count) throw Error(obj, field + " 需要 " + count + " 个整数");
            return array.Select(Integer).ToArray();
        }

        private static float[] Pair(JObject obj, string field)
        {
            if (obj[field] == null) return null;
            if (obj[field] is not JArray values || values.Count != 2) throw Error(obj[field], "需要两个数值");
            return values.Select(Number).ToArray();
        }

        private static float Number(JToken token)
        {
            if (token.Type is not (JTokenType.Float or JTokenType.Integer)) throw Error(token, "需要有限数值");
            double value = (double)token;
            if (double.IsNaN(value) || Math.Abs(value) > float.MaxValue) throw Error(token, "需要有限数值");
            return (float)value;
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
