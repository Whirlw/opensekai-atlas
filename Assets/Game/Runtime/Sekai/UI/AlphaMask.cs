using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace Sekai.UI
{
	[ExecuteInEditMode]
	public class AlphaMask : MonoBehaviour
	{
		[SerializeField]
		private Image _maskImage;

		[SerializeField]
		private bool _unmask;

		private Canvas _canvas;

		private Graphic[] _targetGraphics;

		private Material _maskMaterial;

		private RectTransform _cachedRectTransform;

		private Material MaskMaterial
		{
			get
			{
				if (_maskMaterial == null)
				{
					Shader shader = Shader.Find("Sekai/UI/AlphaMask");
					if (shader != null)
					{
						_maskMaterial = new Material(shader);
					}
				}
				return _maskMaterial;
			}
		}

		private Image MaskImage
		{
			get
			{
				if (_maskImage == null)
				{
					_maskImage = GetComponent<Image>();
				}
				return _maskImage;
			}
		}

		private RectTransform RectTransform
		{
			get
			{
				if (_cachedRectTransform == null)
				{
					_cachedRectTransform = transform as RectTransform;
				}
				return _cachedRectTransform;
			}
		}

		private Canvas RootCanvas
		{
			get
			{
				if (_canvas == null)
				{
					Canvas parentCanvas = GetComponentInParent<Canvas>();
					_canvas = parentCanvas != null && parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas : parentCanvas;
				}
				return _canvas;
			}
		}

		private void SetMaterial(Graphic[] graphics)
		{
			if (graphics == null)
			{
				return;
			}
			Material material = MaskMaterial;
			for (int i = 0; i < graphics.Length; i++)
			{
				if (graphics[i] != null)
				{
					graphics[i].material = material;
				}
			}
		}

		private bool IsValid()
		{
			Canvas rootCanvas = RootCanvas;
			if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				return false;
			}
			Image image = MaskImage;
			return image != null && image.sprite != null;
		}

		private void UpdateMaskParameter()
		{
			Canvas rootCanvas = RootCanvas;
			Camera camera = rootCanvas != null ? rootCanvas.worldCamera : null;
			if (camera == null || MaskMaterial == null || MaskImage == null || MaskImage.sprite == null)
			{
				return;
			}
			MaskMaterial.SetTexture("_MaskTex", MaskImage.sprite.texture);
			MaskMaterial.SetMatrix("_MaskMatrix", CalculateMatrix(camera));
			MaskMaterial.SetFloat("_Unmask", _unmask ? 1f : 0f);
		}

		private Matrix4x4 CalculateMatrix(Camera camera)
		{
			Rect viewportRect = CalcurateViewportRect(camera);
			Matrix4x4 projection = Matrix4x4.Ortho(
				viewportRect.xMin,
				viewportRect.xMax,
				viewportRect.yMin,
				viewportRect.yMax,
				camera.nearClipPlane,
				camera.farClipPlane);
			Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(projection, false);
			Matrix4x4 remap = Matrix4x4.TRS(Vector3.one * 0.5f, Quaternion.identity, Vector3.one * 0.5f);
			return remap * gpuProjection * camera.worldToCameraMatrix;
		}

		private Rect CalcurateViewportRect(Camera camera)
		{
			RectTransform rect = RectTransform;
			if (rect == null || camera == null)
			{
				return new Rect(-1f, -1f, 2f, 2f);
			}
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			Vector2 leftTop = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
			Vector2 rightBottom = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
			float screenHalfWidth = Screen.width * 0.5f;
			float screenHalfHeight = Screen.height * 0.5f;
			float worldHalfHeight = camera.orthographicSize;
			float worldHalfWidth = worldHalfHeight * Screen.width / Mathf.Max(1f, Screen.height);
			return new Rect(
				(leftTop.x - screenHalfWidth) / screenHalfWidth * worldHalfWidth,
				(rightBottom.y - screenHalfHeight) / screenHalfHeight * worldHalfHeight,
				(rightBottom.x - leftTop.x) / screenHalfWidth * worldHalfWidth,
				(leftTop.y - rightBottom.y) / screenHalfHeight * worldHalfHeight);
		}

		private void GetMinMaxCorners(Camera camera, RectTransform root, ref Vector3[] corners, ref Vector2 leftTop, ref Vector2 rightBottom)
		{
			if (root == null)
			{
				return;
			}
			for (int i = 0; i < root.childCount; i++)
			{
				GetMinMaxCorners(camera, root.GetChild(i) as RectTransform, ref corners, ref leftTop, ref rightBottom);
			}
			root.GetWorldCorners(corners);
			Vector2 currentLeftTop = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
			Vector2 currentRightBottom = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
			leftTop.x = Mathf.Min(leftTop.x, currentLeftTop.x);
			leftTop.y = Mathf.Max(leftTop.y, currentLeftTop.y);
			rightBottom.x = Mathf.Max(rightBottom.x, currentRightBottom.x);
			rightBottom.y = Mathf.Min(rightBottom.y, currentRightBottom.y);
		}

		private void OnEnable()
		{
			_targetGraphics = null;
		}

		private void OnDisable()
		{
			if (_targetGraphics != null)
			{
				for (int i = 0; i < _targetGraphics.Length; i++)
				{
					if (_targetGraphics[i] != null)
					{
						_targetGraphics[i].material = null;
					}
				}
			}
			_targetGraphics = null;
		}

		private void Update()
		{
			if (!IsValid())
			{
				return;
			}
			if (_targetGraphics == null || _targetGraphics.Length == 0)
			{
				Transform ownTransform = transform;
				_targetGraphics = GetComponentsInChildren<Graphic>(true)
					.Where(graphic => graphic != null && graphic.transform != ownTransform)
					.ToArray();
				SetMaterial(_targetGraphics);
			}
			UpdateMaskParameter();
		}

		public AlphaMask()
		{
		}
	}
}
