using OpenSekai.MaterialPacks;
using System;
using System.IO;
using System.Linq;
using OpenSekai.Infrastructure.Packages;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    internal static class MaterialPackSampleCommands
    {
        [MenuItem("OpenSekai/Dev/Material Packs/Enable audio ZIP...")]
        private static void EnableAudioZip() => EnableZip("audio");

        [MenuItem("OpenSekai/Dev/Material Packs/Enable COMBO ZIP...")]
        private static void EnableComboZip() => EnableZip("combo");

        [MenuItem("OpenSekai/Dev/Material Packs/Enable judgment ZIP...")]
        private static void EnableJudgmentZip() => EnableZip("judgment");

        [MenuItem("OpenSekai/Dev/Material Packs/Enable hitFx ZIP...")]
        private static void EnableHitFxZip() => EnableZip("hitFx");

        private static async void EnableZip(string category)
        {
            string directory = category == "audio" ? "Tools/fixtures" : "LocalPacks";
            string path = EditorUtility.OpenFilePanel("选择 " + category + " 资源包", Path.GetFullPath(directory), "zip");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var store = MaterialPackStore.Shared;
                var pack = await store.ImportAsync(path);
                var state = store.List();
                var groups = pack.Groups.Where(g => g.Category == category).Select(g => pack.GroupReference(g.Id)).ToList();
                if (groups.Count == 0) throw new InvalidDataException("包内没有 " + category + " 组。");
                if (category == "audio") store.SaveSelection(new MaterialAudioSelection { Groups = groups }, state.Revision);
                else if (category == "judgment") store.SaveJudgmentSelection(new MaterialGroupSelection { Groups = groups }, state.Revision);
                else if (category == "hitFx") store.SaveHitFxSelection(new MaterialGroupSelection { Groups = groups }, state.Revision);
                else store.SaveComboSelection(new MaterialGroupSelection { Groups = groups }, state.Revision);
                Debug.Log("[MaterialPack] 下一局将使用 " + pack.PackId + " 的 " + category + " 组。");
            }
            catch (Exception e) { Debug.LogWarning("[MaterialPack] " + e.Message); }
        }

        [MenuItem("OpenSekai/Dev/Material Packs/Restore built-in audio")]
        private static void Restore() => Restore("audio");

        [MenuItem("OpenSekai/Dev/Material Packs/Restore built-in COMBO")]
        private static void RestoreCombo() => Restore("combo");

        [MenuItem("OpenSekai/Dev/Material Packs/Restore built-in judgment")]
        private static void RestoreJudgment() => Restore("judgment");

        [MenuItem("OpenSekai/Dev/Material Packs/Restore built-in hitFx")]
        private static void RestoreHitFx() => Restore("hitFx");

        private static void Restore(string category)
        {
            try
            {
                var store = MaterialPackStore.Shared;
                if (category == "audio") store.SaveSelection(new MaterialAudioSelection(), store.List().Revision);
                else if (category == "judgment") store.SaveJudgmentSelection(new MaterialGroupSelection(), store.List().Revision);
                else if (category == "hitFx") store.SaveHitFxSelection(new MaterialGroupSelection(), store.List().Revision);
                else store.SaveComboSelection(new MaterialGroupSelection(), store.List().Revision);
                Debug.Log("[MaterialPack] 下一局将使用内置 " + category + "。");
            }
            catch (Exception e) { Debug.LogWarning("[MaterialPack] " + e.Message); }
        }
    }

    // A player test needs the same ZIP as the editor, including on Android where streaming assets reside in the APK.
    internal sealed class MaterialPackTestFixtureBuild : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            if ((context.BuildPlayerOptions.options & BuildOptions.IncludeTestAssemblies) != 0)
                context.AddAdditionalPathToStreamingAssets("Tools/fixtures/material-audio.zip", "material-audio.zip");
        }
    }
}
