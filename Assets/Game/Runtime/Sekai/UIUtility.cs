using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using CriWare;

namespace Sekai
{
	public class UIUtility
	{
		private static readonly Dictionary<string, Dictionary<string, Sprite>> _cachedSprites = new Dictionary<string, Dictionary<string, Sprite>>();
		private const string LIVE_TRANSITIONER_PREFAB_PATH = "Common/Transition/LiveTransitioner";

		public static bool IsShowCostumeInfo { get; set; }

		public static LiveTransitioner PlayLiveTransition(LiveTransitioner.TransitionType type, Action onFinished, string seName = "SE_AREA_TRANSITION_SEKAI", bool showsLoading = false, float timeout = 0f, float delay = 1f, float duration = 1f)
		{
			DestroyLiveTransition();

			GameObject prefab = Resources.Load<GameObject>(LIVE_TRANSITIONER_PREFAB_PATH);
			if (prefab == null)
			{
				Debug.LogWarningFormat("LiveTransitioner prefab could not be loaded. path:{0}", LIVE_TRANSITIONER_PREFAB_PATH);
				onFinished?.Invoke();
				return null;
			}

			GameObject instance = UnityEngine.Object.Instantiate(prefab);
			instance.name = prefab.name;
			LiveTransitioner transitioner = instance.GetComponent<LiveTransitioner>();
			if (transitioner == null)
			{
				UnityEngine.Object.Destroy(instance);
				onFinished?.Invoke();
				return null;
			}

			transitioner.Play(type, onFinished, seName, showsLoading, timeout, delay, duration);
			return transitioner;
		}

		public static void DestroyLiveTransition()
		{
			if (LiveTransitioner.Exists)
			{
				LiveTransitioner.Instance.ForceFinish(null);
			}
		}

		public static Sprite GetSharedSprite(SpriteAtlas atlas, string spriteName)
		{
			if (atlas == null || string.IsNullOrEmpty(spriteName))
			{
				return null;
			}

			string atlasName = atlas.name;
			if (!_cachedSprites.TryGetValue(atlasName, out Dictionary<string, Sprite> atlasSprites))
			{
				atlasSprites = new Dictionary<string, Sprite>();
				_cachedSprites.Add(atlasName, atlasSprites);
			}

			if (atlasSprites.TryGetValue(spriteName, out Sprite cachedSprite) && cachedSprite != null)
			{
				return cachedSprite;
			}

			Sprite sprite = atlas.GetSprite(spriteName);
			if (sprite != null && !atlasSprites.ContainsKey(spriteName))
			{
				atlasSprites.Add(spriteName, sprite);
			}
			return sprite;
		}

		public static void UnloadSharedSprites()
		{
			foreach (Dictionary<string, Sprite> atlasSprites in _cachedSprites.Values)
			{
				UnloadSprites(atlasSprites);
				atlasSprites.Clear();
			}
			_cachedSprites.Clear();
		}

		public static void UnloadSharedSprites(string atlasName)
		{
			if (string.IsNullOrEmpty(atlasName) || !_cachedSprites.TryGetValue(atlasName, out Dictionary<string, Sprite> atlasSprites))
			{
				return;
			}

			UnloadSprites(atlasSprites);
			atlasSprites.Clear();
		}

		private static void UnloadSprites(Dictionary<string, Sprite> sprites)
		{
			foreach (Sprite sprite in sprites.Values)
			{
				if (sprite != null)
				{
					Resources.UnloadAsset(sprite);
				}
			}
		}
	}
}
