using UnityEngine;

namespace Sekai.Core
{
	public class BaseController : MonoBehaviour
	{
		private bool _isExit;
		private bool _isPause;
		private bool _isQuitting;

		protected bool IsExit => _isExit;

		protected bool IsPause => _isPause;

		protected virtual void Awake()
		{
			OnAwake();
		}

		protected virtual void LateUpdate()
		{
			OnUpdate();
		}

		protected virtual void OnDestroy()
		{
			Destroy();
		}

		protected virtual void OnApplicationQuit()
		{
			_isQuitting = Application.isEditor;
		}

		protected virtual void OnApplicationFocus(bool hasFocus)
		{
			SetSystemPause(!hasFocus);
		}

		protected virtual void OnApplicationPause(bool pauseStatus)
		{
			SetSystemPause(pauseStatus);
		}

		public void Exit()
		{
			if (_isExit)
			{
				return;
			}

			_isExit = true;
			OnExit();
		}

		public void CancelExit()
		{
			_isExit = false;
		}

		public void Destroy()
		{
			if (!_isQuitting)
			{
				Exit();
			}
		}

		protected virtual void OnSystemPause()
		{
		}

		protected virtual void OnAwake()
		{
		}

		protected virtual void OnUpdate()
		{
		}

		protected virtual void OnExit()
		{
		}

		protected virtual void OnRenderGUI()
		{
		}

		protected virtual void OnEnable()
		{
		}

		protected virtual void OnDisable()
		{
		}

		private void SetSystemPause(bool value)
		{
			if (_isPause == value)
			{
				return;
			}

			_isPause = value;
			OnSystemPause();
		}
	}
}
