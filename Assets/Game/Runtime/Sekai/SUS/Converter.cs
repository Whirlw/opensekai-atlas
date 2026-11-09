using Sekai.Live;
using UnityEngine;
using OpenSekai.Shared.Conversion;
using OpenSekai.Shared.Settings;
namespace Sekai.SUS
{
    /// <summary>Compatibility entry for callers outside the extracted playback path.</summary>
    public class Converter
    {
        public static void InvalidateLiveSettingCache() { }
        public MusicScore Convert(string text, bool isNeedCombo = true, bool? overrideMirror = null)
        {
            var settings = LiveSettingsStore.Shared.Load();
            return new SusRuntimeConverter().Convert(text, Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath),
                isNeedCombo, overrideMirror ?? settings.IsMirror, settings.UseSimultaneousPushingLine);
        }
    }
}
