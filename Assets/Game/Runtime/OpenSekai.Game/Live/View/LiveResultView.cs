using OpenSekai.MaterialPacks;
using Sekai.Core.Live;
using LitMotion;
using UnityEngine;
using Sekai;

namespace Sekai.Live
{
	public class LiveResultView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_background")]
		[SerializeField]
		private SpriteRenderer background;

		private bool _shouldPlayVoiceSE;
		private MotionHandle _backgroundMotion;

		private const int EFFECT_ORDER = 220;
		private LiveResultEffectSkin _skin;
		private GameObject _instance;
		private HitEffectSequence _sequence;
		private SpriteRendererBaseline _backgroundBaseline;
		private Color _backgroundColor;
		private bool _customBackground;

		public void Setup(bool shouldPlayVoiceSE, LiveResultEffectSkin skin = null)
		{
			StopPresentation();
			_skin = skin;
			if (background != null)
			{
				if (_backgroundBaseline == null)
				{
					_backgroundBaseline = new SpriteRendererBaseline(background);
					_backgroundColor = background.color;
				}
				_customBackground = skin?.DrawBackground(background, _backgroundBaseline) == true;
				if (!_customBackground) _backgroundBaseline.Draw(background, null, _backgroundBaseline.Sprite, true);
				background.color = _backgroundColor;
			}
			_shouldPlayVoiceSE = shouldPlayVoiceSE;
		}

		public void Execute(LiveResultAnimationType liveResultAnimationType)
		{
			switch (liveResultAnimationType)
			{
				case LiveResultAnimationType.None:
					StopPresentation();
					return;
				case LiveResultAnimationType.Failure:
					PlayEndVoice(LiveSoundDefine.SE_LIVE_END_FINISH);
					Fade("fx_result_failed_v2");
					break;
				case LiveResultAnimationType.LifeZero:
					PlayEndVoice(LiveSoundDefine.SE_LIVE_END_FINISH);
					Fade("fx_result_finish");
					break;
				case LiveResultAnimationType.Clear:
					SoundManager.Instance.PlayIngameSE(LiveSoundDefine.SE_LIVE_CLEAR);
					PlayEndVoice(LiveSoundDefine.SE_LIVE_END_CLEAR);
					Fade("fx_result_clear_v2");
					break;
				case LiveResultAnimationType.FullCombo:
					SoundManager.Instance.PlayIngameSE(LiveSoundDefine.SE_LIVE_FULL_COMBO);
					PlayEndVoice(LiveSoundDefine.SE_LIVE_END_PERFECT);
					Fade("fx_result_full_combo_v2");
					break;
				case LiveResultAnimationType.AllPerfect:
					SoundManager.Instance.PlayIngameSE(LiveSoundDefine.SE_LIVE_ALL_PERFECT);
					PlayEndVoice(LiveSoundDefine.SE_LIVE_END_ALL_PERFECT);
					Fade("fx_result_all_perfect_v2");
					break;
				default:
					Debug.LogErrorFormat("Unsupported live result animation type: {0}", liveResultAnimationType);
					break;
			}
		}

		public void Unload()
		{
			StopPresentation();
			AssetBundleUtility.UnloadAssetBundle(LiveConfig.ResultBundleName);
		}

		private void Fade(string prefabName)
		{
			StopPresentation();
			var fade = LMotion.Create(0f, 1f, 0.2f)
				.WithEase(Ease.OutCirc)
				.WithScheduler(LiveMotion.Scheduler(unscaled: true))
				.WithOnComplete(() => ShowResult(prefabName));
			if (background == null)
			{
				_backgroundMotion = fade.RunWithoutBinding();
				return;
			}

			background.gameObject.SetActive(true);
			Color from = _customBackground ? new Color(1f, 1f, 1f, _backgroundColor.a) : _backgroundColor;
			Color to = _customBackground ? new Color(1f, 1f, 1f, 0.5f) : new Color(0f, 0f, 0f, 0.5f);
			_backgroundMotion = fade.Bind(progress => background.color = Color.LerpUnclamped(from, to, progress));
		}

		private void ShowResult(string prefabName)
		{
			string key = MaterialResultFxSlots.ForTemplate(prefabName);
			if (_skin != null && key != null && _skin.HasSequence(key))
			{
				_instance = new GameObject(key);
				_instance.transform.SetParent(transform, false);
				// Result templates render on the Default layer. The backdrop uses a separate UI camera layer.
				_sequence = _skin.CreateSequence(key, _instance.transform, EFFECT_ORDER);
				return;
			}
			GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.ResultBundleName, prefabName, false);
			if (prefab == null)
			{
				prefab = Resources.Load<GameObject>(prefabName);
			}
			if (prefab != null)
			{
				_instance = Instantiate(prefab, transform);
				if (key != null) _skin?.ApplyTemplate(key, _instance);
				Renderer[] renderers = _instance.GetComponentsInChildren<Renderer>(true);
				for (int i = 0; i < renderers.Length; i++)
				{
					renderers[i].sortingOrder += EFFECT_ORDER;
				}
			}
		}

		private void OnDisable() => StopPresentation();

		private void Update()
		{
			if (_sequence == null) return;
			_sequence.Advance(Time.unscaledDeltaTime);
			if (!_sequence.Active) ClearInstance();
		}

		private void ClearInstance()
		{
			_sequence?.Stop();
			_sequence = null;
			if (_instance == null) return;
			_instance.SetActive(false);
			Destroy(_instance);
			_instance = null;
		}

		private void StopPresentation()
		{
			_backgroundMotion.TryCancel();
			ClearInstance();
			if (background != null) background.gameObject.SetActive(false);
		}

		private void PlayEndVoice(string cueName)
		{
			if (_shouldPlayVoiceSE)
			{
				SoundManager.Instance.PlayIngameSEOneShot(cueName);
			}
		}
	}
}
