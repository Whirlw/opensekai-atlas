using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UiEffect
{
	[AddComponentMenu("UI/Effects/Gradient Color")]
	[RequireComponent(typeof(Graphic))]
	public class GradientColor : BaseMeshEffect
	{
		private const int ONE_TEXT_VERTEX = 6;

		[UnityEngine.Serialization.FormerlySerializedAs("_colorTop")]
		[SerializeField]
		private Color colorTopValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_colorBottom")]
		[SerializeField]
		private Color colorBottomValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_colorLeft")]
		[SerializeField]
		private Color colorLeftValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_colorRight")]
		[SerializeField]
		private Color colorRightValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_gradientOffsetVertical")]
		[Range(-1f, 1f)]
		[SerializeField]
		private float gradientOffsetVerticalValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_gradientOffsetHorizontal")]
		[Range(-1f, 1f)]
		[SerializeField]
		private float gradientOffsetHorizontalValue;

		[UnityEngine.Serialization.FormerlySerializedAs("_splitTextGradient")]
		[SerializeField]
		private bool splitTextGradientValue;

		public Color colorTop
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public Color colorBottom
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public Color colorLeft
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public Color colorRight
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public float gradientOffsetVertical
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public float gradientOffsetHorizontal
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public bool splitTextGradient
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			throw null;
		}

		private void ModifyVertices(List<UIVertex> vList)
		{
			throw null;
		}

		private void Refresh()
		{
			throw null;
		}

		public GradientColor()
		{
			throw null;
		}
	}
}
