using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UiEffect
{
	[AddComponentMenu("UI/Effects/Gradient Alpha")]
	[RequireComponent(typeof(Graphic))]
	public class GradientAlpha : BaseMeshEffect
	{
		private const int ONE_TEXT_VERTEX = 6;

		[SerializeField]
		[Range(0f, 1f)]
		private float _alphaTop;

		[SerializeField]
		[Range(0f, 1f)]
		private float _alphaBottom;

		[SerializeField]
		[Range(0f, 1f)]
		private float _alphaLeft;

		[SerializeField]
		[Range(0f, 1f)]
		private float _alphaRight;

		[SerializeField]
		[Range(-1f, 1f)]
		private float _gradientOffsetVertical;

		[SerializeField]
		[Range(-1f, 1f)]
		private float _gradientOffsetHorizontal;

		[SerializeField]
		private bool _splitTextGradient;

		public float alphaTop
		{
			get
			{
				return _alphaTop;
			}
			set
			{
				SetAlpha(ref _alphaTop, value);
			}
		}

		public float alphaBottom
		{
			get
			{
				return _alphaBottom;
			}
			set
			{
				SetAlpha(ref _alphaBottom, value);
			}
		}

		public float alphaLeft
		{
			get
			{
				return _alphaLeft;
			}
			set
			{
				SetAlpha(ref _alphaLeft, value);
			}
		}

		public float alphaRight
		{
			get
			{
				return _alphaRight;
			}
			set
			{
				SetAlpha(ref _alphaRight, value);
			}
		}

		public float gradientOffsetVertical
		{
			get
			{
				return _gradientOffsetVertical;
			}
			set
			{
				SetOffset(ref _gradientOffsetVertical, value);
			}
		}

		public float gradientOffsetHorizontal
		{
			get
			{
				return _gradientOffsetHorizontal;
			}
			set
			{
				SetOffset(ref _gradientOffsetHorizontal, value);
			}
		}

		public bool splitTextGradient
		{
			get
			{
				return _splitTextGradient;
			}
			set
			{
				if (_splitTextGradient == value)
				{
					return;
				}

				_splitTextGradient = value;
				Refresh();
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			if (!IsActive() || vh == null)
			{
				return;
			}

			List<UIVertex> vertices = UiEffectListPool<UIVertex>.Get();
			vh.GetUIVertexStream(vertices);
			ModifyVertices(vertices);
			vh.Clear();
			vh.AddUIVertexTriangleStream(vertices);
			UiEffectListPool<UIVertex>.Release(vertices);
		}

		private void ModifyVertices(List<UIVertex> vList)
		{
			if (!IsActive() || vList == null || vList.Count < 1)
			{
				return;
			}

			float minX = 0f;
			float minY = 0f;
			float width = 0f;
			float height = 0f;
			for (int i = 0; i < vList.Count; i++)
			{
				if (i == 0 || (_splitTextGradient && i % ONE_TEXT_VERTEX == 0))
				{
					int endIndex = _splitTextGradient ? i + ONE_TEXT_VERTEX : vList.Count;
					UIVertex first = vList[i];
					minX = first.position.x;
					minY = first.position.y;
					float maxX = first.position.x;
					float maxY = first.position.y;
					for (int j = i; j < endIndex && j < vList.Count; j++)
					{
						Vector3 position = vList[j].position;
						if (minX >= position.x)
						{
							minX = position.x;
						}
						if (minY >= position.y)
						{
							minY = position.y;
						}
						if (maxX <= position.x)
						{
							maxX = position.x;
						}
						if (maxY <= position.y)
						{
							maxY = position.y;
						}
					}

					width = maxX - minX;
					height = maxY - minY;
				}

				UIVertex vertex = vList[i];
				float verticalRate = 0f;
				if (height > 0f)
				{
					verticalRate = (vertex.position.y - minY) / height;
				}
				verticalRate = Mathf.Clamp01(verticalRate + _gradientOffsetVertical);

				float horizontalRate = 0f;
				if (width > 0f)
				{
					horizontalRate = (vertex.position.x - minX) / width;
				}
				horizontalRate = Mathf.Clamp01(horizontalRate + _gradientOffsetHorizontal);

				Color32 color = vertex.color;
				float sourceAlpha = color.a / 255f;
				float verticalAlpha = _alphaBottom + (_alphaTop - _alphaBottom) * verticalRate;
				float horizontalAlpha = _alphaLeft + (_alphaRight - _alphaLeft) * horizontalRate;
				color.a = (byte)(Mathf.Clamp01(sourceAlpha * verticalAlpha * horizontalAlpha) * 255f);
				vertex.color = color;
				vList[i] = vertex;
			}
		}

		private void Refresh()
		{
			if (graphic != null)
			{
				graphic.SetVerticesDirty();
			}
		}

		public GradientAlpha()
		{
			_alphaTop = 1f;
			_alphaBottom = 1f;
			_alphaLeft = 1f;
			_alphaRight = 1f;
		}

		private void SetAlpha(ref float field, float value)
		{
			if (field == value)
			{
				return;
			}

			field = Mathf.Clamp01(value);
			Refresh();
		}

		private void SetOffset(ref float field, float value)
		{
			if (field == value)
			{
				return;
			}

			field = Mathf.Clamp(value, -1f, 1f);
			Refresh();
		}
	}
}
