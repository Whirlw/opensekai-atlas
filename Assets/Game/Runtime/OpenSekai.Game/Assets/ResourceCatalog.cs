using Sekai.Live;
using TMPro;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// One fixed <see cref="Resources"/> asset: where it lives, an optional second casing to try, and the
	/// type it must come back as.
	/// </summary>
	/// <remarks>
	/// Loads are plain synchronous <c>Resources.Load</c> calls, exactly what the call sites did before,
	/// so the fallback order and the null result keep their old meaning. Async and batched loading stay on
	/// <see cref="ResourceLoader"/>, which is the dynamic path.
	/// </remarks>
	public readonly struct ResourceEntry<T> where T : Object
	{
		private readonly string _path;
		private readonly string _fallbackPath;

		public ResourceEntry(string path, string fallbackPath = null)
		{
			_path = path;
			_fallbackPath = fallbackPath;
		}

		/// <summary>Primary Resources path, without extension.</summary>
		public string Path => _path;

		/// <summary>Second path tried only when the primary returns null; null when there is none.</summary>
		public string FallbackPath => _fallbackPath;

		public T Load()
		{
			T asset = Resources.Load<T>(_path);
			if (asset == null && !string.IsNullOrEmpty(_fallbackPath))
			{
				asset = Resources.Load<T>(_fallbackPath);
			}
			return asset;
		}

		public bool TryLoad(out T asset)
		{
			asset = Load();
			return asset != null;
		}
	}

	/// <summary>
	/// The typed directory of assets the game loads from <c>Resources</c> by fixed path.
	/// </summary>
	/// <remarks>
	/// The point is that no call site spells a Resources path any more: fonts, the live configuration and
	/// the handful of fixed prefabs and materials are named here once, with their type, so a rename or a
	/// casing mistake is a compile-time or single-line change instead of a silent runtime null.
	/// Assets addressed by data (screen layers by name, dialog prefabs, note prefabs chosen by config,
	/// wording tables, atlas sprites) deliberately stay on the dynamic path — they are keys, not entries.
	/// </remarks>
	public static class ResourceCatalog
	{
		// ---- fonts -------------------------------------------------------------------------------
		// TMP font assets live under Assets/Graphics/Resources/font, the raw OTF/TTF under
		// Assets/Game/Resources/Fonts.

		public static readonly ResourceEntry<TMP_FontAsset> BoldBaseFont =
			new ResourceEntry<TMP_FontAsset>("font/SourceHanSansSC-Bold SDF_Base");

		public static readonly ResourceEntry<TMP_FontAsset> BoldDynamicFont =
			new ResourceEntry<TMP_FontAsset>("font/SourceHanSansSC-Bold SDF_Dynamic");

		public static readonly ResourceEntry<TMP_FontAsset> MediumBaseFont =
			new ResourceEntry<TMP_FontAsset>("font/SourceHanSansSC-Medium SDF_Base");

		public static readonly ResourceEntry<TMP_FontAsset> MediumDynamicFont =
			new ResourceEntry<TMP_FontAsset>("font/SourceHanSansSC-Medium SDF_Dynamic");

		public static readonly ResourceEntry<Font> MediumTtfFont =
			new ResourceEntry<Font>("Fonts/SourceHanSansSC-Medium");

		public static readonly ResourceEntry<Font> BoldTtfFont =
			new ResourceEntry<Font>("Fonts/SourceHanSansSC-Bold");

		// ---- fixed configuration -----------------------------------------------------------------

		/// <summary>The live bundle config that lane offsets, note speeds and SE volume read from.</summary>
		public static readonly ResourceEntry<LiveBundleBuildData> LiveBundleConfig =
			new ResourceEntry<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);

		// ---- fixed prefabs -----------------------------------------------------------------------

		public static readonly ResourceEntry<GameObject> LiveTransitioner =
			new ResourceEntry<GameObject>("Common/Transition/LiveTransitioner");

		public static readonly ResourceEntry<GameObject> EventSystem =
			new ResourceEntry<GameObject>("Common/Input/EventSystem");

		/// <summary>Background cover the dialogs draw behind themselves; ships with a lower-cased copy.</summary>
		public static readonly ResourceEntry<GameObject> DialogFillCover =
			new ResourceEntry<GameObject>("UI/UIParts/UIPartsDialogFillCover", "ui/uiparts/uipartsdialogfillcover");

		/// <summary>Chrome shown when a live finishes; the lower-cased path is the historical layout.</summary>
		public static readonly ResourceEntry<GameObject> LiveOutUi =
			new ResourceEntry<GameObject>("Live/UI/LiveOutUI", "live/ui/LiveOutUI");

		/// <summary>The screen layers the boot scene starts with.</summary>
		public static readonly ResourceEntry<ScreenLayerDataList> EntryScreenLayers =
			new ResourceEntry<ScreenLayerDataList>("Screen/SceneEntry/EntryScreenLayers");

		// ---- fixed materials ---------------------------------------------------------------------

		public static readonly ResourceEntry<Material> PairNoteLineMaterial =
			new ResourceEntry<Material>("Materials/Ingame/PairNoteLine", "materials/ingame/PairNoteLine");

		public static readonly ResourceEntry<Material> LongNoteLineMaterial =
			new ResourceEntry<Material>("Materials/Ingame/LongNoteLine", "materials/ingame/LongNoteLine");
	}
}
