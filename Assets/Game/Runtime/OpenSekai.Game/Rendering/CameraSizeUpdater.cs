using UnityEngine;

namespace Sekai
{
	public class CameraSizeUpdater : MonoBehaviour
	{
		private enum BaseType
		{
			Both = 0,
			Width = 1,
			Height = 2
		}

		[SerializeField]
		private float _baseWidth;

		[SerializeField]
		private float _baseHeight;

		[SerializeField]
		private float _pixelPerUnit;

		[SerializeField]
		private BaseType _baseType;

		[SerializeField]
		private Camera _camera;

		public float OrthographicSize
		{
			get
			{
				return _camera != null ? _camera.orthographicSize : 0f;
			}
		}

		public float BaseWidth
		{
			get
			{
				return _baseWidth;
			}
		}

		public float BaseHeight
		{
			get
			{
				return _baseHeight;
			}
		}

		public float BaseOrthographicSize
		{
			get
			{
				return _baseHeight / _pixelPerUnit * 0.5f;
			}
		}

		private void Awake()
		{
			UpdateOrthographicSize();
		}

		public void ForceUpdate()
		{
			UpdateOrthographicSize();
		}

		private void UpdateOrthographicSize()
		{
			if (_camera == null)
			{
				_camera = GetComponent<Camera>();
			}
			if (_camera == null || _pixelPerUnit == 0f || _baseWidth == 0f || _baseHeight == 0f)
			{
				return;
			}

			float size = BaseOrthographicSize;
			float screenAspect = (float)Screen.height / Screen.width;
			float baseAspect = _baseHeight / _baseWidth;
			if (_baseType != BaseType.Height && (baseAspect <= screenAspect || _baseType == BaseType.Width))
			{
				size *= screenAspect / baseAspect;
			}
			_camera.orthographicSize = size;
		}

		public CameraSizeUpdater()
		{
			_baseWidth = 1920f;
			_baseHeight = 1080f;
			_pixelPerUnit = 108f;
		}
	}
}
