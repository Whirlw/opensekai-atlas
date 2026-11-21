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
        private static async void EnableAudioZip()
        {
            string path = EditorUtility.OpenFilePanel("选择要试听的打歌音效包", Path.GetFullPath("Tools/fixtures"), "zip");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var store = MaterialPackStore.Shared;
                var pack = await store.ImportAsync(path);
                var state = store.List();
                var selection = new MaterialAudioSelection { Groups = pack.Groups.Select(g => pack.GroupReference(g.Id)).ToList() };
                store.SaveSelection(selection, state.Revision);
                Debug.Log("[MaterialPack] 下一局将使用 " + pack.PackId + " 的音效。");
            }
            catch (Exception e) { Debug.LogWarning("[MaterialPack] " + e.Message); }
        }

        [MenuItem("OpenSekai/Dev/Material Packs/Restore built-in audio")]
        private static void Restore()
        {
            try
            {
                var store = MaterialPackStore.Shared;
                store.SaveSelection(new MaterialAudioSelection(), store.List().Revision);
                Debug.Log("[MaterialPack] 下一局将使用内置音效。");
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
