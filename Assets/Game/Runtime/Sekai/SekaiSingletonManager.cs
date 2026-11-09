using CP;
using UnityEngine;
using UnityEngine.EventSystems;
using CriWare;

namespace Sekai
{
	public class SekaiSingletonManager : MonoBehaviour
	{
		private const string EVENT_SYSTEM_PREFAB_PATH = "Common/Input/EventSystem";

		private static SekaiSingletonManager _instance;
		private static float _deltaK = 1f;

		private EventSystem _eventSystem;

		public static float DeltaTimeK => _deltaK;

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);
			OnInitialize();
		}

		protected virtual void OnInitialize()
		{
			FramerateUtility.SetFrameRate();
			EnsureEventSystem();
			TextMeshProUtility.SetupBuiltinFontAsset();
			AssetBundleMetaManager.Instance.Initialize();
			AssetBundleManager.Instance.Initialize();
			SoundUtility.SetupGlobalSeSettings(GlobalSeSettings.Normal);
		}

		private void Update()
		{
			_deltaK = Time.timeScale * Time.deltaTime * Application.targetFrameRate;
		}

		public virtual void OnFinalize()
		{
			if (_eventSystem != null)
			{
				Destroy(_eventSystem.gameObject);
				_eventSystem = null;
			}
		}

		private void OnDestroy()
		{
			if (_instance != this)
			{
				return;
			}

			OnFinalize();
			_instance = null;
		}

		private void OnApplicationQuit()
		{
			_instance = null;
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (!pauseStatus)
			{
				System.GC.Collect();
			}
		}

		private void EnsureEventSystem()
		{
			_eventSystem = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
			if (_eventSystem != null)
			{
				NativeInput.ConfigureEventSystem(_eventSystem);
				return;
			}

			GameObject prefab = Resources.Load<GameObject>(EVENT_SYSTEM_PREFAB_PATH);
			GameObject eventSystemObject = prefab != null
				? Instantiate(prefab)
				: new GameObject("EventSystem", typeof(EventSystem));

			eventSystemObject.name = prefab != null ? prefab.name : "EventSystem";
			DontDestroyOnLoad(eventSystemObject);
			_eventSystem = eventSystemObject.GetComponent<EventSystem>();
			NativeInput.ConfigureEventSystem(_eventSystem);
		}
	}
}
