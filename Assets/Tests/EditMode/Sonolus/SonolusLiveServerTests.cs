using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.EditMode.Sonolus
{
    /// <summary>
    /// Optional, against a live server: reads its level home page and search form, lists and
    /// searches, then installs the first NextRUSH+ level into a scratch library and checks the
    /// package. Skipped unless <c>OPENSEKAI_SONOLUS_LIVE</c> names a server (e.g. untitledcharts.com).
    /// </summary>
    public sealed class SonolusLiveServerTests
    {
        [UnityTest]
        public IEnumerator BrowsesAndInstallsFromALiveServer() => UniTask.ToCoroutine(async () =>
        {
            string server = Environment.GetEnvironmentVariable("OPENSEKAI_SONOLUS_LIVE");
            if (string.IsNullOrEmpty(server))
            {
                Assert.Ignore("Optional integration test: set OPENSEKAI_SONOLUS_LIVE to a Sonolus server, e.g. untitledcharts.com.");
            }

            var client = new SonolusClient(SonolusServerAddress.Parse(server));
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            CancellationToken token = timeout.Token;

            SonolusServerInfo info = await client.GetServerInfoAsync(token);
            Assert.That(info.HasLevels, Is.True);
            SonolusLevelsInfo levels = await client.GetLevelsInfoAsync(token);
            Assert.That(levels.Sections, Is.Not.Empty, "the level home page has sections");

            SonolusPage<SonolusLevel> first = await client.ListLevelsAsync(null, 0, null, token);
            SonolusPage<SonolusLevel> second = await client.ListLevelsAsync(null, 1, null, token);
            Assert.That(first.Items, Is.Not.Empty);
            Assert.That(second.Items.Select(level => level.Name), Is.Not.EqualTo(first.Items.Select(level => level.Name)), "page 1 differs from page 0");

            SonolusLevel target = first.Items.First(level => SonolusLevelInstaller.IsSupported(level, out _));
            SonolusLevelDetails details = await client.GetLevelAsync(target.Name, token);
            Assert.That(details.Level.Data, Is.Not.Null);

            string root = Path.Combine(Path.GetTempPath(), "sonolus-live-" + Guid.NewGuid().ToString("N"));
            try
            {
                var packages = new SongPackageManager(Path.Combine(root, "library"), Path.Combine(root, "tmp"));
                var installer = new SonolusLevelInstaller(packages, root, Path.Combine(root, "staging"));
                float last = 0f;
                SongPackage package = await installer.InstallAsync(client, details, (progress, _) => last = progress, token);

                Assert.That(last, Is.EqualTo(1f));
                Assert.That(package.Manifest.chartFormat, Is.EqualTo(NextRushChartLoader.Format));
                Assert.That(File.Exists(package.ScorePath) && File.Exists(package.AudioPath), Is.True);
                Assert.That(File.Exists(Path.Combine(package.RootDirectory, SonolusLevelInstaller.SOURCE_FILE_NAME)), Is.True);
                Assert.That(SonolusLevelInstaller.Sha1Hex(File.ReadAllBytes(package.ScorePath)), Is.EqualTo(details.Level.Data.Hash).IgnoreCase,
                    "the chart is kept byte for byte");
                Assert.That(NextRushChartLoader.ReadFile(package.ScorePath), Is.Not.Null);
                Assert.That(installer.StateOf(client.Address, details.Level), Is.EqualTo(SonolusInstallState.Installed));
                Assert.That(installer.FindInstalled(client.Address, target.Name), Is.EqualTo(package.RootDirectory));
                Assert.That(packages.LoadItems().Count, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        });
    }
}
