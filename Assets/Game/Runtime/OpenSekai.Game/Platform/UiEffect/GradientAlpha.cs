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

		[UnityEngine.Serialization.FormerlySerializedAs("_alphaTop")]
		[SerializeField]
		[Range(0f, 1f)]
		private float alphaTopValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_alphaBottom")]
		[SerializeField]
		[Range(0f, 1f)]
		private float alphaBottomValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_alphaLeft")]
		[SerializeField]
		[Range(0f, 1f)]
		private float alphaLeftValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_alphaRight")]
		[SerializeField]
		[Range(0f, 1f)]
		private float alphaRightValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_gradientOffsetVertical")]
		[SerializeField]
		[Range(-1f, 1f)]
		private float gradientOffsetVerticalValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_gradientOffsetHorizontal")]
		[SerializeField]
		[Range(-1f, 1f)]
		private float gradientOffsetHorizontalValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_splitTextGradient")]
		[SerializeField]
		private bool splitTextGradientValue;

		public float alphaTop
		{
			get
			{
				return alphaTopValue;
			}
			set
			{
				SetAlpha(ref alphaTopValue, value);
			}
		}

		public float alphaBottom
		{
			get
			{
				return alphaBottomValue;
			}
			set
			{
				SetAlpha(ref alphaBottomValue, value);
			}
		}

		public float alphaLeft
		{
			get
			{
				return alphaLeftValue;
			}
			set
			{
				SetAlpha(ref alphaLeftValue, value);
			}
		}

		public float alphaRight
		{
			get
			{
				return alphaRightValue;
			}
			set
			{
				SetAlpha(ref alphaRightValue, value);
			}
		}

		public float gradientOffsetVertical
		{
			get
			{
				return gradientOffsetVerticalValue;
			}
			set
			{
				SetOffset(ref gradientOffsetVerticalValue, value);
			}
		}

		public float gradientOffsetHorizontal
		{
			get
			{
				return gradientOffsetHorizontalValue;
			}
			set
			{
				SetOffset(ref gradientOffsetHorizontalValue, value);
			}
		}

		public bool splitTextGradient
		{
			get
			{
				return splitTextGradientValue;
			}
			set
			{
				if (splitTextGradientValue == value)
				{
					return;
				}

				splitTextGradientValue = value;
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
				if (i == 0 || (splitTextGradientValue && i % ONE_TEXT_VERTEX == 0))
				{
					int endIndex = splitTextGradientValue ? i + ONE_TEXT_VERTEX : vList.Count;
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
				verticalRate = Mathf.Clamp01(verticalRate + gradientOffsetVerticalValue);

				float horizontalRate = 0f;
				if (width > 0f)
				{
					horizontalRate = (vertex.position.x - minX) / width;
				}
				horizontalRate = Mathf.Clamp01(horizontalRate + gradientOffsetHorizontalValue);

				Color32 color = vertex.color;
				float sourceAlpha = color.a / 255f;
				float verticalAlpha = alphaBottomValue + (alphaTopValue - alphaBottomValue) * verticalRate;
				float horizontalAlpha = alphaLeftValue + (alphaRightValue - alphaLeftValue) * horizontalRate;
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
			alphaTopValue = 1f;
			alphaBottomValue = 1f;
			alphaLeftValue = 1f;
			alphaRightValue = 1f;
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
