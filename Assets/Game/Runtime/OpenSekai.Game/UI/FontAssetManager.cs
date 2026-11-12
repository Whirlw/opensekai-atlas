using System;
using System.Collections.Generic;
using CP;
using TMPro;
using UnityEngine;

namespace Sekai
{
	public sealed class FontAssetManager : SingletonMonoBehaviour<FontAssetManager>
	{
		private TMP_FontAsset _baseFontEB;

		private TMP_FontAsset _dynamicFontEB;

		private TMP_FontAsset _baseFontDB;

		private TMP_FontAsset _dynamicFontDB;

		private bool _isOnDemandFontSetup;

		private readonly List<TMP_FontAsset> _loadedOnDemandAssets = new List<TMP_FontAsset>();

		private bool _isInitialized;

		private bool _isDynamicFontAssetReset;

		protected override void OnInitialize()
		{
			base.OnInitialize();
			if (_isInitialized)
			{
				return;
			}

			_baseFontEB = ResourceCatalog.BoldBaseFont.Load();
			ClearFallbackFontAsset(_baseFontEB);
			_dynamicFontEB = ResourceCatalog.BoldDynamicFont.Load();

			_baseFontDB = ResourceCatalog.MediumBaseFont.Load();
			ClearFallbackFontAsset(_baseFontDB);
			_dynamicFontDB = ResourceCatalog.MediumDynamicFont.Load();

			_isInitialized = true;
		}

		public override void OnFinalize()
		{
			UnloadOnDemandFontAsset();
			UnloadDynamicFontAsset();
			UnloadBaseFontAsset();
			_isInitialized = false;
			_isDynamicFontAssetReset = false;
		}

		public void SetupBuiltinFontAsset()
		{
			if (!_isInitialized)
			{
				OnInitialize();
			}

			UnloadOnDemandFontAsset();
			ResetDynamicFontAssets();

			ClearPrimaryFontAssetData(_baseFontEB);
			ClearFallbackFontAsset(_baseFontEB);
			AddFallbackFontAsset(_baseFontEB, _dynamicFontEB);

			ClearPrimaryFontAssetData(_baseFontDB);
			ClearFallbackFontAsset(_baseFontDB);
			AddFallbackFontAsset(_baseFontDB, _dynamicFontDB);

			_isOnDemandFontSetup = false;
		}

		public void SetupOnDemandFontAsset()
		{
			// An earlier implementation swaps in AssetBundle-loaded on-demand fonts here.
			// OpenSekai currently ships the builtin font atlases locally, so this
			// keeps the same fallback shape until the on-demand font bundle path is restored.
			SetupBuiltinFontAsset();
		}

		private void ResetDynamicFontAssets()
		{
			if (_isDynamicFontAssetReset)
			{
				return;
			}

			// Dynamic TMP font assets can retain runtime character tables while their
			// atlas texture still points at the copied project PNG. Clear them before
			// fallback setup so TMP regenerates glyphs for local text.
			ClearDynamicFontAssetData(_dynamicFontEB);
			ClearDynamicFontAssetData(_dynamicFontDB);
			_isDynamicFontAssetReset = true;
		}

		private void ClearPrimaryFontAssetData(TMP_FontAsset fontAsset)
		{
			// Keep the prefab/font references intact, but make the base
			// Rodin assets act as empty shells so text resolves through one
			// dynamic fallback font instead of mixing Rodin and CJK glyphs.
			SafeClearFontAssetData(fontAsset);
		}

		private void ClearDynamicFontAssetData(TMP_FontAsset fontAsset)
		{
			SafeClearFontAssetData(fontAsset);
		}

		/// <summary>
		/// TMP's ClearFontAssetData walks the atlas texture array without a null check and
		/// memsets the texture data; a player build whose font asset shipped without an
		/// atlas texture therefore dies with a native SIGSEGV. Skip the call when there is
		/// nothing to clear, and never let a font hiccup take the boot sequence down.
		/// </summary>
		private static void SafeClearFontAssetData(TMP_FontAsset fontAsset)
		{
			if (fontAsset == null)
			{
				return;
			}

			Texture2D[] atlasTextures = fontAsset.atlasTextures;
			if (atlasTextures == null || atlasTextures.Length == 0 || atlasTextures[0] == null)
			{
				return;
			}

			// Dynamic font assets start with a 1x1 placeholder atlas; TMP grows it on the
			// first glyph request. Clearing a placeholder makes TMP memset zero bytes and
			// crash in the player, so leave it alone until real pixels exist.
			Texture2D atlas = atlasTextures[0];
			if (atlas.width <= 1 || atlas.height <= 1)
			{
				return;
			}

			try
			{
				fontAsset.ClearFontAssetData(false);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[FontAssetManager] failed to clear " + fontAsset.name + ": " + exception);
			}
		}

		private void ClearFallbackFontAsset(TMP_FontAsset fontAsset)
		{
			if (fontAsset == null)
			{
				return;
			}

			if (fontAsset.fallbackFontAssetTable == null)
			{
				fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
				return;
			}

			fontAsset.fallbackFontAssetTable.Clear();
		}

		private void AddFallbackFontAsset(TMP_FontAsset fontAsset, TMP_FontAsset fallbackFontAsset)
		{
			if (fontAsset == null || fallbackFontAsset == null)
			{
				return;
			}

			if (fontAsset.fallbackFontAssetTable == null)
			{
				fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
			}

			if (!fontAsset.fallbackFontAssetTable.Contains(fallbackFontAsset))
			{
				fontAsset.fallbackFontAssetTable.Add(fallbackFontAsset);
			}
		}

		private void UnloadBaseFontAsset()
		{
			ClearFallbackFontAsset(_baseFontEB);
			ClearFallbackFontAsset(_baseFontDB);
			_baseFontEB = null;
			_baseFontDB = null;
		}

		private void UnloadDynamicFontAsset()
		{
			_dynamicFontEB = null;
			_dynamicFontDB = null;
		}

		private void UnloadOnDemandFontAsset()
		{
			foreach (TMP_FontAsset fontAsset in _loadedOnDemandAssets)
			{
				if (fontAsset != null)
				{
					Resources.UnloadAsset(fontAsset);
				}
			}

			_loadedOnDemandAssets.Clear();
			_isOnDemandFontSetup = false;
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

		public FontAssetManager()
		{
		}
	}
}
