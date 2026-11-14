using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CriWare;

namespace Sekai
{
	public class LiveTransitioner : MonoBehaviour
	{
		public enum TransitionType
		{
			Live = 0,
			VirtualArea = 1,
			ForceWhite = 2,
			White = 3,
			MusicScoreMakerLoading = 4
		}

		private const string PREFAB_PATH = "Common/Transition/LiveTransitioner";
		private const string DEFAULT_TRANSITION_SE = "SE_AREA_TRANSITION_SEKAI";
		private const float DEFAULT_FADE_DELAY = 1f;
		private const float DEFAULT_FADE_DURATION = 1f;

		private static LiveTransitioner _instance;

		[SerializeField]
		private RectTransform _contentRoot;

		[SerializeField]
		private ColorFader _coverFader;

		[SerializeField]
		private GameObject _effectPrefab;

		[SerializeField]
		private RectTransform _effectEmitRoot;

		[SerializeField]
		private Canvas _canvas;

		[SerializeField]
		private GameObject _loadingContent;

		private ParticleSystem _startParticle;
		private Coroutine _destroyCoroutine;
		private Func<float, UniTask> _onTransitionBGMStop;

		public static LiveTransitioner Instance => _instance;

		public static bool Exists => _instance != null;

		public GameObject ContentObject => _loadingContent;

		public static LiveTransitioner Create()
		{
			var prefab = ResourceCatalog.LiveTransitioner.Load();
			if (prefab == null)
			{
				Debug.LogWarningFormat("LiveTransitioner prefab could not be loaded. path:{0}", PREFAB_PATH);
				return null;
			}

			var transitioner = Instantiate(prefab);
			transitioner.name = prefab.name;
			return transitioner.GetComponent<LiveTransitioner>();
		}

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(_instance.gameObject);
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);
			ShowLoadingContent(false);
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}

		public static void SafeFinish(Action onFinished)
		{
			SafeFinish(onFinished, null);
		}

		public static void SafeFinish(Action onFinished, Action onFinishFadeBGM)
		{
			if (_instance == null)
			{
				return;
			}

			_instance.Finish(onFinished, onFinishFadeBGM);
		}

		public static void SafeForceFinish(Action onFinished)
		{
			if (_instance == null)
			{
				return;
			}

			_instance.ForceFinish(onFinished);
		}

		public void Play(Action onFinished)
		{
			Play(onFinished, DEFAULT_TRANSITION_SE, false, 0f);
		}

		public void Play(Action onFinished, string seName, bool showsLoading, float timeout)
		{
			Play(onFinished, seName, showsLoading, timeout, DEFAULT_FADE_DELAY, DEFAULT_FADE_DURATION);
		}

		/// <summary>Same as the four argument overload but with an explicit fade schedule, so callers
		/// that already show their own cover can skip the one second wait.</summary>
		public void Play(Action onFinished, string seName, bool showsLoading, float timeout, float delay, float duration)
		{
			PlayCore(onFinished, seName, showsLoading, timeout, delay, duration);
		}

		public void Play(TransitionType type, Action onFinished, string seName = DEFAULT_TRANSITION_SE, bool showsLoading = false, float timeout = 0f, float delay = DEFAULT_FADE_DELAY, float duration = DEFAULT_FADE_DURATION)
		{
			switch (type)
			{
				case TransitionType.ForceWhite:
					PlayForceWhite(onFinished, seName, showsLoading, timeout);
					break;
				case TransitionType.White:
					PlayWhiteOut(onFinished, showsLoading, timeout, delay, duration);
					break;
				default:
					PlayCore(onFinished, seName, showsLoading, timeout, delay, duration);
					break;
			}
		}

		private void PlayCore(Action onFinished, string seName, bool showsLoading, float timeout, float delay, float duration)
		{
			gameObject.SetActive(true);
			ShowLoadingContent(false);

			if (_coverFader != null)
			{
				_coverFader.gameObject.SetActive(true);
				_coverFader.Set(ColorUtility.WHITE_ALPHA_0);
			}

			if (_effectPrefab != null && _effectEmitRoot != null)
			{
				var effect = Instantiate(_effectPrefab, _effectEmitRoot);
				_startParticle = effect.transform.childCount > 0
					? effect.transform.GetChild(0).GetComponent<ParticleSystem>()
					: effect.GetComponentInChildren<ParticleSystem>(true);
				if (_startParticle != null)
				{
					_startParticle.Play(true);
				}
			}

			if (!string.IsNullOrEmpty(seName))
			{
				SoundManager.Instance.PlaySEOneShot(seName);
			}

			_coverFader?.Stop();
			Action finish = () =>
			{
				if (_startParticle != null)
				{
					_startParticle.Pause(true);
				}

				onFinished?.Invoke();
				ShowLoadingContent(showsLoading);
			};

			if (_coverFader != null)
			{
				_coverFader.Play(ColorUtility.WHITE_ALPHA_1, delay, duration, finish);
			}
			else
			{
				StartCoroutine(InvokeNextFrame(finish));
			}

			if (timeout > 0f)
			{
				StopDestroyCoroutine();
				_destroyCoroutine = StartCoroutine(DestroyAfter(timeout));
			}
		}

		private void PlayForceWhite(Action onFinished, string seName = DEFAULT_TRANSITION_SE, bool showsLoading = false, float timeout = 0f)
		{
			gameObject.SetActive(true);
			if (_coverFader != null)
			{
				_coverFader.gameObject.SetActive(true);
				_coverFader.Set(ColorUtility.WHITE_ALPHA_1);
			}

			if (!string.IsNullOrEmpty(seName))
			{
				SoundManager.Instance.PlaySEOneShot(seName);
			}

			onFinished?.Invoke();
			ShowLoadingContent(showsLoading);
			if (timeout > 0f)
			{
				StopDestroyCoroutine();
				_destroyCoroutine = StartCoroutine(DestroyAfter(timeout));
			}
		}

		public void PlayWhiteOut(Action onFinished, bool showsLoading = false, float timeout = 0f, float delay = DEFAULT_FADE_DELAY, float duration = DEFAULT_FADE_DURATION)
		{
			gameObject.SetActive(true);
			ShowLoadingContent(false);
			if (_coverFader != null)
			{
				_coverFader.gameObject.SetActive(true);
				_coverFader.Set(ColorUtility.WHITE_ALPHA_0);
				_coverFader.Play(ColorUtility.WHITE_ALPHA_1, delay, duration, () =>
				{
					onFinished?.Invoke();
					ShowLoadingContent(showsLoading);
				});
			}
			else
			{
				StartCoroutine(InvokeNextFrame(onFinished));
			}

			if (timeout > 0f)
			{
				StopDestroyCoroutine();
				_destroyCoroutine = StartCoroutine(DestroyAfter(timeout));
			}
		}

		public void Finish(Action onFinished = null, Action onFinishFadeBGM = null)
		{
			StopDestroyCoroutine();
			if (_loadingContent != null)
			{
				_loadingContent.SetActive(false);
			}

			if (_coverFader != null)
			{
				_coverFader.Stop();
				_coverFader.Play(ColorUtility.WHITE_ALPHA_0, 1f, 1f, onFinished);
			}
			else
			{
				onFinished?.Invoke();
			}

			ResumePausedStartParticle();
			ScheduleDestroy(5f);
		}

		public void ForceFinish(Action onFinished = null)
		{
			StopDestroyCoroutine();
			if (_loadingContent != null)
			{
				_loadingContent.SetActive(false);
			}

			if (_coverFader != null)
			{
				_coverFader.Stop();
				_coverFader.Set(ColorUtility.WHITE_ALPHA_0);
			}

			ResumePausedStartParticle();
			DestroySelf();
			onFinished?.Invoke();
		}

		public void ShowLoadingContent(bool visible)
		{
			if (_loadingContent != null)
			{
				_loadingContent.SetActive(visible);
			}
		}

		private void StopDestroyCoroutine()
		{
			if (_destroyCoroutine == null)
			{
				return;
			}

			StopCoroutine(_destroyCoroutine);
			_destroyCoroutine = null;
		}

		private IEnumerator InvokeNextFrame(Action action)
		{
			yield return null;
			action?.Invoke();
		}

		private IEnumerator DestroyAfter(float timeout)
		{
			yield return new WaitForSeconds(timeout);
			_destroyCoroutine = null;
			ForceFinish(null);
		}

		private void ScheduleDestroy(float delay)
		{
			StopDestroyCoroutine();
			_destroyCoroutine = StartCoroutine(DestroySelfAfter(delay));
		}

		private IEnumerator DestroySelfAfter(float delay)
		{
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}
			else
			{
				yield return null;
			}

			_destroyCoroutine = null;
			DestroySelf();
		}

		private void DestroySelf()
		{
			if (_instance == this)
			{
				_instance = null;
			}

			Destroy(gameObject);
		}

		private void ResumePausedStartParticle()
		{
			if (_startParticle != null && _startParticle.isPaused && _startParticle.IsAlive(true))
			{
				_startParticle.Play(true);
			}
		}

	}
}
