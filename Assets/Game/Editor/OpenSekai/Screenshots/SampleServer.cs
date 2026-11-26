using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    /// <summary>
    /// A Sonolus server that only exists inside a sample workspace: requests to
    /// <see cref="BASE_URL"/> are answered from files this writes, with the sample library's songs
    /// and covers, so the server pages can be pictured without anyone else's charts. Nothing is sent
    /// to the network.
    /// </summary>
    [InitializeOnLoad]
    internal static class SampleServer
    {
        internal const string BASE_URL = "https://charts.example.com";
        internal const string NAME = "示例谱面站";

        private const string API = BASE_URL + "/sonolus";
        private const string COVERS = BASE_URL + "/covers/";
        private const string AUDIO = BASE_URL + "/audio/";
        private const int SECTION_SIZE = 12;

        private static readonly string[] _commenters = { "玩家甲", "玩家乙", "玩家丙", "玩家丁" };
        private static readonly int[] _commentHours = { 2, 9, 26, 50 };
        private static readonly string[] _comments =
        {
            "中段的交替很顺手，副歌的滑条跟着主旋律走，读谱舒服。", "尾杀比标级难一点，其他部分很友好。",
            "谢谢谱师！第一次 FULL COMBO 就是这首。", "求一张 EXPERT 难度。"
        };

        static SampleServer()
        {
            if (!SampleLibrary.IsWorkspace)
            {
                return;
            }

            SonolusClient.CaptureResponder = Answer;
            ChartRankCache.CaptureServer = BASE_URL;
        }

        /// <summary>The file that answers <paramref name="url"/>; null for any other server.</summary>
        private static string Answer(string url)
        {
            if (!url.StartsWith(BASE_URL + "/", StringComparison.Ordinal))
            {
                return null;
            }

            string path = url.Split('?')[0];
            if (path.StartsWith(COVERS, StringComparison.Ordinal))
            {
                string slug = Path.GetFileNameWithoutExtension(path);
                return FileUrl(SampleLibrary.Songs.FirstOrDefault(song => song.Slug == slug)?.CoverPath);
            }

            if (path.StartsWith(AUDIO, StringComparison.Ordinal))
            {
                string slug = Path.GetFileNameWithoutExtension(path);
                SampleLibrary.Song song = SampleLibrary.Songs.FirstOrDefault(candidate => candidate.Slug == slug);
                return FileUrl(song == null ? null
                    : Path.Combine(OpenSekai.Shared.Packages.SongPackageStorage.RootDirectory, song.Folder, "audio.wav"));
            }

            return path.StartsWith(API + "/", StringComparison.Ordinal) ? WriteJson(path, url) : FileUrl(null);
        }

        private static string WriteJson(string path, string url)
        {
            JObject body = Body(path.Substring(API.Length).Trim('/').Split('/'), url);
            if (body == null)
            {
                return FileUrl(null);
            }

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/OpenSekaiShots/sample-server"));
            Directory.CreateDirectory(directory);
            string name;
            using (SHA1 sha = SHA1.Create())
            {
                name = Hex(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url)));
            }

            string file = Path.Combine(directory, name + ".json");
            File.WriteAllText(file, body.ToString(Formatting.None));
            return FileUrl(file);
        }

        private static JObject Body(string[] path, string url)
        {
            if (path.Length == 1 && path[0] == "info")
            {
                return new JObject { ["title"] = NAME, ["buttons"] = new JArray(new JObject { ["type"] = "level" }) };
            }

            if (path[0] != "levels" || path.Length < 2)
            {
                return null;
            }

            if (path[1] == "info")
            {
                return new JObject
                {
                    ["searches"] = new JArray(Search()),
                    ["sections"] = new JArray(Section("#NEWEST", "star", 0), Section("#POPULAR", "heart", 9), Section("#RANDOM", null, 15))
                };
            }

            if (path[1] == "list")
            {
                bool first = !url.Contains("page=") || url.Contains("page=0");
                return new JObject
                {
                    ["pageCount"] = 1, ["items"] = first ? new JArray(SampleLibrary.Songs.Select(Level)) : new JArray(),
                    ["searches"] = new JArray(Search())
                };
            }

            SampleLibrary.Song song = SampleLibrary.Songs.FirstOrDefault(candidate => candidate.LevelName == Uri.UnescapeDataString(path[1]));
            if (song == null)
            {
                return null;
            }

            if (path.Length == 2)
            {
                return Details(song);
            }

            switch (path[2])
            {
                case "community" when path[path.Length - 1] == "info":
                    return new JObject { ["actions"] = new JArray(), ["topComments"] = new JArray() };
                case "community":
                    return new JObject { ["pageCount"] = 1, ["comments"] = new JArray(_comments.Select(Comment)) };
                case "leaderboards" when path[path.Length - 1] == "list":
                    return new JObject { ["pageCount"] = 1, ["records"] = new JArray(Enumerable.Range(0, SECTION_SIZE).Select(Record)) };
                case "leaderboards":
                    return new JObject { ["topRecords"] = new JArray() };
                default:
                    return null;
            }
        }

        private static JObject Section(string title, string icon, int first)
        {
            int count = SampleLibrary.Songs.Count;
            var items = Enumerable.Range(0, SECTION_SIZE).Select(i => Level(SampleLibrary.Songs[(first + i) % count]));
            return new JObject { ["title"] = title, ["icon"] = icon, ["itemType"] = "level", ["items"] = new JArray(items) };
        }

        private static JObject Level(SampleLibrary.Song song)
        {
            var tags = new JArray(
                new JObject { ["title"] = char.ToUpperInvariant(song.Difficulty[0]) + song.Difficulty.Substring(1) },
                new JObject { ["title"] = Number(40 + song.Index * 37 % 260), ["icon"] = "heart" },
                new JObject { ["title"] = Number(3 + song.Index * 7 % 40), ["icon"] = "comment" });
            if (song.Publication == SampleLibrary.Publication.Ranked)
            {
                tags.Add(new JObject { ["title"] = "精选", ["icon"] = "trophy" });
            }

            return new JObject
            {
                ["name"] = song.LevelName, ["source"] = string.Empty, ["version"] = 1, ["rating"] = song.Level, ["title"] = song.Title,
                ["artists"] = song.Singer + " / " + song.Composer, ["author"] = song.Charter, ["tags"] = tags,
                ["engine"] = new JObject { ["name"] = SonolusLevelInstaller.SUPPORTED_ENGINE, ["version"] = 13, ["title"] = "Next RUSH+" },
                ["cover"] = new JObject { ["hash"] = Sha1(song.CoverPath), ["url"] = "/covers/" + song.Slug + ".png" },
                ["bgm"] = new JObject { ["hash"] = string.Empty, ["url"] = "/audio/" + song.Slug + ".wav" },
                ["data"] = new JObject { ["hash"] = string.Empty, ["url"] = "/data/" + song.Slug }
            };
        }

        private static JObject Details(SampleLibrary.Song song) => new JObject
        {
            ["item"] = Level(song),
            ["description"] = "这是用于界面示例的虚构谱面简介。\n\n· 4/4 拍，BPM 168，双押与滑条为主\n· 尾段有一次变速，建议流速 10.5 以上\n· 欢迎在社区留言反馈读谱体验",
            ["actions"] = new JArray(),
            ["hasCommunity"] = true,
            ["leaderboards"] = new JArray(Board("arcade", "街机分数"), Board("accuracy", "准确度分数"), Board("combo", "最少断连")),
            ["sections"] = new JArray()
        };

        private static JObject Board(string name, string title) => new JObject { ["name"] = name, ["title"] = title };

        private static JObject Comment(string content, int index) => new JObject
        {
            ["name"] = "comment" + Number(index), ["author"] = _commenters[index], ["content"] = content, ["actions"] = new JArray(),
            ["time"] = DateTimeOffset.UtcNow.AddHours(-_commentHours[index]).ToUnixTimeMilliseconds()
        };

        private static JObject Record(int index) => new JObject
        {
            ["name"] = "record" + Number(index), ["rank"] = "#" + Number(index + 1), ["player"] = "玩家 " + (char)('A' + index),
            ["value"] = (1000000 - index * 1837).ToString("N0", CultureInfo.InvariantCulture)
        };

        /// <summary>One search form with a field of every type the game draws.</summary>
        private static JObject Search() => new JObject
        {
            ["type"] = "advanced", ["title"] = "高级搜索", ["icon"] = "advanced",
            ["options"] = new JArray(
                Select("staff_pick", "精选", "off", ("off", "不限"), ("true", "仅精选谱面"), ("false", "仅非精选谱面")),
                Slider("min_rating", "最低等级", 1), Slider("max_rating", "最高等级", 40),
                Text("title_includes", "标题包含"), Text("author_includes", "谱师名称包含"),
                new JObject { ["query"] = "liked", ["name"] = "只看收藏", ["type"] = "toggle", ["def"] = false },
                Select("sort_by", "排序方式", "published_at", ("published_at", "发布时间"), ("rating", "等级"), ("likes", "点赞数")),
                new JObject
                {
                    ["query"] = "difficulties", ["name"] = "难度", ["type"] = "multi", ["def"] = new JArray(true, true, true, false),
                    ["values"] = new JArray(Value("hard", "HARD"), Value("expert", "EXPERT"), Value("master", "MASTER"), Value("append", "APPEND"))
                })
        };

        private static JObject Select(string query, string name, string selected, params (string Name, string Title)[] values) => new JObject
        {
            ["query"] = query, ["name"] = name, ["type"] = "select", ["def"] = selected,
            ["values"] = new JArray(values.Select(value => Value(value.Name, value.Title)))
        };

        private static JObject Slider(string query, string name, int initial) => new JObject
        {
            ["query"] = query, ["name"] = name, ["type"] = "slider", ["def"] = initial, ["min"] = 1, ["max"] = 40, ["step"] = 1
        };

        private static JObject Text(string query, string name) => new JObject
        {
            ["query"] = query, ["name"] = name, ["type"] = "text", ["def"] = string.Empty, ["placeholder"] = "输入文字", ["limit"] = 100
        };

        private static JObject Value(string name, string title) => new JObject { ["name"] = name, ["title"] = title };

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Sha1(string path)
        {
            using (SHA1 sha = SHA1.Create())
            {
                return Hex(sha.ComputeHash(File.ReadAllBytes(path)));
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        /// <summary>A path that does not exist fails the request, which keeps it off the network.</summary>
        private static string FileUrl(string path) =>
            new Uri(path ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/OpenSekaiShots/sample-server/none"))).AbsoluteUri;
    }
}
