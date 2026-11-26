using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    /// <summary>
    /// A library that is safe to publish pictures of: fictional songs whose covers are drawn for
    /// this project (ArtSource/SampleCovers), quiet generated audio, made-up credits and runs, and a
    /// resource pack. The style guide's screenshots and promo recordings show it in place of a
    /// player's own songs.
    ///
    /// It only installs into a sample workspace (<see cref="IsWorkspace"/>), a copy of the project
    /// whose company name gives it a data folder of its own, so it can never write into a player's
    /// library. Tools/new-sample-workspace.ps1 makes one.
    /// </summary>
    internal static class SampleLibrary
    {
        /// <summary>Company name that marks a sample workspace; Application.persistentDataPath follows from it.</summary>
        internal const string WORKSPACE_COMPANY = "OpenSekaiSample";

        private const string COVER_DIRECTORY = "../ArtSource/SampleCovers";
        private const string BASE_SCORE = "../Tools/fixtures/baseline_tap/score.json";
        private const string BASE_TEMPO = "\"changeValue\":120}";
        private const int BASE_BPM = 120;
        private const float FILLER_SECONDS = 9f;

        /// <summary>The song whose audio is left out, so the list shows 缺少音频.</summary>
        private const string WITHOUT_AUDIO = "Faraway Choir";

        /// <summary>The song whose cover is left out, so the page shows its placeholder.</summary>
        private const string WITHOUT_COVER = "Northbound";

        internal sealed class Song
        {
            internal Song(int index, string title, string singer, string composer, string charter, string difficulty, int level,
                int seconds, Publication publication)
            {
                Index = index;
                Title = title;
                Singer = singer;
                Composer = composer;
                Charter = charter;
                Difficulty = difficulty;
                Level = level;
                Seconds = seconds;
                Publication = publication;
            }

            internal int Index { get; }
            internal string Title { get; }
            internal string Singer { get; }
            internal string Composer { get; }
            internal string Charter { get; }
            internal string Difficulty { get; }
            internal int Level { get; }
            internal int Seconds { get; }
            internal Publication Publication { get; }

            internal string Slug => Title.ToLowerInvariant().Replace(' ', '-');

            /// <summary>The level's name on <see cref="SampleServer"/>; Ranked lookups expect this shape.</summary>
            internal string LevelName => "UnCh-" + (Index + 1).ToString("x32", CultureInfo.InvariantCulture);

            internal string Folder => Title + "_sample" + Index.ToString("00", CultureInfo.InvariantCulture);

            internal string CoverPath => Path.GetFullPath(Path.Combine(Application.dataPath, COVER_DIRECTORY, Slug + ".png"));
        }

        /// <summary>Where a song says it came from.</summary>
        internal enum Publication
        {
            /// <summary>No source on record.</summary>
            Local,

            /// <summary>Downloaded from the sample server, not Ranked.</summary>
            Server,

            /// <summary>Downloaded from the sample server and Ranked.</summary>
            Ranked
        }

        internal static readonly IReadOnlyList<Song> Songs = Build(
            ("Aurora Line", "Mira", "Kite Harbor", "示例谱师 A", "master", 31, 128, Publication.Ranked),
            ("Night Signal", "Noctua", "Mono Fields", "示例谱师 B", "expert", 27, 141, Publication.Server),
            ("Paper Comet", "Mira", "Paper Orchard", "示例谱师 A", "append", 33, 117, Publication.Ranked),
            ("Glass Garden", "Lilt", "Kite Harbor", "示例谱师 C", "master", 29, 132, Publication.Ranked),
            ("Lumen Drive", "Vanta 7", "Mono Fields", "示例谱师 B", "master", 32, 125, Publication.Ranked),
            ("Echo Bloom", "Lilt", "Petal Works", "示例谱师 D", "hard", 19, 138, Publication.Local),
            ("Orbit Parade", "Noctua", "Star Cartel", "示例谱师 A", "expert", 26, 122, Publication.Server),
            ("Silver Static", "Vanta 7", "Mono Fields", "示例谱师 C", "append", 35, 119, Publication.Local),
            ("Velvet Prism", "Mira", "Petal Works", "示例谱师 B", "master", 30, 134, Publication.Ranked),
            ("Cobalt Hour", "Noctua", "Kite Harbor", "示例谱师 D", "normal", 14, 129, Publication.Local),
            ("Neon Tide", "Vanta 7", "Star Cartel", "示例谱师 A", "expert", 25, 115, Publication.Server),
            ("Starlit Atlas", "Mira", "Star Cartel", "示例谱师 C", "master", 33, 147, Publication.Ranked),
            ("Hollow Bell", "Lilt", "Paper Orchard", "示例谱师 B", "hard", 18, 126, Publication.Local),
            ("Crimson Lattice", "Vanta 7", "Mono Fields", "示例谱师 D", "append", 36, 121, Publication.Ranked),
            ("Ivory Circuit", "Noctua", "Paper Orchard", "示例谱师 A", "easy", 7, 118, Publication.Local),
            ("Moth and Lantern", "Lilt", "Petal Works", "示例谱师 C", "master", 28, 136, Publication.Local),
            ("Tidal Index", "Mira", "Kite Harbor", "示例谱师 B", "expert", 24, 123, Publication.Local),
            ("Quiet Engine", "Noctua", "Mono Fields", "示例谱师 D", "master", 30, 131, Publication.Local),
            ("Saffron Relay", "Vanta 7", "Star Cartel", "示例谱师 A", "master", 31, 127, Publication.Ranked),
            (WITHOUT_AUDIO, "Lilt", "Petal Works", "示例谱师 C", "hard", 20, 144, Publication.Local),
            ("Lantern Code", "Mira", "Paper Orchard", "示例谱师 B", "expert", 28, 120, Publication.Local),
            (WITHOUT_COVER, "Noctua", "Kite Harbor", "示例谱师 D", "normal", 15, 133, Publication.Local));

        /// <summary>True in a sample workspace, the only place sample content installs or the sample server answers.</summary>
        internal static bool IsWorkspace => Application.companyName == WORKSPACE_COMPANY;

        internal static void RequireWorkspace()
        {
            if (!IsWorkspace)
            {
                throw new InvalidOperationException("Sample content only installs into a sample workspace, so it cannot reach your own "
                    + "library. Make one with Tools/new-sample-workspace.ps1 and run this there.");
            }
        }

        /// <summary>Writes every song's package, replacing an earlier install of it. Player data is left alone.</summary>
        internal static void WritePackages()
        {
            RequireWorkspace();
            string baseScore = File.ReadAllText(Path.Combine(Application.dataPath, BASE_SCORE));
            int tempo = baseScore.IndexOf(BASE_TEMPO, StringComparison.Ordinal);
            if (tempo < 0)
            {
                throw new InvalidDataException("The base score has no " + BASE_BPM + " BPM event to vary.");
            }

            DateTime newest = DateTime.UtcNow.AddDays(-1);
            foreach (Song song in Songs)
            {
                string directory = Path.Combine(SongPackageStorage.RootDirectory, song.Folder);
                Directory.CreateDirectory(directory);
                DateTime imported = newest.AddMinutes(-90 * song.Index);
                var manifest = new JObject
                {
                    ["formatVersion"] = 1, ["id"] = "sample" + song.Index.ToString("00", CultureInfo.InvariantCulture),
                    ["title"] = song.Title, ["scoreTitle"] = song.Title, ["composer"] = song.Composer, ["lyricist"] = song.Composer,
                    ["arranger"] = song.Composer, ["singer"] = song.Singer, ["collaborationLabel"] = string.Empty,
                    ["description"] = string.Empty, ["audioFileName"] = "audio.wav", ["jacketFileName"] = "jacket.png",
                    ["scoreFileName"] = "score.json", ["videoFileName"] = string.Empty, ["fillerSec"] = FILLER_SECONDS,
                    ["previewStartTimeSec"] = 8f, ["userName"] = song.Charter, ["musicDifficultyType"] = song.Difficulty,
                    ["playLevel"] = song.Level, ["importedAtUtc"] = imported.ToString("O", CultureInfo.InvariantCulture)
                };
                File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToString(Formatting.Indented));

                // Runs are kept by the score file's hash, so each song needs a file of its own.
                string bpm = (BASE_BPM + song.Index).ToString(CultureInfo.InvariantCulture);
                File.WriteAllText(Path.Combine(directory, "score.json"),
                    baseScore.Substring(0, tempo) + "\"changeValue\":" + bpm + "}" + baseScore.Substring(tempo + BASE_TEMPO.Length));
                if (song.Title != WITHOUT_AUDIO)
                {
                    File.WriteAllBytes(Path.Combine(directory, "audio.wav"), Pad(song.Seconds, song.Index));
                }

                if (song.Title != WITHOUT_COVER)
                {
                    File.Copy(song.CoverPath, Path.Combine(directory, "jacket.png"), true);
                }

                if (song.Publication != Publication.Local)
                {
                    var source = new JObject { ["server"] = SampleServer.BASE_URL, ["name"] = song.LevelName, ["version"] = 1 };
                    File.WriteAllText(Path.Combine(directory, SonolusLevelInstaller.SOURCE_FILE_NAME), source.ToString(Formatting.Indented));
                }

                foreach (string file in Directory.GetFiles(directory))
                {
                    File.SetLastWriteTimeUtc(file, imported);
                }
            }
        }

        /// <summary>
        /// Records the songs' Ranked verdicts, and runs for most of them when the workspace has none
        /// yet. Needs the packages to be listed, so it runs in Play Mode.
        /// </summary>
        internal static void SeedRecords()
        {
            RequireWorkspace();
            Dictionary<string, SongPackageItem> items = SongPackageManager.Shared.LoadItems()
                .Where(item => !string.IsNullOrEmpty(item?.ScoreKey))
                .GroupBy(item => Path.GetFileName(item.Entry.RootDirectory))
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (Song song in Songs)
            {
                if (items.TryGetValue(song.Folder, out SongPackageItem item) && item.RankKey != null)
                {
                    ChartRankCache.Save(item.RankKey, new ChartRankCache.Verdict
                    {
                        ScoreKey = item.ScoreKey, Level = song.Level, Ranked = song.Publication == Publication.Ranked
                    });
                }
            }

            PlayerProfileStore profile = PlayerProfileStore.Local;
            if (profile.ReadHistory(0, 1).TotalCount > 0)
            {
                return;
            }

            profile.SetAppearance("Atlas", "halo");
            var random = new System.Random(7);
            DateTime now = DateTime.UtcNow;
            var runs = new List<CustomMusicScorePlayHistoryRecord>();
            foreach (Song song in Songs)
            {
                // Every sixth song stays unplayed.
                if (song.Index % 6 != 5 && items.TryGetValue(song.Folder, out SongPackageItem item))
                {
                    runs.Add(Run(song, item.ScoreKey, random, now.AddHours(-(1.4 + song.Index * 7.3))));
                }
            }

            foreach (CustomMusicScorePlayHistoryRecord run in runs.OrderBy(run => run.PlayedAtUtc, StringComparer.Ordinal))
            {
                profile.History.Submit(run, null, out _);
            }
        }

        private static CustomMusicScorePlayHistoryRecord Run(Song song, string scoreKey, System.Random random, DateTime playedAt)
        {
            bool allPerfect = song.Index % 7 == 2;
            bool fullCombo = allPerfect || song.Index % 3 == 0;
            int notes = 640 + song.Level * 24;
            int score = allPerfect ? 1000000 : fullCombo ? 991000 + random.Next(8000) : 936000 + random.Next(50000);
            int greats = allPerfect ? 0 : Math.Max(1, (1000000 - score) / 900);
            int goods = fullCombo ? 0 : 4 + random.Next(6);
            int misses = fullCombo ? 0 : 3 + random.Next(9);
            return new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3,
                RecordId = "sample-run-" + song.Index.ToString(CultureInfo.InvariantCulture),
                ScoreKey = scoreKey,
                Ranked = song.Publication == Publication.Ranked,
                Title = song.Title,
                Difficulty = song.Difficulty,
                PlayLevel = song.Level,
                PlayedAtUtc = playedAt.ToString("O", CultureInfo.InvariantCulture),
                EndReason = PlayResultKind.Completed,
                FormulaVersion = RankingRules.FORMULA_VERSION,
                MaximumScore = RankingRules.MAXIMUM_SCORE,
                TotalScore = score,
                Rank = score >= 950000 ? "S" : "A",
                Life = 820,
                TotalComboCount = notes,
                MaxCombo = fullCombo ? notes : notes / 2 + 60,
                PerfectCount = notes - greats - goods - misses,
                GreatCount = greats,
                GoodCount = goods,
                MissCount = misses,
                IsFullCombo = fullCombo,
                IsAllPerfect = allPerfect,
                LivePlayMode = "Free"
            };
        }

        /// <summary>A resource pack of four generated hit sounds, with a name, an icon and a preset; returns the ZIP's path.</summary>
        internal static string WriteResourcePack(string directory)
        {
            RequireWorkspace();
            string path = Path.Combine(directory, "sample-pack.zip");
            var sounds = new (string Id, string Name, int Hertz, int Milliseconds)[]
            {
                ("crisp", "清脆", 1320, 60), ("soft", "柔和", 660, 90), ("bell", "风铃", 1980, 160), ("wood", "木鱼", 420, 50)
            };
            string[] slots = { "audio.tap", "audio.perfect", "audio.great", "audio.good", "audio.critical", "audio.flick" };
            var groups = new JArray();
            foreach (var sound in sounds)
            {
                var files = new JObject();
                foreach (string slot in slots)
                {
                    files[slot] = new JObject { ["file"] = "sound/" + sound.Id + ".wav" };
                }

                groups.Add(new JObject { ["id"] = sound.Id, ["category"] = "audio", ["displayName"] = sound.Name, ["slots"] = files });
            }

            var pack = new JObject
            {
                ["schemaVersion"] = 1, ["packId"] = "sample.prism-hits", ["version"] = "1.2.0", ["groups"] = groups,
                ["extensions"] = new JObject
                {
                    ["opensekai.metadata"] = new JObject { ["displayName"] = "棱镜打击音", ["icon"] = "art/icon.png" },
                    ["opensekai.preset"] = new JObject
                    {
                        ["name"] = "棱镜打击音",
                        ["categories"] = new JObject { ["audio"] = new JObject { ["groups"] = new JArray("crisp") } }
                    }
                }
            };

            Directory.CreateDirectory(directory);
            File.Delete(path);
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Add(zip, "pack.json", Encoding.UTF8.GetBytes(pack.ToString(Formatting.Indented)));
                Add(zip, "art/icon.png", File.ReadAllBytes(Songs.First(song => song.Title == "Velvet Prism").CoverPath));
                foreach (var sound in sounds)
                {
                    Add(zip, "sound/" + sound.Id + ".wav", Click(sound.Hertz, sound.Milliseconds));
                }
            }

            return path;
        }

        private static void Add(ZipArchive zip, string name, byte[] bytes)
        {
            using (Stream stream = zip.CreateEntry(name).Open())
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>A very quiet two-tone pad, so a song has a length and a preview to play.</summary>
        private static byte[] Pad(int seconds, int seed)
        {
            const int RATE = 8000;
            double frequency = 220d * Math.Pow(2d, seed % 12 / 12d);
            return Wave(RATE, RATE * seconds, time =>
                0.012 * (Math.Sin(2d * Math.PI * frequency * time) + Math.Sin(3d * Math.PI * frequency * time)));
        }

        private static byte[] Click(int hertz, int milliseconds)
        {
            const int RATE = 48000;
            double length = milliseconds / 1000d;
            return Wave(RATE, (int)(RATE * length), time => 0.28 * Math.Exp(-5d * time / length) * Math.Sin(2d * Math.PI * hertz * time));
        }

        /// <summary>Mono 16-bit PCM of <paramref name="sample"/>, a function of seconds in -1..1.</summary>
        private static byte[] Wave(int rate, int count, Func<double, double> sample)
        {
            using (var stream = new MemoryStream(44 + count * 2))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + count * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    writer.Write((short)Math.Round(short.MaxValue * sample(i / (double)rate)));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static IReadOnlyList<Song> Build(
            params (string Title, string Singer, string Composer, string Charter, string Difficulty, int Level, int Seconds, Publication From)[] rows)
        {
            return rows.Select((row, index) => new Song(index, row.Title, row.Singer, row.Composer, row.Charter, row.Difficulty,
                row.Level, row.Seconds, row.From)).ToArray();
        }
    }
}
