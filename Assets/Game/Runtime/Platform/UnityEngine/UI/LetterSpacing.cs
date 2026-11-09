using System.Collections;
using System.Collections.Generic;

namespace UnityEngine.UI
{
	[AddComponentMenu("UI/Effects/Letter Spacing", 15)]
	public class LetterSpacing : BaseMeshEffect
	{
		private const string SUPPORTED_TAG_REGEX_PATTERSN = "<b>|</b>|<i>|</i>|<size=.*?>|</size>|<color=.*?>|</color>|<material=.*?>|</material>";

		[SerializeField]
		private bool _useRichText;

		[SerializeField]
		private float _spacing;

		public float spacing
		{
			get
			{
				return _spacing;
			}
			set
			{
				if (!Mathf.Approximately(_spacing, value))
				{
					_spacing = value;
					if (graphic != null)
					{
						graphic.SetVerticesDirty();
					}
				}
			}
		}

		protected LetterSpacing()
		{
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			if (!IsActive() || vh == null)
			{
				return;
			}
			List<UIVertex> verts = new List<UIVertex>();
			vh.GetUIVertexStream(verts);
			ModifyVertices(verts);
			vh.Clear();
			vh.AddUIVertexTriangleStream(verts);
		}

		public void ModifyVertices(List<UIVertex> verts)
		{
			if (verts == null || verts.Count == 0 || Mathf.Approximately(_spacing, 0f))
			{
				return;
			}
			float offset = _spacing;
			for (int i = 0; i < verts.Count; i += 6)
			{
				float advance = offset * (i / 6);
				for (int j = 0; j < 6 && i + j < verts.Count; j++)
				{
					UIVertex vertex = verts[i + j];
					vertex.position.x += advance;
					verts[i + j] = vertex;
				}
			}
		}

		private IEnumerator GetRegexMatchedTagCollection(string line, out int lineLengthWithoutTags)
		{
			lineLengthWithoutTags = string.IsNullOrEmpty(line) ? 0 : line.Length;
			return System.Array.Empty<object>().GetEnumerator();
		}
	}
}
