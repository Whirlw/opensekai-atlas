using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiUIBlurSetupPass : ScriptableRenderPass
	{
		private SekaiUIBuffer _uIBuffer;
		private RenderTextureDescriptor _colorDescriptor;
		private RenderTextureDescriptor _depthDescriptor;

		public void Setup(SekaiUIBuffer uiBuffer, RenderTextureDescriptor colorDescriptor, RenderTextureDescriptor depthDescriptor)
		{
			_uIBuffer = uiBuffer;
			_colorDescriptor = colorDescriptor;
			_depthDescriptor = depthDescriptor;
			_uIBuffer?.TryGetRTHandle(_colorDescriptor, _depthDescriptor);
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			// Setup already allocates the persistent UI buffers before dependent passes record.
		}

		public void Dispose()
		{
			_uIBuffer?.Dispose();
			_uIBuffer = null;
		}
	}
}
