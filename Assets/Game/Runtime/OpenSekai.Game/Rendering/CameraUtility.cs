using UnityEngine;

namespace Sekai.Core
{
	public class CameraUtility
	{
		private const string FRONT_CAMERA_TAG = "FrontCamera";

		private static readonly string _pvPath;

		private static readonly string _renderCanvasPath;

		private static readonly string _mainCameraVLPath;

		private static readonly string _mainCameraMVPath;

		private static readonly string _subCameraPath;

		public static GameObject CreateRenderCanvas(Transform parent, bool worldPoitionStays = false)
		{
			return LoadObject(_renderCanvasPath, parent, worldPoitionStays);
		}

		public static GameObject CreateMVMainCamera(Transform parent, bool worldPoitionStays = false)
		{
			return CreateMainCamera(_mainCameraMVPath, parent, worldPoitionStays);
		}

		public static GameObject CreateVLMainCamera(Transform parent, bool worldPoitionStays = false)
		{
			return CreateMainCamera(_mainCameraVLPath, parent, worldPoitionStays);
		}

		private static GameObject CreateMainCamera(string path, Transform parent, bool worldPoitionStays = false)
		{
			return LoadObject(path, parent, worldPoitionStays);
		}

		public static GameObject CreateSubCamera(Transform parent, bool worldPoitionStays = false)
		{
			return LoadObject(_subCameraPath, parent, worldPoitionStays);
		}

		private static GameObject LoadObject(string path, Transform parent, bool worldPoitionStays = false)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}

			GameObject prefab = Resources.Load<GameObject>(path);
			return prefab != null ? Object.Instantiate(prefab, parent, worldPoitionStays) : null;
		}

		public static Camera GetFrontCamera()
		{
			GameObject frontCamera = GameObject.FindGameObjectWithTag(FRONT_CAMERA_TAG);
			return frontCamera != null ? frontCamera.GetComponent<Camera>() : null;
		}

		public CameraUtility()
		{
		}

		static CameraUtility()
		{
			_pvPath = "live/camera/pv";
			_renderCanvasPath = "live/camera/RenderCanvas";
			_mainCameraVLPath = "live/camera/MainCameraVL";
			_mainCameraMVPath = "live/camera/MainCameraMV";
			_subCameraPath = "live/camera/SubCamera";
		}
	}
}
