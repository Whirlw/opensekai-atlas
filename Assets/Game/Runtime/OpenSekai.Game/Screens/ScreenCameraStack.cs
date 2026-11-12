using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai
{
	public class ScreenCameraStack
	{
		private const int UI_RENDERER_INDEX = 2;

		private class StackCameraLayer
		{
						private Camera _Camera;
			public Camera Camera
			{
			    get => _Camera;
			    set => _Camera = value;
			}
						private UniversalAdditionalCameraData _CameraData;
			public UniversalAdditionalCameraData CameraData
			{
			    get => _CameraData;
			    set => _CameraData = value;
			}
						private int _Priority;
			public int Priority
			{
			    get => _Priority;
			    set => _Priority = value;
			}

			public StackCameraLayer(Camera camera, UniversalAdditionalCameraData cameraData, int priority)
			{
				Camera = camera;
				CameraData = cameraData;
				Priority = priority;
			}
		}

		private Camera _baseCamera;
		private UniversalAdditionalCameraData _baseCameraData;
		private readonly List<StackCameraLayer> _cameraStackList = new List<StackCameraLayer>();

		public Camera BaseCamera => _baseCamera;

		public Camera CreateEmptyBaseCamera(Transform parent)
		{
			var cameraObject = new GameObject("BaseCamera");
			if (parent != null)
			{
				cameraObject.transform.SetParent(parent, false);
			}

			var camera = cameraObject.AddComponent<Camera>();
			camera.backgroundColor = Color.black;
			camera.clearFlags = CameraClearFlags.Color;
			camera.cullingMask = 0;
			camera.orthographic = true;
			camera.depth = -100f;
			camera.useOcclusionCulling = false;
			GetOrAddCameraData(camera)?.SetRenderer(UI_RENDERER_INDEX);
			SetBaseCamera(camera);
			return camera;
		}

		public void AddToStack(Camera camera, int priority)
		{
			if (camera == null)
			{
				return;
			}

			var cameraData = GetOrAddCameraData(camera);
			if (cameraData != null)
			{
				cameraData.renderType = CameraRenderType.Overlay;
			}

			_cameraStackList.RemoveAll(x => x.Camera == camera);
			_cameraStackList.Add(new StackCameraLayer(camera, cameraData, priority));
			_cameraStackList.Sort((a, b) => a.Priority.CompareTo(b.Priority));
			StackToBaseCamera();
		}

		public void RemoveFromStack(Camera camera)
		{
			if (camera == null)
			{
				return;
			}

			_cameraStackList.RemoveAll(x => x.Camera == null || x.Camera == camera);
			if (_baseCameraData != null)
			{
				_baseCameraData.cameraStack.RemoveAll(x => x == null || x == camera);
			}
			if (_cameraStackList.Count > 0)
			{
				StackToBaseCamera();
			}
			else
			{
				SetupSekaiCameraData();
			}
		}

		public void SetBaseCamera(Camera camera)
		{
			_baseCamera = camera;
			_baseCameraData = GetOrAddCameraData(camera);
			if (_baseCameraData != null)
			{
				_baseCameraData.renderType = CameraRenderType.Base;
			}

			StackToBaseCamera();
		}

		private static UniversalAdditionalCameraData GetOrAddCameraData(Camera camera)
		{
			if (camera == null)
			{
				return null;
			}

			return camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
		}

		private void StackToBaseCamera()
		{
			if (_baseCameraData == null)
			{
				return;
			}

			_cameraStackList.RemoveAll(x => x.Camera == null);
			if (_cameraStackList.Count == 0)
			{
				SetupSekaiCameraData();
				return;
			}

			_baseCameraData.cameraStack.Clear();
			foreach (var layer in _cameraStackList)
			{
				if (layer.Camera != null && layer.Camera != _baseCamera)
				{
					_baseCameraData.cameraStack.Add(layer.Camera);
				}
			}

			SetupSekaiCameraData();
		}

		private void SetupSekaiCameraData()
		{
			if (_baseCamera == null || _baseCameraData == null)
			{
				return;
			}

			var cameraCount = _baseCameraData.cameraStack.Count + 1;
			_baseCamera.GetSekaiAdditionalCameraData()?.Setup(0, cameraCount);
			for (var i = 0; i < _baseCameraData.cameraStack.Count; i++)
			{
				_baseCameraData.cameraStack[i]?.GetSekaiAdditionalCameraData()?.Setup(i + 1, cameraCount);
			}
		}
	}
}
