using Sekai.Core;
using UnityEngine;

namespace Sekai
{
	public class CameraFOVUpdater : MonoBehaviour
	{
		[SerializeField]
		private Camera _camera;

		private void Awake()
		{
			if (_camera == null)
			{
				_camera = GetComponent<Camera>();
			}

			UpdateFOVSize();
		}

		private void UpdateFOVSize()
		{
			if (_camera == null)
			{
				return;
			}

			_camera.fieldOfView = SekaiCameraAspect.CalculateVerticalFov(_camera.fieldOfView);
		}

		public CameraFOVUpdater()
		{
		}
	}
}
