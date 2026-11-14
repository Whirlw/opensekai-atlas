using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiCopyPass : ScriptableRenderPass
	{
		private readonly ProfilingSampler _profilingSampler;
		private RTHandle _source;
		private RTHandle _dest;

		public SekaiCopyPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiCopyPass));
			_profilingSampler = new ProfilingSampler(profilerTag);
			requiresIntermediateTexture = true;
		}

		public void Setup(RTHandle source, RTHandle dest)
		{
			_source = source;
			_dest = dest;
		}

		private class PassData
		{
			public TextureHandle source;
			public TextureHandle destination;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (_dest == null)
			{
				return;
			}

			using (var builder = renderGraph.AddUnsafePass<PassData>(nameof(SekaiCopyPass), out var passData, _profilingSampler))
			{
				passData.source = _source == null
					? frameData.Get<UniversalResourceData>().activeColorTexture
					: renderGraph.ImportTexture(_source);
				passData.destination = renderGraph.ImportTexture(_dest);
				builder.UseTexture(passData.source, AccessFlags.Read);
				builder.UseTexture(passData.destination, AccessFlags.Write);
				builder.AllowGlobalStateModification(true);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
				{
					Blitter.BlitCameraTexture(CommandBufferHelpers.GetNativeCommandBuffer(context.cmd), data.source, data.destination);
				});
			}
		}
	}
}
