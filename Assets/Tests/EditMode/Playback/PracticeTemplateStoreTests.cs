using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Shared.Playback;

namespace OpenSekai.Tests.EditMode.Playback
{
    public sealed class PracticeTemplateStoreTests
    {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "opensekai-practice-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "templates.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void FreshStoreReadsTheSameSpeedsAndRestsInOrder()
        {
            var store = new PracticeTemplateStore(_path);
            Assert.That(store.Load(), Is.Empty);
            var template = new PracticeTemplate("低速/热身", new[] { PracticeStep.Play(0.5f),
                PracticeStep.Rest(2), PracticeStep.Play(0.713f), PracticeStep.Rest(2.5) });
            store.Save(template);

            var saved = new PracticeTemplateStore(_path).Load().Single();
            Assert.That(saved.Name, Is.EqualTo("低速/热身"));
            Assert.That(saved.Steps.Select(step => step.Kind), Is.EqualTo(new[] { PracticeStepKind.Play,
                PracticeStepKind.Rest, PracticeStepKind.Play, PracticeStepKind.Rest }));
            Assert.That(saved.Steps[0].Speed, Is.EqualTo(0.5f));
            Assert.That(saved.Steps[1].RestSeconds, Is.EqualTo(2));
            Assert.That(saved.Steps[2].Speed, Is.EqualTo(0.713f));
            Assert.That(saved.Steps[3].RestSeconds, Is.EqualTo(2.5));
        }

        [Test]
        public void ReplacingOneTemplateDoesNotChangeOthersOrPriorSnapshots()
        {
            var store = new PracticeTemplateStore(_path);
            store.Save(new PracticeTemplate("slow", new[] { PracticeStep.Play(0.5f) }));
            store.Save(new PracticeTemplate("fast", new[] { PracticeStep.Play(2) }));
            var before = store.Load();
            store.Save(new PracticeTemplate(" slow ", new[] { PracticeStep.Play(0.625f) }));
            var after = store.Load();
            Assert.That(after.Select(t => t.Name), Is.EqualTo(new[] { "slow", "fast" }));
            Assert.That(before[0].Steps[0].Speed, Is.EqualTo(0.5f));
            Assert.That(after[0].Steps[0].Speed, Is.EqualTo(0.625f));
            Assert.That(after[1].Steps[0].Speed, Is.EqualTo(2));
        }

        [TestCase("unreadable")]
        [TestCase("null")]
        [TestCase("[null]")]
        [TestCase("[{\"name\":\"a\",\"steps\":[]}]")]
        [TestCase("[{\"name\":\"a\",\"steps\":[{\"speed\":1}]}]")]
        [TestCase("[{\"name\":\"a\",\"steps\":[{\"kind\":\"Play\",\"speed\":4000}]}]")]
        [TestCase("[{\"name\":\"a\",\"steps\":[{\"kind\":\"Play\",\"speed\":1}]},{\"name\":\"a\",\"steps\":[{\"kind\":\"Play\",\"speed\":2}]}]")]
        public void SavingDoesNotOverwriteAnUnreadableOrInvalidLibrary(string original)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, original);
            var store = new PracticeTemplateStore(_path);
            Assert.Catch(() => store.Save(new PracticeTemplate("new", new[] { PracticeStep.Play(1) })));
            Assert.That(File.ReadAllText(_path), Is.EqualTo(original));
        }

        [Test]
        public void IndependentWritersPreserveEachOthersTemplates()
        {
            Parallel.For(0, 12, i => new PracticeTemplateStore(_path).Save(
                new PracticeTemplate("set" + i, new[] { PracticeStep.Play(1) })));
            Assert.That(new PracticeTemplateStore(_path).Load().Select(t => t.Name),
                Is.EquivalentTo(Enumerable.Range(0, 12).Select(i => "set" + i)));
        }
    }
}
