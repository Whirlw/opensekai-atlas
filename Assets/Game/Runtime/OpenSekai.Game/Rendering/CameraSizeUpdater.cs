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

		[UnityEngine.Serialization.FormerlySerializedAs("_baseWidth")]
		[SerializeField]
		private float baseWidth;

		[UnityEngine.Serialization.FormerlySerializedAs("_baseHeight")]
		[SerializeField]
		private float baseHeight;

		[UnityEngine.Serialization.FormerlySerializedAs("_pixelPerUnit")]
		[SerializeField]
		private float pixelPerUnit;

		[UnityEngine.Serialization.FormerlySerializedAs("_baseType")]
		[SerializeField]
		private BaseType baseType;

		[UnityEngine.Serialization.FormerlySerializedAs("_camera")]
		[SerializeField]
		private Camera camera;

		public float OrthographicSize
		{
			get
			{
				return camera != null ? camera.orthographicSize : 0f;
			}
		}

		public float BaseWidth
		{
			get
			{
				return baseWidth;
			}
		}

		public float BaseHeight
		{
			get
			{
				return baseHeight;
			}
		}

		public float BaseOrthographicSize
		{
			get
			{
				return baseHeight / pixelPerUnit * 0.5f;
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
			if (camera == null)
			{
				camera = GetComponent<Camera>();
			}
			if (camera == null || pixelPerUnit == 0f || baseWidth == 0f || baseHeight == 0f)
			{
				return;
			}

			float size = BaseOrthographicSize;
			float screenAspect = (float)Screen.height / Screen.width;
			float baseAspect = baseHeight / baseWidth;
			if (baseType != BaseType.Height && (baseAspect <= screenAspect || baseType == BaseType.Width))
			{
				size *= screenAspect / baseAspect;
			}
			camera.orthographicSize = size;
		}

		public CameraSizeUpdater()
		{
			baseWidth = 1920f;
			baseHeight = 1080f;
			pixelPerUnit = 108f;
		}
	}
}
