using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	internal static class NextRushPalette
	{
		private static readonly int _property = Shader.PropertyToID("_NextRushPalette");

		internal static void Apply(Renderer[] renderers, int style, ref int previous, ref MaterialPropertyBlock block)
		{
			if (previous == style || renderers == null) return;
			previous = style;
			block ??= new MaterialPropertyBlock();
			Color color = style == 0 ? Color.clear : style == 1 ? Color.gray : NextRushConnectorSegment.PaletteColor(style);
			foreach (var renderer in renderers)
			{
				if (renderer == null) continue;
				renderer.GetPropertyBlock(block);
				block.SetColor(_property, color);
				renderer.SetPropertyBlock(block);
			}
		}
	}
}
