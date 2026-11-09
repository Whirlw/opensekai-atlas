using UnityEngine;

namespace CP
{
	public class SingletonMonoBehaviour<T> : MonoBehaviour, ISingletonMonoBehaviour where T : SingletonMonoBehaviour<T>
	{
		private static T _instance;

		public static T Instance
		{
			get
			{
				if (_instance != null)
				{
					return _instance;
				}

				_instance = Object.FindFirstObjectByType<T>();
				if (_instance != null)
				{
					_instance.SetupInstance(_instance);
					return _instance;
				}

				GameObject gameObject = new GameObject(typeof(T).Name);
				_instance = gameObject.AddComponent<T>();
				_instance.SetupInstance(_instance);
				return _instance;
			}
		}

		public static bool ExistsInstance
		{
			get
			{
				return _instance != null;
			}
		}

		public MonoBehaviour MonoBehaviour
		{
			get
			{
				return this;
			}
		}

		protected virtual void OnInitialize()
		{
		}

		public virtual void OnFinalize()
		{
		}

		public virtual bool IsDontDestroy()
		{
			return true;
		}

		public void SetupInstance()
		{
			SetupInstance((T)this);
		}

		private void SetupInstance(T target)
		{
			if (target == null)
			{
				return;
			}

			if (_instance != null && _instance != target)
			{
				Destroy(target.gameObject);
				return;
			}

			Initialize(target);
		}

		private void Initialize(T target)
		{
			_instance = target;
			if (target.IsDontDestroy())
			{
				DontDestroyOnLoad(target.gameObject);
			}
			target.OnInitialize();
		}

		private void Destroyed(T target)
		{
			if (_instance != target)
			{
				return;
			}

			target.OnFinalize();
			_instance = null;
		}

		private void Awake()
		{
			SetupInstance((T)this);
		}

		protected virtual void OnDestroy()
		{
			Destroyed((T)this);
		}

		private void OnApplicationQuit()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}

		public SingletonMonoBehaviour()
		{
			}
	}
}
