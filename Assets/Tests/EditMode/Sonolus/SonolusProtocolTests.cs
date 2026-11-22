using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App.Server;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;

namespace OpenSekai.Tests.EditMode.Sonolus
{
    /// <summary>
    /// The Sonolus client's pure parts: reading addresses and share links, resolving resource
    /// URLs against a path prefix, parsing server responses (shaped like UntitledCharts', with
    /// synthetic content), mapping a level to a package manifest, and the saved server list.
    /// Nothing here touches the network.
    /// </summary>
    public sealed class SonolusProtocolTests
    {
        // ---------------------------------------------------------------- addresses

        [TestCase("untitledcharts.com", "https://untitledcharts.com", null)]
        [TestCase("https://UntitledCharts.com/", "https://untitledcharts.com", null)]
        [TestCase("https://untitledcharts.com/sonolus", "https://untitledcharts.com", null)]
        [TestCase("https://example.com/custom/", "https://example.com/custom", null)]
        [TestCase("https://example.com/custom/sonolus/", "https://example.com/custom", null)]
        [TestCase("https://untitledcharts.com/levels/UnCh-abc", "https://untitledcharts.com", "UnCh-abc")]
        [TestCase("sonolus://example.com/custom/levels/a%20b", "https://example.com/custom", "a b")]
        [TestCase("https://open.sonolus.com/example.com/custom/levels/x", "https://example.com/custom", "x")]
        [TestCase("https://example.com:8443/levels/x?ref=share#top", "https://example.com:8443", "x")]
        public void ParsesServerAddressesAndShareLinks(string input, string baseUrl, string level)
        {
            Assert.That(SonolusServerAddress.TryParse(input, out SonolusServerAddress address, out string name, out string error), Is.True, error);
            Assert.That(address.BaseUrl, Is.EqualTo(baseUrl));
            Assert.That(name, Is.EqualTo(level));
        }

        [TestCase("")]
        [TestCase("http://example.com")]
        [TestCase("ftp://example.com")]
        [TestCase("https://user@example.com")]
        [TestCase("https://exa mple.com")]
        [TestCase("https://example.com/../x")]
        [TestCase("https://example.com:99999")]
        public void RefusesUnsafeOrBrokenAddresses(string input)
        {
            Assert.That(SonolusServerAddress.TryParse(input, out _, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void EndpointsHangOffThePrefixAndEncodeTheQuery()
        {
            SonolusServerAddress address = SonolusServerAddress.Parse("https://example.com/custom");
            string url = address.Endpoint("/levels/list", new[]
            {
                new KeyValuePair<string, string>("type", "quick"),
                new KeyValuePair<string, string>("keywords", "春 & 夏"),
                new KeyValuePair<string, string>("skipped", null)
            });
            Assert.That(url, Is.EqualTo("https://example.com/custom/sonolus/levels/list?type=quick&keywords=%E6%98%A5%20%26%20%E5%A4%8F"));
        }

        [Test]
        public void ResolvesResourceUrlsAgainstTheServerPrefix()
        {
            SonolusServerAddress address = SonolusServerAddress.Parse("https://example.com/custom");
            Assert.That(address.Resolve("/sonolus/repository/abc"), Is.EqualTo("https://example.com/custom/sonolus/repository/abc"));
            Assert.That(address.Resolve("https://cdn.example.net/a/b"), Is.EqualTo("https://cdn.example.net/a/b"));
            Assert.That(address.Resolve("//cdn.example.net/a"), Is.EqualTo("https://cdn.example.net/a"));
            Assert.That(address.Resolve("http://cdn.example.net/a"), Is.Null, "plain HTTP resources are refused");
            Assert.That(address.Resolve("file:///etc/passwd"), Is.Null);
            Assert.That(address.Resolve(null), Is.Null);
        }

        // ---------------------------------------------------------------- responses

        private const string LEVEL_JSON = @"{
            ""name"": ""UnCh-0001"", ""source"": ""https://example.com"", ""version"": 2, ""rating"": 28,
            ""title"": ""Sample Song"", ""artists"": ""Sample Artist"", ""author"": ""Charter#1"",
            ""tags"": [ { ""icon"": ""trophy"" }, { ""title"": ""3d ago"", ""icon"": ""clock"" }, { ""title"": ""Expert"" }, { ""bogus"": 1 } ],
            ""engine"": { ""name"": ""NextRUSH_P"", ""version"": 13, ""title"": ""NextRUSH+"" },
            ""cover"": { ""hash"": ""aa"", ""url"": ""https://cdn.example.com/c"" },
            ""bgm"": { ""hash"": ""bb"", ""url"": ""/sonolus/repository/bb"" },
            ""preview"": { ""url"": ""/p"" },
            ""data"": { ""hash"": ""dd"", ""url"": ""https://cdn.example.com/d"" },
            ""unknownField"": { ""nested"": true }
        }";

        [Test]
        public void ParsesALevelLeniently()
        {
            SonolusLevel level = SonolusLevel.From(JToken.Parse(LEVEL_JSON));
            Assert.That(level.Name, Is.EqualTo("UnCh-0001"));
            Assert.That(level.RatingText, Is.EqualTo("28"));
            Assert.That(level.EngineName, Is.EqualTo("NextRUSH_P"));
            Assert.That(level.Tags.Select(tag => tag.Title ?? "<" + tag.Icon + ">"), Is.EqualTo(new[] { "<trophy>", "3d ago", "Expert" }));
            Assert.That(level.Bgm.Url, Is.EqualTo("/sonolus/repository/bb"));
            Assert.That(level.Preview.Hash, Is.Null);
            Assert.That(SonolusLevel.From(JToken.Parse("{\"title\":\"no name\"}")), Is.Null);
            Assert.That(SonolusLevel.From(JToken.Parse("[]")), Is.Null);
        }

        [Test]
        public void ParsesSectionsAndTheAdvancedSearchForm()
        {
            string json = @"{
              ""sections"": [
                { ""title"": ""#NEWEST"", ""icon"": ""level"", ""itemType"": ""level"", ""items"": [ " + LEVEL_JSON + @" ] },
                { ""title"": ""Skins"", ""itemType"": ""skin"", ""items"": [ { ""name"": ""s"" } ] },
                { ""title"": ""Empty"", ""itemType"": ""level"", ""items"": [] }
              ],
              ""searches"": [ { ""type"": ""advanced"", ""title"": ""Advanced Search"", ""options"": [
                { ""query"": ""keywords"", ""name"": ""#KEYWORDS"", ""type"": ""text"", ""def"": """", ""limit"": 100 },
                { ""query"": ""staff_pick"", ""name"": ""Staff Pick"", ""type"": ""select"", ""def"": ""default"",
                  ""values"": [ { ""name"": ""default"", ""title"": ""#DEFAULT"" }, { ""name"": ""true"", ""title"": ""Only"" } ] },
                { ""query"": ""min_rating"", ""name"": ""Minimum Rating"", ""type"": ""slider"", ""def"": -999, ""min"": -999, ""max"": 999, ""step"": 1 },
                { ""query"": ""genres"", ""name"": ""Genres"", ""type"": ""multi"", ""def"": [ true, false ],
                  ""values"": [ { ""name"": ""pop"", ""title"": ""Pop"" }, { ""name"": ""rock"", ""title"": ""Rock"" } ] },
                { ""query"": ""nsfw"", ""name"": ""NSFW"", ""type"": ""toggle"", ""def"": false },
                { ""query"": ""who"", ""name"": ""Who"", ""type"": ""serverItem"" }
              ] } ]
            }";
            SonolusLevelsInfo info = SonolusLevelsInfo.From(JToken.Parse(json));
            Assert.That(info.Sections.Count, Is.EqualTo(1), "only non-empty level sections are kept");
            Assert.That(SonolusText.Localize(info.Sections[0].Title), Is.EqualTo("最新"));

            SonolusSearch search = info.Searches.Single();
            Assert.That(search.Type, Is.EqualTo("advanced"));
            Assert.That(search.Options.Select(option => option.IsSupported), Is.EqualTo(new[] { true, true, true, true, true, false }));
            SonolusSearchOption slider = search.Options[2];
            Assert.That(slider.Default, Is.EqualTo("-999"));
            Assert.That(slider.Min, Is.EqualTo(-999d));
            Assert.That(search.Options[1].Values.Select(value => value.Name), Is.EqualTo(new[] { "default", "true" }));
            Assert.That(search.Options[3].DefaultFlags, Is.EqualTo(new[] { true, false }));
            Assert.That(search.Options[4].Default, Is.EqualTo("0"));
        }

        [Test]
        public void ParsesDetailsCommentsAndRecords()
        {
            SonolusLevelDetails details = SonolusLevelDetails.From(JToken.Parse(@"{
                ""item"": " + LEVEL_JSON + @", ""description"": ""Line one\nLine two"", ""hasCommunity"": true,
                ""leaderboards"": [ { ""name"": ""arcade"", ""title"": ""Arcade Score"" }, { ""title"": ""nameless"" } ], ""sections"": [] }"));
            Assert.That(details.Description, Is.EqualTo("Line one\nLine two"));
            Assert.That(details.HasCommunity, Is.True);
            Assert.That(details.Leaderboards.Select(board => board.Name), Is.EqualTo(new[] { "arcade" }));
            Assert.That(SonolusLevelDetails.From(JToken.Parse("{\"item\":null}")), Is.Null);

            var comments = SonolusComment.ListFrom(JToken.Parse(@"[ { ""name"": ""1"", ""author"": ""A#1"", ""time"": 1771199972192, ""content"": ""nice"" } ]"));
            Assert.That(comments[0].Time.Value.ToUnixTimeMilliseconds(), Is.EqualTo(1771199972192L));
            var records = SonolusRecord.ListFrom(JToken.Parse(@"[ { ""rank"": ""#1"", ""player"": ""P#2"", ""value"": ""AP | 1000000"" } ]"));
            Assert.That(records[0].Value, Is.EqualTo("AP | 1000000"));
        }

        [Test]
        public void PagesByNumberOrByCursor()
        {
            var numbered = new SonolusPage<int>(new[] { 1 }, 3, null);
            Assert.That(numbered.HasMoreAfter(1), Is.True);
            Assert.That(numbered.HasMoreAfter(2), Is.False);
            var cursor = new SonolusPage<int>(new[] { 1 }, -1, "next");
            Assert.That(cursor.UsesCursor, Is.True);
            Assert.That(cursor.HasMoreAfter(99), Is.True);
            Assert.That(new SonolusPage<int>(new int[0], -1, null).HasMoreAfter(0), Is.False);
        }

        [Test]
        public void LocalizesTextConstants()
        {
            Assert.That(SonolusText.Localize("#RANDOM"), Is.EqualTo("随机"));
            Assert.That(SonolusText.Localize("#SOME_NEW_THING"), Is.EqualTo("Some New Thing"));
            Assert.That(SonolusText.Localize("Plain"), Is.EqualTo("Plain"));
            Assert.That(SonolusText.Localize(null), Is.EqualTo(string.Empty));
        }

        // ---------------------------------------------------------------- installer mapping

        [Test]
        public void OnlyNextRushLevelsAreSupported()
        {
            SonolusLevel level = SonolusLevel.From(JToken.Parse(LEVEL_JSON));
            Assert.That(SonolusLevelInstaller.IsSupported(level, out _), Is.True);
            JObject other = JObject.Parse(LEVEL_JSON);
            other["engine"]["name"] = "next-rush";
            other["engine"]["title"] = "Next RUSH";
            Assert.That(SonolusLevelInstaller.IsSupported(SonolusLevel.From(other), out _), Is.True);
            other["engine"]["name"] = "pjsekai";
            other["engine"]["title"] = "PJSekai";
            Assert.That(SonolusLevelInstaller.IsSupported(SonolusLevel.From(other), out string reason), Is.False);
            Assert.That(reason, Does.Contain("PJSekai"));
        }

        [Test]
        public void MapsALevelToANativePackageManifest()
        {
            SonolusLevelDetails details = SonolusLevelDetails.From(JToken.Parse(@"{ ""item"": " + LEVEL_JSON + @", ""description"": ""desc"" }"));
            var manifest = JObject.Parse(SonolusLevelInstaller.BuildManifest(details, ".mp3", ".png"));
            Assert.That((string)manifest["chartFormat"], Is.EqualTo("next-rush"));
            Assert.That((string)manifest["title"], Is.EqualTo("Sample Song"));
            Assert.That((string)manifest["composer"], Is.EqualTo("Sample Artist"));
            Assert.That((string)manifest["userName"], Is.EqualTo("Charter#1"));
            Assert.That((int)manifest["playLevel"], Is.EqualTo(28));
            Assert.That((string)manifest["musicDifficultyType"], Is.EqualTo("expert"), "the Expert tag names the difficulty");
            Assert.That((string)manifest["scoreFileName"], Is.EqualTo(SonolusLevelInstaller.DATA_FILE_NAME));
            Assert.That((string)manifest["audioFileName"], Is.EqualTo("audio.mp3"));
            Assert.That((string)manifest["jacketFileName"], Is.EqualTo("jacket.png"));

            var noCover = JObject.Parse(SonolusLevelInstaller.BuildManifest(details, ".ogg", null));
            Assert.That((string)noCover["jacketFileName"], Is.Empty);
        }

        [Test]
        public void SniffsMediaByContent()
        {
            Assert.That(SonolusLevelInstaller.AudioExtension(Pad(Encoding.ASCII.GetBytes("ID3"))), Is.EqualTo(".mp3"));
            Assert.That(SonolusLevelInstaller.AudioExtension(Pad(new byte[] { 0xFF, 0xFB })), Is.EqualTo(".mp3"));
            Assert.That(SonolusLevelInstaller.AudioExtension(Pad(Encoding.ASCII.GetBytes("OggS"))), Is.EqualTo(".ogg"));
            Assert.That(SonolusLevelInstaller.AudioExtension(Encoding.ASCII.GetBytes("RIFF0000WAVEfmt ")), Is.EqualTo(".wav"));
            Assert.That(SonolusLevelInstaller.AudioExtension(Pad(Encoding.ASCII.GetBytes("<htm"))), Is.Null);
            Assert.That(SonolusLevelInstaller.ImageExtension(Pad(new byte[] { 0x89, 0x50, 0x4E, 0x47 })), Is.EqualTo(".png"));
            Assert.That(SonolusLevelInstaller.ImageExtension(Pad(new byte[] { 0xFF, 0xD8, 0xFF })), Is.EqualTo(".jpg"));
            Assert.That(SonolusLevelInstaller.ImageExtension(Pad(Encoding.ASCII.GetBytes("GIF8"))), Is.Null);
            Assert.That(SonolusLevelInstaller.Sha1Hex(Encoding.ASCII.GetBytes("abc")), Is.EqualTo("a9993e364706816aba3e25717850c26c9cd0d89d"));
        }

        [Test]
        public void ALevelNeverDownloadedIsMissing()
        {
            string root = Path.Combine(Path.GetTempPath(), "sonolus-index-" + Guid.NewGuid().ToString("N"));
            try
            {
                var installer = new SonolusLevelInstaller(
                    new OpenSekai.Infrastructure.Packages.SongPackageManager(Path.Combine(root, "library"), Path.Combine(root, "tmp")),
                    root, Path.Combine(root, "staging"));
                SonolusServerAddress server = SonolusServerAddress.Parse("example.com");
                SonolusLevel level = SonolusLevel.From(JToken.Parse(LEVEL_JSON));
                Assert.That(installer.StateOf(server, level), Is.EqualTo(SonolusInstallState.Missing));
                Assert.That(installer.FindInstalled(server, level.Name), Is.Null);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        // ---------------------------------------------------------------- saved servers

        [Test]
        public void ServerListStartsEmptyAndKeepsWhatThePlayerAdds()
        {
            string root = Path.Combine(Path.GetTempPath(), "servers-" + Guid.NewGuid().ToString("N"));
            try
            {
                var registry = new ServerRegistry(root);
                Assert.That(registry.Servers, Is.Empty, "the game ships with no servers");

                ServerEntry added = registry.TryAdd(ServerKind.Sonolus, "", "sonolus://example.com/custom/levels/x", out string error);
                Assert.That(added, Is.Not.Null, error);
                Assert.That(added.Address, Is.EqualTo("https://example.com/custom"));
                Assert.That(added.Name, Is.EqualTo("example.com"));
                Assert.That(registry.TryAdd(ServerKind.Sonolus, "again", "https://example.com/custom/", out error), Is.Null);
                Assert.That(error, Does.Contain("已经添加"));
                Assert.That(registry.TryAdd(ServerKind.WebView, "site", "http://example.com", out error), Is.Null, "websites must be HTTPS");
                ServerEntry site = registry.TryAdd(ServerKind.WebView, "site", "example.org/charts", out error);
                Assert.That(site, Is.Not.Null, error);
                Assert.That(site.Address, Is.EqualTo("https://example.org/charts"));

                var reloaded = new ServerRegistry(root);
                Assert.That(reloaded.Servers.Select(server => server.Address),
                    Is.EqualTo(new[] { "https://example.com/custom", "https://example.org/charts" }));
                Assert.That(reloaded.TryRename(reloaded.Find(site.Id), "renamed", out error), Is.True, error);
                Assert.That(new ServerRegistry(root).Find(site.Id).Name, Is.EqualTo("renamed"));
                Assert.That(reloaded.TryRemove(reloaded.Find(added.Id), out error), Is.True, error);
                Assert.That(new ServerRegistry(root).Servers.Single().Kind, Is.EqualTo(ServerKind.WebView));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void AdvancedFiltersAreSavedPerServerAndCanBeCleared()
        {
            string root = Path.Combine(Path.GetTempPath(), "servers-" + Guid.NewGuid().ToString("N"));
            try
            {
                var registry = new ServerRegistry(root);
                ServerEntry first = registry.TryAdd(ServerKind.Sonolus, "first", "example.com/first", out string error);
                ServerEntry second = registry.TryAdd(ServerKind.Sonolus, "second", "example.com/second", out error);
                var firstQuery = new[] { new KeyValuePair<string, string>("genre", "a,b") };
                var secondQuery = new[] { new KeyValuePair<string, string>("genre", string.Empty) };
                Assert.That(registry.TrySaveAdvancedSearch(first, "advanced", firstQuery, out error), Is.True, error);
                Assert.That(registry.TrySaveAdvancedSearch(second, "advanced", secondQuery, out error), Is.True, error);
                Assert.That(registry.TryRename(first, "renamed", out error), Is.True, error);

                var reopened = new ServerRegistry(root);
                ServerEntry firstAgain = reopened.Find(first.Id);
                Assert.That((string)firstAgain.AdvancedSearch["type"], Is.EqualTo("advanced"));
                Assert.That((string)firstAgain.AdvancedSearch["values"]["genre"], Is.EqualTo("a,b"));
                Assert.That((string)reopened.Find(second.Id).AdvancedSearch["values"]["genre"], Is.Empty);
                Assert.That(reopened.TrySaveAdvancedSearch(firstAgain, "advanced", null, out error), Is.True, error);

                var afterReset = new ServerRegistry(root);
                Assert.That(afterReset.Find(first.Id).AdvancedSearch, Is.Null);
                Assert.That(afterReset.Find(second.Id).AdvancedSearch, Is.Not.Null);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void FailedServerSaveLeavesTheListAndItsLastSavedContentsIntact()
        {
            string root = Path.Combine(Path.GetTempPath(), "servers-" + Guid.NewGuid().ToString("N"));
            try
            {
                var registry = new ServerRegistry(root);
                ServerEntry existing = registry.TryAdd(ServerKind.Sonolus, "original", "example.com", out string error);
                Assert.That(existing, Is.Not.Null, error);
                string path = Path.Combine(root, "servers.json");
                string savedFile = Path.Combine(root, "previous.json");
                string original = File.ReadAllText(path);
                File.Move(path, savedFile);
                Directory.CreateDirectory(path); // A directory cannot be replaced by the new configuration file.

                int changes = 0;
                registry.Changed += () => changes++;
                Assert.That(registry.TryAdd(ServerKind.WebView, "other", "example.org", out error), Is.Null);
                Assert.That(error, Does.Contain("无法保存服务器列表"));
                Assert.That(registry.TryRename(existing, "new name", out error), Is.False);
                Assert.That(error, Does.Contain("无法保存服务器列表"));
                Assert.That(registry.TryRemove(existing, out error), Is.False);
                Assert.That(error, Does.Contain("无法保存服务器列表"));

                Assert.That(changes, Is.Zero);
                Assert.That(registry.Servers.Single(), Is.SameAs(existing));
                Assert.That(existing.Name, Is.EqualTo("original"));
                Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
                Directory.Delete(path);
                File.Move(savedFile, path);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                Assert.That(new ServerRegistry(root).Find(existing.Id).Name, Is.EqualTo("original"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void OldBuiltInEntriesInASavedListAreDropped()
        {
            string root = Path.Combine(Path.GetTempPath(), "servers-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "servers.json"), @"{ ""version"": 1, ""hidden"": [ ""builtin-opensekai"" ],
                    ""servers"": [ { ""id"": ""user-1"", ""kind"": ""sonolus"", ""name"": ""Mine"", ""address"": ""https://example.com"" } ] }");
                Assert.That(new ServerRegistry(root).Servers.Select(server => server.Id), Is.EqualTo(new[] { "user-1" }));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static byte[] Pad(byte[] prefix)
        {
            var bytes = new byte[16];
            Array.Copy(prefix, bytes, prefix.Length);
            return bytes;
        }
    }
}
