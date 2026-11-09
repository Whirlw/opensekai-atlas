using System.IO;
using NUnit.Framework;
using OpenSekai.Shared.Packages;

namespace OpenSekai.Tests.EditMode.Packages
{
    /// <summary>
    /// Folder names end up as paths on the user's device, so the sanitising rules are pinned here,
    /// including the fallback when a package has no usable title.
    /// </summary>
    public class SongPackageStorageTests
    {
        private static SongPackageManifest Manifest(string title, string id) => new SongPackageManifest
        {
            title = title, id = id
        };

        [Test]
        public void FolderNameIsTitleUnderscoreId()
        {
            Assert.That(SongPackageStorage.CreateFolderName(Manifest("Test", "codexbaseline20261101tap")),
                Is.EqualTo("Test_codexbaseline20261101tap"));
        }

        [Test]
        public void SpacesBecomeUnderscores()
        {
            Assert.That(SongPackageStorage.CreateFolderName(Manifest("Baseline Tap", "abc")),
                Is.EqualTo("Baseline_Tap_abc"));
        }

        [Test]
        public void RepeatedSpacesCollapse()
        {
            string folder = SongPackageStorage.CreateFolderName(Manifest("A    B", "abc"));

            Assert.That(folder, Is.EqualTo("A_B_abc"));
        }

        [Test]
        public void TrimmedTitleLosesLeadingAndTrailingSpaces()
        {
            Assert.That(SongPackageStorage.CreateFolderName(Manifest("  Test  ", "abc")), Is.EqualTo("Test_abc"));
        }

        [Test]
        public void BlankTitleFallsBackToUntitled()
        {
            Assert.That(SongPackageStorage.CreateFolderName(Manifest("   ", "abc")), Is.EqualTo("Untitled_abc"));
        }

        [Test]
        public void NullManifestYieldsAShortId()
        {
            string folder = SongPackageStorage.CreateFolderName(null);

            Assert.That(folder, Has.Length.EqualTo(12));
            Assert.That(folder, Does.Match("^[0-9a-z]{12}$"));
        }

        [Test]
        public void ShortIdsAreTwelveLowercaseCharacters()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.That(SongPackageStorage.GenerateShortId(), Does.Match("^[0-9a-z]{12}$"));
            }
        }

        [Test]
        public void ShortIdsDoNotRepeat()
        {
            string first = SongPackageStorage.GenerateShortId();
            string second = SongPackageStorage.GenerateShortId();

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void PackageResolvesItsFilesFromTheManifest()
        {
            string root = Path.Combine(Path.GetTempPath(), "opensekai-tests", "pkg");
            var manifest = new SongPackageManifest
            {
                title = "T", id = "abc", scoreFileName = "score.json",
                audioFileName = "audio.ogg", jacketFileName = "jacket.png"
            };

            var package = new SongPackage(root, manifest);

            Assert.That(package.RootDirectory, Is.EqualTo(root));
            Assert.That(package.ScorePath, Is.EqualTo(Path.Combine(root, "score.json")));
            Assert.That(package.AudioPath, Is.EqualTo(Path.Combine(root, "audio.ogg")));
            Assert.That(package.JacketPath, Is.EqualTo(Path.Combine(root, "jacket.png")));
            Assert.That(package.AudioCueName, Is.EqualTo("custom_abc"));
        }

        [Test]
        public void PackageMusicIdIsStableForTheSameId()
        {
            var left = new SongPackage("a", Manifest("T", "sameid"));
            var right = new SongPackage("b", Manifest("T", "sameid"));

            Assert.That(left.MusicId, Is.EqualTo(right.MusicId));
        }

        [Test]
        public void MusicDurationUsesTheExplicitLengthFirst()
        {
            var manifest = Manifest("T", "abc");
            manifest.secForMusicScoreMaker = 90;
            var package = new SongPackage("a", manifest);

            Assert.That(package.MusicDurationSec, Is.EqualTo(90));
        }

        [Test]
        public void MusicDurationFallsBackToAHundredTwentySeconds()
        {
            Assert.That(new SongPackage("a", Manifest("T", "abc")).MusicDurationSec, Is.EqualTo(120));
        }
    }
}
