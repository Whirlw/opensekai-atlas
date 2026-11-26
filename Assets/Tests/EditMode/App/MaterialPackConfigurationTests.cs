using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using OpenSekai.Shared.Settings;
using Sekai;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>
    /// The resource pack page's contract on the store: pack names, icons and presets read from
    /// the manifest's extensions, the built-in sets migrated from the old settings, the two modes
    /// (预设 and 混搭) and what each keeps, one-commit saves, uninstall impact, and the demo chart.
    /// </summary>
    public sealed class MaterialPackConfigurationTests
    {
        private const string SAMPLE = "Tools/fixtures/material-audio.zip";
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "material-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // ------------------------------------------------------------------ manifest extensions

        [Test]
        public async Task PackNameIconAndPresetAreReadAndKeptInTheIndex()
        {
            var store = Store();
            var pack = await store.ImportAsync(PresetPack());
            Assert.That(pack.DisplayName, Is.EqualTo("墨色打歌"));
            Assert.That(pack.Icon.File, Is.EqualTo("art/icon.png"));
            Assert.That(pack.Icon.Hash, Has.Length.EqualTo(64));
            Assert.That(pack.Icon.Width, Is.EqualTo(4));
            Assert.That(pack.Preset.Name, Is.EqualTo("清脆打击"));
            Assert.That(pack.Preset.Categories["audio"].Groups, Is.EqualTo(new[] { "clicks" }));
            Assert.That(pack.Preset.Categories["audio"].BuiltinSet, Is.EqualTo(2));
            Assert.That(pack.MetadataVersion, Is.EqualTo(InstalledMaterialPack.CURRENT_METADATA_VERSION));

            var reread = Store().List().Installed.Single();
            Assert.That(reread.DisplayName, Is.EqualTo("墨色打歌"));
            Assert.That(reread.Icon.Hash, Is.EqualTo(pack.Icon.Hash));
            Assert.That(reread.Preset.Categories.Keys, Is.EquivalentTo(new[] { "audio", "notes" }));
            Assert.That(store.List().Audio.Groups, Is.Empty, "导入本身只安装");
        }

        [Test]
        public async Task APackWithoutExtensionsStillImportsWithFallbackMetadata()
        {
            var pack = await Store().ImportAsync(SAMPLE);
            Assert.That(pack.DisplayName, Is.Null);
            Assert.That(pack.Icon, Is.Null);
            Assert.That(pack.Preset, Is.Null);
            Assert.That(pack.MetadataVersion, Is.EqualTo(InstalledMaterialPack.CURRENT_METADATA_VERSION));
        }

        [TestCase("foreignGroup")]
        [TestCase("wrongCategory")]
        [TestCase("builtinOnJudgment")]
        [TestCase("builtinRange")]
        [TestCase("unknownCategory")]
        [TestCase("missingIcon")]
        [TestCase("metadataField")]
        [TestCase("presetName")]
        public async Task AnInvalidPresetOrMetadataIsAManifestError(string fault)
        {
            var store = Store();
            try
            {
                await store.ImportAsync(PresetPack(fault));
                Assert.Fail("应拒绝 " + fault);
            }
            catch (InvalidDataException) { }

            Assert.That(store.List().Installed, Is.Empty);
        }

        [Test]
        public async Task PresetTapStaysBuiltinWhenTheMainGroupChanges()
        {
            var store = Store();
            var pack = await store.ImportAsync("Tools/fixtures/material-audio-preset.zip");
            store.ApplyPreset(pack.PackId, pack.Digest, store.List().Revision);
            foreach (string main in new[] { "sounds-2", "sounds-1" })
            {
                store.SaveBundleGroup(pack.PackId, pack.Digest, "audio", main, store.List().Revision);
                store = Store();
                var preview = store.Describe("audio", store.ResolvePreset(pack.PackId, pack.Digest).Audio);
                var tap = preview.Single(s => s.Slot == "audio.tap");
                Assert.That(tap.Source, Is.Null);
                Assert.That(tap.Reason, Is.EqualTo("override"));
                Assert.That(preview.Single(s => s.Slot == "audio.perfect").Source, Is.EqualTo(pack.GroupReference(main)));
                Assert.That(preview.Single(s => s.Slot == "audio.countdown").Source, Is.EqualTo(pack.GroupReference("sounds-common")));
                var live = store.Capture();
                try { Assert.That(live.Audio.Select(b => (b.Slot, b.Source, b.Reason)), Is.EqualTo(preview.Select(b => (b.Slot, b.Source, b.Reason)))); }
                finally { store.Release(); }
            }
        }

        [Test]
        public async Task PinnedGroupFollowsItsOwnRevisionAndCopiesIntoMix()
        {
            var store = Store();
            var original = await store.ImportAsync(PresetPack(editCategories: c => c["audio"]["overrides"] = new JObject()));
            var revised = await store.ImportAsync(PresetPack(editCategories: c =>
            {
                c["audio"]["groups"] = new JArray("holds");
                c["audio"]["overrides"] = new JObject { ["audio.tap"] = null, ["audio.perfect"] = "clicks" };
            }));
            var empty = store.ResolvePreset(original.PackId, original.Digest);
            Assert.That(empty.Audio.Overrides, Is.Empty);
            var pinned = store.ResolvePreset(revised.PackId, revised.Digest);
            Assert.That(store.Resolve(pinned.Audio).Single(b => b.Slot == "audio.perfect").Source, Is.EqualTo(revised.GroupReference("clicks")));
            store.SaveConfiguration(pinned, store.List().Revision);
            store = Store();
            var mix = store.List().MixConfiguration();
            Assert.That(mix.SameChoicesAs(pinned), Is.True);
            mix.Audio.Overrides.Remove("audio.tap");
            store.SaveConfiguration(mix, store.List().Revision);
            Assert.That(store.ResolvePreset(revised.PackId, revised.Digest).Audio.Overrides.ContainsKey("audio.tap"), Is.True);
            store.Uninstall(original.PackId, original.Digest, store.List().Revision);
            Assert.That(Store().List().MixConfiguration().Audio.Overrides["audio.perfect"], Is.EqualTo(revised.GroupReference("clicks")));
            store.Uninstall(revised.PackId, revised.Digest, store.List().Revision);
            Assert.That(Store().List().MixConfiguration().Audio.Overrides, Is.Empty);
        }

        [TestCase("audio", "{'audio.typo':null}", "['audio.typo']")]
        [TestCase("audio", "{'audio.tap':'keys'}", "['audio.tap']")]
        [TestCase("audio", "{'audio.tap':'holds'}", "['audio.tap']")]
        [TestCase("audio", "{'audio.tap':'unknown'}", "['audio.tap']")]
        [TestCase("audio", "{'audio.tap':false}", "['audio.tap']")]
        [TestCase("audio", "null", "")]
        [TestCase("notes", "{}", "")]
        public async Task InvalidOverridesRejectOnlyTheNewRevision(string category, string json, string suffix)
        {
            var store = Store();
            var valid = await store.ImportAsync(PresetPack());
            store.ApplyPreset(valid.PackId, valid.Digest, store.List().Revision);
            long revision = store.List().Revision;
            try
            {
                await store.ImportAsync(PresetPack(editCategories: c => c[category]["overrides"] = JToken.Parse(json)));
                Assert.Fail("应拒绝无效的逐槽指定");
            }
            catch (InvalidDataException error)
            {
                Assert.That(error.Message, Does.Contain("extensions['opensekai.preset'].categories." + category + ".overrides" + suffix));
            }
            var saved = Store().List();
            Assert.That(saved.Revision, Is.EqualTo(revision));
            Assert.That(saved.Installed.Single().Digest, Is.EqualTo(valid.Digest));
            Assert.That(saved.ActiveConfiguration().Audio.Groups, Is.EqualTo(new[] { valid.GroupReference("clicks") }));
        }

        // ------------------------------------------------------------------ built-in sets

        [Test]
        public async Task TheOldSettingsBecomeTheBuiltinSetsOnceAndExternalChoicesStay()
        {
            var store = Store(MaterialBuiltinSelection.FromLegacy(1, 0, 1));
            Assert.That(Sets(store.List().Builtin), Is.EqualTo((2, 1, 1)));
            Assert.That(Sets(Store().List().Builtin), Is.EqualTo((1, 1, 1)), "新档沿用游戏原默认值");

            // An index written before the page existed: external groups and overrides, no sets.
            var pack = await store.ImportAsync(SAMPLE);
            var selection = new MaterialAudioSelection { Groups = { pack.GroupReference("holds"), pack.GroupReference("clicks") } };
            selection.Overrides["audio.tap"] = null;
            store.SaveSelection(selection, store.List().Revision);
            StripFromIndex("Builtin");

            store = Store(MaterialBuiltinSelection.FromLegacy(0, 1, 0));
            var state = store.List();
            Assert.That(Sets(state.Builtin), Is.EqualTo((1, 2, 1)));
            Assert.That(state.Audio.Groups, Is.EqualTo(selection.Groups));
            Assert.That(state.Audio.Overrides.ContainsKey("audio.tap"), Is.True);
            Assert.That(Sets(Store(MaterialBuiltinSelection.FromLegacy(0, 1, 0)).List().Builtin), Is.EqualTo((1, 2, 1)), "迁移幂等");

            // Once any write happened the sets are the store's own; the old settings no longer matter.
            Assert.That(state.Mode, Is.EqualTo(MaterialPackModes.MIX), "已启用外部组的旧档进入混搭，下一局不变");
            var configuration = state.MixConfiguration();
            configuration.Notes.Groups.Clear();
            store.SaveConfiguration(configuration, state.Revision);
            Assert.That(Sets(Store(MaterialBuiltinSelection.FromLegacy(1, 1, 1)).List().Builtin), Is.EqualTo((1, 2, 1)));
        }

        [Test]
        public void TheLiveSnapshotTakesTheSetsWithoutTouchingTheStoredSettings()
        {
            var settings = new LiveSettingData { NoteSkinIndex = 0, NoteEffect = 0, NoteSeIndex = 0 };
            var snapshot = new PlaybackSettingsSnapshot(settings);
            var merged = snapshot.WithBuiltinSets(1, 1, 0).ToLegacyData();
            Assert.That(merged.NoteSkinIndex, Is.EqualTo(1));
            Assert.That(merged.NoteEffect, Is.EqualTo(1));
            Assert.That(merged.NoteSeIndex, Is.Zero);
            Assert.That(snapshot.ToLegacyData().NoteSkinIndex, Is.Zero);
            Assert.That(settings.NoteSkinIndex, Is.Zero);
        }

        [Test]
        public void AnIndexWithoutAModeStartsInPresetUnlessItAlreadyMixes()
        {
            var fresh = Store().List();
            Assert.That(fresh.Mode, Is.EqualTo(MaterialPackModes.PRESET), "新档默认是预设");
            Assert.That(fresh.Preset.IsBuiltin && fresh.Preset.BuiltinSet == 1, Is.True);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);

            var second = Store(MaterialBuiltinSelection.FromLegacy(1, 1, 1)).List();
            Assert.That(second.Mode, Is.EqualTo(MaterialPackModes.PRESET));
            Assert.That(second.Preset.BuiltinSet, Is.EqualTo(2), "旧设置三项都是第二套：内置预设第二套");
            Assert.That(Sets(second.ActiveConfiguration().Builtin), Is.EqualTo((2, 2, 1)));
            if (Directory.Exists(_root)) Directory.Delete(_root, true);

            var split = Store(MaterialBuiltinSelection.FromLegacy(1, 0, 1)).List();
            Assert.That(split.Mode, Is.EqualTo(MaterialPackModes.MIX), "旧设置各项不同：混搭保留原样");
            Assert.That(Sets(split.ActiveConfiguration().Builtin), Is.EqualTo((2, 1, 1)));
        }

        // ------------------------------------------------------------------ saving

        [Test]
        public async Task AConfigurationSavesInOneCommitOrNotAtAll()
        {
            var store = Store();
            var pack = await store.ImportAsync(SAMPLE);
            var state = store.List();
            Assert.That(state.IsPresetMode, Is.True);
            var configuration = state.MixConfiguration();
            configuration.Audio.Groups.Add(pack.GroupReference("clicks"));
            configuration.Builtin.Set("notes", 2);
            configuration.Notes.Groups.Add(pack.GroupReference("clicks"));
            Assert.Throws<InvalidDataException>(() => store.SaveConfiguration(configuration, state.Revision), "音效组不能作为音符组");
            var unchanged = store.List();
            Assert.That(unchanged.Revision, Is.EqualTo(state.Revision));
            Assert.That(unchanged.Audio.Groups, Is.Empty);
            Assert.That(unchanged.Builtin.Notes, Is.EqualTo(1));
            Assert.That(unchanged.IsPresetMode, Is.True);

            configuration.Notes.Groups.Clear();
            long revision = store.SaveConfiguration(configuration, state.Revision);
            Assert.That(revision, Is.EqualTo(state.Revision + 1));
            var saved = Store().List();
            Assert.That(saved.Audio.Groups, Is.EqualTo(new[] { pack.GroupReference("clicks") }));
            Assert.That(saved.Builtin.Notes, Is.EqualTo(2));
            Assert.That(saved.Mode, Is.EqualTo(MaterialPackModes.MIX), "保存混搭即使用混搭");
            Assert.Throws<MaterialPackConflictException>(() => store.SaveConfiguration(configuration, state.Revision));
        }

        // ------------------------------------------------------------------ presets and modes

        [Test]
        public async Task APresetAndTheMixEachKeepTheirChoicesAcrossModeSwitches()
        {
            var store = Store();
            var other = await store.ImportAsync(SAMPLE);
            var preset = await store.ImportAsync(PresetPack());
            var mixed = store.List().MixConfiguration();
            mixed.Audio.Groups.Add(other.GroupReference("holds"));
            mixed.Builtin = new MaterialBuiltinSelection { Notes = 2, HitFx = 2 };
            store.SaveConfiguration(mixed, store.List().Revision);

            var candidate = store.ResolvePreset(preset.PackId, preset.Digest);
            Assert.That(candidate.Audio.Groups, Is.EqualTo(new[] { preset.GroupReference("clicks") }));
            Assert.That(store.List().Mode, Is.EqualTo(MaterialPackModes.MIX), "查看预设不改变选择");

            store.ApplyPreset(preset.PackId, preset.Digest, store.List().Revision);
            var state = Store().List();
            Assert.That(state.Mode, Is.EqualTo(MaterialPackModes.PRESET));
            Assert.That(state.Preset.IsFrom(preset.PackId, preset.Digest), Is.True);
            var active = state.ActiveConfiguration();
            Assert.That(active.Audio.Groups, Is.EqualTo(new[] { preset.GroupReference("clicks") }));
            Assert.That(active.Notes.Groups, Is.EqualTo(new[] { preset.GroupReference("keys") }));
            Assert.That(Sets(active.Builtin), Is.EqualTo((1, 1, 1)), "整合包各类别均跟随内置整合包");
            Assert.That(active.Judgment.Groups, Is.Empty);
            Assert.That(state.MixConfiguration().SameChoicesAs(mixed), Is.True, "使用预设不改动混搭");
            Assert.That(Sets(store.Builtin), Is.EqualTo((1, 1, 1)), "下一局取内置整合包的套装");

            store.SetMode(MaterialPackModes.MIX, store.List().Revision);
            state = store.List();
            Assert.That(state.ActiveConfiguration().SameChoicesAs(mixed), Is.True, "切回混搭恢复原来的选择");
            Assert.That(state.Preset.IsFrom(preset.PackId, preset.Digest), Is.True, "预设的选择保留");
            Assert.That(store.SetMode(MaterialPackModes.MIX, state.Revision), Is.EqualTo(state.Revision), "已是混搭时不写入");
            store.SetMode(MaterialPackModes.PRESET, state.Revision);
            Assert.That(store.List().ActiveConfiguration().SameChoicesAs(candidate), Is.True);

            store.SaveSelection(new MaterialAudioSelection { Groups = { preset.GroupReference("clicks") } }, store.List().Revision);
            Assert.That(store.List().Mode, Is.EqualTo(MaterialPackModes.MIX), "改动一个类别即使用混搭");

            store.ChoosePreset(MaterialPresetChoice.Builtin(2), store.List().Revision);
            Assert.That(Sets(store.List().ActiveConfiguration().Builtin), Is.EqualTo((2, 2, 1)));
            Assert.That(store.List().ActiveConfiguration().Audio.Groups, Is.Empty);
            long revision = store.List().Revision;
            Assert.Throws<InvalidDataException>(() => store.ApplyPreset(other.PackId, other.Digest, revision), "没有预设的包");
            Assert.Throws<InvalidDataException>(() => store.ChoosePreset(MaterialPresetChoice.Builtin(3), revision));
            Assert.Throws<ArgumentException>(() => store.SetMode("theme", revision));
            Assert.That(store.List().Revision, Is.EqualTo(revision));
        }

        [Test]
        public async Task BundlesShareModuleFallbacksAndMixKeepsItsEditableCopy()
        {
            var store = Store();
            var pack = await store.ImportAsync(PresetPack());
            store.ChoosePreset(MaterialPresetChoice.Builtin(2), store.List().Revision);
            Assert.That(Sets(Store().List().ActiveConfiguration().Builtin), Is.EqualTo((2, 2, 1)), "旧版整套选择");
            store.ApplyPreset(pack.PackId, pack.Digest, store.List().Revision);
            var sets = new MaterialBuiltinSelection { Notes = 2, HitFx = 1 };
            long before = store.List().Revision;
            long saved = store.SaveBuiltinBundle(sets, before);
            Assert.Throws<MaterialPackConflictException>(() => store.SaveBuiltinBundle(new MaterialBuiltinSelection(), before));
            Assert.Throws<InvalidDataException>(() => store.SaveBuiltinBundle(new MaterialBuiltinSelection { HitFx = 3 }, saved));
            Assert.That(store.SaveBuiltinBundle(sets, saved), Is.EqualTo(saved));
            store = Store();
            Assert.That(store.List().Preset.IsFrom(pack.PackId, pack.Digest), Is.True);
            var bundle = store.ResolvePreset(pack.PackId, pack.Digest);
            Assert.That(Sets(bundle.Builtin), Is.EqualTo((2, 1, 1)), "忽略包内旧的 audio.builtinSet=2");
            var live = store.Capture();
            try
            {
                Assert.That(Sets(live.Builtin), Is.EqualTo((2, 1, 1)));
                Assert.That(live.Audio.Single(b => b.Slot == "audio.tap").Source, Is.EqualTo(pack.GroupReference("clicks")));
            }
            finally { store.Release(); }
            store.SaveConfiguration(bundle, store.List().Revision);
            store.SaveConfiguration(MaterialPresets.BuildBuiltin(1), store.List().Revision);
            store.SetMode(MaterialPackModes.PRESET, store.List().Revision);
            store.SetMode(MaterialPackModes.MIX, store.List().Revision);
            Assert.That(store.List().ActiveConfiguration().SameChoicesAs(MaterialPresets.BuildBuiltin(1)), Is.True);
            Assert.That(Sets(store.List().Preset.GetBuiltinSets()), Is.EqualTo((2, 1, 1)));
        }

        [Test]
        public async Task TheMixFallsBackToTheBuiltinBundlesSets()
        {
            var store = Store();
            var preset = await store.ImportAsync(PresetPack());
            store.ApplyPreset(preset.PackId, preset.Digest, store.List().Revision);
            var sets = new MaterialBuiltinSelection { Notes = 2, HitFx = 1 };
            store.SaveBuiltinBundle(sets, store.List().Revision);
            Assert.That(store.List().Mode, Is.EqualTo(MaterialPackModes.PRESET), "保存内置整合包不切换模式");

            store.SetMode(MaterialPackModes.MIX, store.List().Revision);
            var state = Store().List();
            Assert.That(state.Notes.Groups, Is.EqualTo(new[] { preset.GroupReference("keys") }), "改过内置套装后，第一次进入混搭仍从预设开始");
            Assert.That(Sets(state.MixConfiguration().Builtin), Is.EqualTo((2, 1, 1)));

            store.SaveBuiltinBundle(new MaterialBuiltinSelection { Notes = 1, HitFx = 2 }, store.List().Revision);
            state = store.List();
            Assert.That(state.Mode, Is.EqualTo(MaterialPackModes.MIX));
            Assert.That(state.Notes.Groups, Is.EqualTo(new[] { preset.GroupReference("keys") }), "混搭的资源组不变");
            Assert.That(Sets(store.Builtin), Is.EqualTo((1, 2, 1)), "混搭随内置整合包回退");
        }

        [Test]
        public async Task ABundleKeepsItsChosenGroupPerCategoryUntilUninstalled()
        {
            var store = Store();
            var pack = await store.ImportAsync(PresetPack());
            var state = store.List();
            Assert.That(MaterialPresets.Options(pack, "audio").Select(g => g.Id), Does.Contain("clicks").And.Contain("holds"));
            Assert.That(MaterialPresets.Options(pack, "notes").Count, Is.EqualTo(1), "只有一个资源组的类别无需选择");
            Assert.That(MaterialPresets.Options(pack, "judgment"), Is.Empty, "预设未声明的类别");
            Assert.That(MaterialPresets.MainGroup(pack, "audio", null), Is.EqualTo("clicks"));

            long revision = store.SaveBundleGroup(pack.PackId, pack.Digest, "audio", "holds", state.Revision);
            Assert.That(revision, Is.EqualTo(state.Revision + 1));
            Assert.That(store.List().IsPresetMode, Is.True, "保存整合包设置不切换模式");
            Assert.That(store.List().ActiveConfiguration().Audio.Groups, Is.Empty, "未使用的整合包不影响下一局");
            Assert.That(store.SaveBundleGroup(pack.PackId, pack.Digest, "audio", "holds", revision), Is.EqualTo(revision), "已选用时不写入");
            Assert.Throws<InvalidDataException>(() => store.SaveBundleGroup(pack.PackId, pack.Digest, "notes", "holds", revision), "音效组不能用于音符");
            Assert.Throws<MaterialPackConflictException>(() => store.SaveBundleGroup(pack.PackId, pack.Digest, "audio", "clicks", state.Revision));

            var bundle = Store().ResolvePreset(pack.PackId, pack.Digest);
            Assert.That(bundle.Audio.Groups, Is.EqualTo(new[] { pack.GroupReference("holds") }));
            Assert.That(bundle.Notes.Groups, Is.EqualTo(new[] { pack.GroupReference("keys") }));
            store.ApplyPreset(pack.PackId, pack.Digest, store.List().Revision);
            Assert.That(store.List().ActiveConfiguration().Audio.Groups, Is.EqualTo(new[] { pack.GroupReference("holds") }));

            store.SaveBundleGroup(pack.PackId, pack.Digest, "audio", "clicks", store.List().Revision);
            Assert.That(store.List().BundleGroupsOf(pack.PackId, pack.Digest), Is.Null, "选回预设自带的组不留记录");
            Assert.That(store.List().ActiveConfiguration().Audio.Groups, Is.EqualTo(new[] { pack.GroupReference("clicks") }));

            store.SaveBundleGroup(pack.PackId, pack.Digest, "audio", "holds", store.List().Revision);
            store.Uninstall(pack.PackId, pack.Digest, store.List().Revision);
            Assert.That(Store().List().BundleGroups, Is.Empty, "卸载时移除整合包设置");
        }

        [Test]
        public async Task TheFirstSwitchToMixStartsFromThePresetAndLaterOnesKeepTheMix()
        {
            var store = Store();
            var preset = await store.ImportAsync(PresetPack());
            store.ApplyPreset(preset.PackId, preset.Digest, store.List().Revision);
            store.SetMode(MaterialPackModes.MIX, store.List().Revision);
            var mix = store.List().MixConfiguration();
            Assert.That(mix.SameChoicesAs(MaterialPresets.Build(preset)), Is.True, "混搭从未设置过：从当前预设开始");

            mix.Notes.Groups.Clear();
            store.SaveConfiguration(mix, store.List().Revision);
            store.SetMode(MaterialPackModes.PRESET, store.List().Revision);
            store.SetMode(MaterialPackModes.MIX, store.List().Revision);
            Assert.That(store.List().MixConfiguration().Notes.Groups, Is.Empty, "之后的切换保留混搭");
        }

        [Test]
        public async Task TheDemoBindsAnyConfigurationWithoutHoldingTheStore()
        {
            var store = Store();
            var preset = await store.ImportAsync(PresetPack());
            var bindings = store.Bind(MaterialPresets.Build(preset));
            Assert.That(bindings.Audio.Single(b => b.Slot == "audio.tap").Source, Is.EqualTo(preset.GroupReference("clicks")));
            Assert.That(bindings.Notes.Single(b => b.Slot == "notes.notes_2").Source, Is.EqualTo(preset.GroupReference("keys")));
            Assert.That(Sets(bindings.Builtin), Is.EqualTo((1, 1, 1)));
            Assert.That(store.IsInSession, Is.False);
            Assert.DoesNotThrow(() => store.SetMode(MaterialPackModes.MIX, store.List().Revision), "演示不占用存储");

            var captured = store.Capture();
            try
            {
                Assert.That(captured.Audio.All(b => b.Source == null), Is.True, "下一局取当前使用的混搭");
                Assert.That(store.IsInSession, Is.True);
            }
            finally { store.Release(); }
        }

        [Test]
        public void TheDemoChartHasEveryNoteKindASkinDraws()
        {
            var document = OpenSekai.App.UI.PackDemoChart.Build();
            var notes = document.NoteList;
            Assert.That(notes.Select(n => n.id).Distinct().Count(), Is.EqualTo(notes.Count));
            Assert.That(notes.Any(n => n.category == Sekai.Live.NoteCategory.Normal && n.type == Sekai.Live.NoteType.Default), Is.True, "tap");
            Assert.That(notes.Any(n => n.category == Sekai.Live.NoteCategory.Normal && n.type == Sekai.Live.NoteType.Critical), Is.True, "critical tap");
            Assert.That(notes.Any(n => n.category == Sekai.Live.NoteCategory.Flick && n.previousConnectionId == -1), Is.True, "flick");
            Assert.That(notes.Select(n => n.direction).Distinct().Count(), Is.EqualTo(3), "上、左、右三个方向");
            Assert.That(notes.Any(n => n.category == Sekai.Live.NoteCategory.Long), Is.True, "long");
            Assert.That(notes.Any(n => n.category == Sekai.Live.NoteCategory.Connection), Is.True, "relay");
            Assert.That(notes.Any(n => n.category == Sekai.Live.NoteCategory.Friction), Is.True, "trace");
            foreach (var note in notes.Where(n => n.nextConnectionId != -1))
                Assert.That(notes.Single(n => n.id == note.nextConnectionId).previousConnectionId, Is.EqualTo(note.id));

            var config = Sekai.ResourceCatalog.LiveBundleConfig.Load();
            var score = OpenSekai.Live.Conversion.RuntimeChartConverter.Convert(document, config);
            Assert.That(Sekai.LiveUtility.CalculateTotalComboCount(score), Is.GreaterThan(30));
        }

        [Test]
        public async Task UninstallingThePresetSourceFallsBackToTheBuiltinPresetAndLeavesTheMix()
        {
            var store = Store();
            var unrelated = await store.ImportAsync(SAMPLE);
            var preset = await store.ImportAsync(PresetPack());
            var mix = store.List().MixConfiguration();
            mix.Notes.Groups.Add(preset.GroupReference("keys"));
            store.SaveConfiguration(mix, store.List().Revision);
            store.ApplyPreset(preset.PackId, preset.Digest, store.List().Revision);
            var before = store.List();

            var impact = store.PreviewUninstall(unrelated.PackId, unrelated.Digest);
            Assert.That(impact.ChangesConfiguration, Is.False);
            Assert.That(store.List().Revision, Is.EqualTo(before.Revision), "预检只读");
            store.Uninstall(unrelated.PackId, unrelated.Digest, before.Revision, out impact);
            Assert.That(impact.ChangesConfiguration, Is.False);
            Assert.That(store.List().Preset.IsFrom(preset.PackId, preset.Digest), Is.True);

            impact = store.PreviewUninstall(preset.PackId, preset.Digest);
            Assert.That(impact.Categories, Is.EqualTo(new[] { "notes" }), "混搭里用到它的类别");
            Assert.That(impact.ClearedPreset, Is.EqualTo("清脆打击"));
            store.Uninstall(preset.PackId, preset.Digest, store.List().Revision);
            var state = Store().List();
            Assert.That(state.Mode, Is.EqualTo(MaterialPackModes.PRESET));
            Assert.That(state.Preset.IsBuiltin && state.Preset.BuiltinSet == 1, Is.True);
            Assert.That(state.ActiveConfiguration().SameChoicesAs(MaterialPresets.BuildBuiltin(1)), Is.True);
            Assert.That(state.Notes.Groups, Is.Empty);
            Assert.That(state.Installed, Is.Empty);
        }

        [Test]
        public async Task AnOlderIndexGetsItsMetadataFromTheInstalledManifestOnce()
        {
            var store = Store();
            var pack = await store.ImportAsync(PresetPack());
            string index = Path.Combine(_root, "state.json");
            var json = JObject.Parse(File.ReadAllText(index));
            var entry = (JObject)json["Installed"][0];
            entry.Remove("DisplayName");
            entry.Remove("Icon");
            entry.Remove("Preset");
            entry.Remove("MetadataVersion");
            File.WriteAllText(index, json.ToString());

            store = Store();
            Assert.That(store.List().Installed.Single().MetadataVersion, Is.Zero);
            Assert.That(store.List().Installed.Single().Preset, Is.Null);
            Assert.That(await store.BackfillMetadataAsync(), Is.EqualTo(1));
            var filled = Store().List().Installed.Single();
            Assert.That(filled.DisplayName, Is.EqualTo("墨色打歌"));
            Assert.That(filled.Icon.Hash, Is.EqualTo(pack.Icon.Hash));
            Assert.That(filled.Preset.Name, Is.EqualTo("清脆打击"));
            Assert.That(await Store().BackfillMetadataAsync(), Is.Zero);
        }

        [Test]
        public async Task AGroupPreviewListsItsOwnMediaWithoutChangingTheSelection()
        {
            var store = Store();
            var pack = await store.ImportAsync(PresetPack());
            long revision = store.List().Revision;
            var audio = store.DescribeGroup(pack.PackId, pack.Digest, "clicks");
            Assert.That(audio.Items.Select(i => i.Slot), Is.EqualTo(new[] { "audio.tap", "audio.perfect" }));
            Assert.That(audio.Items.All(i => i.Kind == MaterialPreviewKind.Audio), Is.True);
            Assert.That(audio.ProvidedSlots, Is.EqualTo(2));
            Assert.That(audio.TotalSlots, Is.EqualTo(MaterialPackAudio.Slots.Count()));
            var notes = store.DescribeGroup(pack.PackId, pack.Digest, "keys").Items.Single();
            Assert.That(notes.Kind, Is.EqualTo(MaterialPreviewKind.Image));
            Assert.That(MaterialPackArchive.ReadVerified(notes.Path, notes.Media.Hash), Is.Not.Empty);
            Assert.That(store.List().Revision, Is.EqualTo(revision));
        }

        // ------------------------------------------------------------------ resolution

        [Test]
        public void SlotStatesTellOverridesFromFallsAndSequencesHideTheirParts()
        {
            var pack = new InstalledMaterialPack { PackId = "test.pack", Digest = new string('a', 64), Version = "1" };
            pack.Groups.Add(new MaterialPackGroup { Id = "events", Category = "resultFx", Sprites = { ["resultFx.clear"] = Sequence() } });
            pack.Groups.Add(new MaterialPackGroup { Id = "parts", Category = "resultFx", Sprites =
            {
                ["resultFx.clear.line"] = Image(), ["resultFx.failure.line"] = Image()
            } });
            var selection = new MaterialGroupSelection { Groups = { pack.GroupReference("events"), pack.GroupReference("parts") } };
            selection.Overrides["resultFx.lifeZero.line"] = null;
            var states = MaterialPackResolver.Describe(new[] { pack }, "resultFx", selection, _root);
            var clear = states.Single(s => s.Slot == "resultFx.clear");
            Assert.That(clear.IsSequence, Is.True);
            Assert.That(clear.Reason, Is.EqualTo(MaterialSlotState.REASON_PRIORITY));
            Assert.That(states.Single(s => s.Slot == "resultFx.lifeZero.line").IsExplicitBuiltin, Is.True);
            var fall = states.Single(s => s.Slot == "resultFx.allPerfect.line");
            Assert.That(fall.Source, Is.Null);
            Assert.That(fall.IsExplicitBuiltin, Is.False);
            var hidden = MaterialPackResolver.HiddenSlots("resultFx", states);
            Assert.That(hidden, Does.Contain("resultFx.clear.line"));
            Assert.That(hidden, Does.Contain("resultFx.clear.layout"));
            Assert.That(hidden, Does.Not.Contain("resultFx.failure.line"));

            var hit = new InstalledMaterialPack { PackId = "test.hit", Digest = new string('c', 64), Version = "1" };
            hit.Groups.Add(new MaterialPackGroup { Id = "seq", Category = "hitFx", Sprites = { ["hitFx.critical.aura"] = Sequence() } });
            var hitStates = MaterialPackResolver.Describe(new[] { hit }, "hitFx",
                new MaterialGroupSelection { Groups = { hit.GroupReference("seq") } }, _root);
            Assert.That(MaterialPackResolver.HiddenSlots("hitFx", hitStates), Is.EquivalentTo(new[] { "hitFx.critical.aura.trail" }));
        }

        [Test]
        public void TheComboLayoutComesFromTheFirstEnabledGroupThatHasOne()
        {
            var pack = new InstalledMaterialPack { PackId = "test.combo", Digest = new string('d', 64), Version = "1" };
            pack.Groups.Add(new MaterialPackGroup { Id = "digits", Category = "combo", Sprites = { ["combo.digit.1"] = Image() } });
            pack.Groups.Add(new MaterialPackGroup { Id = "layout", Category = "combo", Layout = new MaterialComboLayout { Scale = new[] { 1f, 1f } } });
            var selection = new MaterialGroupSelection { Groups = { pack.GroupReference("digits"), pack.GroupReference("layout") } };
            Assert.That(MaterialPackResolver.ComboLayoutSource(new[] { pack }, selection), Is.EqualTo(pack.GroupReference("layout")));
            selection.Groups.RemoveAt(1);
            Assert.That(MaterialPackResolver.ComboLayoutSource(new[] { pack }, selection), Is.Null);
        }

        [Test]
        public void GroupReferencesSplitIntoPackDigestAndGroup()
        {
            Assert.That(InstalledMaterialPack.TryParseReference("a.b/" + new string('e', 64) + "/g-1", out string id, out string digest, out string group), Is.True);
            Assert.That(id, Is.EqualTo("a.b"));
            Assert.That(digest, Has.Length.EqualTo(64));
            Assert.That(group, Is.EqualTo("g-1"));
            Assert.That(InstalledMaterialPack.TryParseReference("builtin", out _, out _, out _), Is.False);
            Assert.That(InstalledMaterialPack.TryParseReference("a//g", out _, out _, out _), Is.False);
        }

        // ------------------------------------------------------------------ helpers

        private MaterialPackStore Store(MaterialBuiltinSelection legacy = null) =>
            new MaterialPackStore(_root, legacy == null ? null : () => legacy);

        private static (int, int, int) Sets(MaterialBuiltinSelection builtin) => (builtin.Notes, builtin.HitFx, builtin.Audio);

        private void StripFromIndex(string field)
        {
            string index = Path.Combine(_root, "state.json");
            var json = JObject.Parse(File.ReadAllText(index));
            json.Remove(field);
            File.WriteAllText(index, json.ToString());
            File.Delete(index + ".bak");
        }

        private static MaterialSpriteSlot Image() => new()
        {
            File = "a.png", Hash = new string('b', 64), Width = 2, Height = 2, Rect = new[] { 0, 0, 2, 2 }
        };

        private static MaterialSpriteSlot Sequence()
        {
            var layer = new MaterialEffectLayer
            {
                File = "s.png", Hash = new string('f', 64), Width = 2, Height = 2,
                Frames = new[] { new MaterialEffectFrame { Rect = new[] { 0, 0, 2, 2 }, DurationMs = 50 } }
            };
            return new MaterialSpriteSlot { File = layer.File, Hash = layer.Hash, Width = 2, Height = 2, Rect = new[] { 0, 0, 2, 2 }, Layers = new[] { layer } };
        }

        /// <summary>The audio fixture plus a notes group, an icon and the metadata and preset extensions.</summary>
        private string PresetPack(string fault = null, Action<JObject> editCategories = null)
        {
            string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
            using var input = ZipFile.OpenRead(SAMPLE);
            var manifest = JObject.Parse(new StreamReader(input.GetEntry("pack.json").Open()).ReadToEnd());
            manifest["packId"] = "opensekai.ink";
            ((JArray)manifest["groups"]).Add(new JObject
            {
                ["id"] = "keys", ["category"] = "notes", ["displayName"] = "墨色音符",
                ["slots"] = new JObject { ["notes.notes_2"] = new JObject { ["file"] = "art/note.png" } }
            });
            var audio = new JObject
            {
                ["groups"] = new JArray(fault switch { "foreignGroup" => "nope", "wrongCategory" => "keys", _ => "clicks" }),
                ["builtinSet"] = fault == "builtinRange" ? 3 : 2
            };
            var categories = new JObject { ["audio"] = audio, ["notes"] = new JObject { ["groups"] = new JArray("keys") } };
            if (fault == "builtinOnJudgment") categories["judgment"] = new JObject { ["builtinSet"] = 1 };
            if (fault == "unknownCategory") categories["theme"] = new JObject();
            editCategories?.Invoke(categories);
            var metadata = new JObject { ["displayName"] = "墨色打歌", ["icon"] = fault == "missingIcon" ? "art/none.png" : "art/icon.png" };
            if (fault == "metadataField") metadata["author"] = "x";
            var presetObject = new JObject { ["name"] = fault == "presetName" ? " " : "清脆打击", ["categories"] = categories };
            manifest["extensions"] = new JObject
            {
                [MaterialPackManifest.METADATA_EXTENSION] = metadata,
                [MaterialPackManifest.PRESET_EXTENSION] = presetObject,
                ["another.tool"] = new JObject { ["anything"] = true }
            };

            using var output = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var item in input.Entries)
            {
                if (item.FullName == "pack.json") continue;
                using var source = item.Open();
                using var target = output.CreateEntry(item.FullName).Open();
                source.CopyTo(target);
            }

            Write(output, "pack.json", Encoding.UTF8.GetBytes(manifest.ToString()));
            Write(output, "art/icon.png", Png(4));
            Write(output, "art/note.png", Png(2));
            return path;
        }

        private static void Write(ZipArchive zip, string name, byte[] bytes)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] Png(int size)
        {
            var texture = new UnityEngine.Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false);
            try { return UnityEngine.ImageConversion.EncodeToPNG(texture); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
