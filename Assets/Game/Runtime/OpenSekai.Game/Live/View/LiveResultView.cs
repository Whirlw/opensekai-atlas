using Sekai.Core.Live;
using LitMotion;
using UnityEngine;
using Sekai;

namespace Sekai.Live
{
	public class LiveResultView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _background;

		private bool _shouldPlayVoiceSE;
		private MotionHandle _backgroundMotion;
		private MotionHandle _resultDelay;

		public void Setup(bool shouldPlayVoiceSE)
		{
			_shouldPlayVoiceSE = shouldPlayVoiceSE;
		}

		public void Execute(LiveResultAnimationType liveResultAnimationType)
		{
			switch (liveResultAnimationType)
			{
				case LiveResultAnimationType.None:
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
			CancelMotions();
			AssetBundleUtility.UnloadAssetBundle(LiveConfig.ResultBundleName);
		}

		private void Fade(string prefabName)
		{
			if (_background != null)
			{
				_background.gameObject.SetActive(true);
				_backgroundMotion.TryCancel();
				Color from = _background.color;
				Color to = new Color(0f, 0f, 0f, 0.5f);
				_backgroundMotion = LMotion.Create(0f, 1f, 0.2f)
					.WithEase(Ease.OutCirc)
					.WithScheduler(LiveMotion.Scheduler())
					.Bind(progress => _background.color = Color.LerpUnclamped(from, to, progress));
			}

			_resultDelay.TryCancel();
			_resultDelay = LiveMotion.Delay(0.2f, () =>
			{
				GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.ResultBundleName, prefabName, false);
				if (prefab == null)
				{
					prefab = Resources.Load<GameObject>(prefabName);
				}
				if (prefab != null)
				{
					GameObject instance = Instantiate(prefab, transform);
					Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
					for (int i = 0; i < renderers.Length; i++)
					{
						renderers[i].sortingOrder += 220;
					}
				}
			});
		}

		private void OnDisable() => CancelMotions();

		private void CancelMotions()
		{
			_backgroundMotion.TryCancel();
			_resultDelay.TryCancel();
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
