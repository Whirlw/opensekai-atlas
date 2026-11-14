using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiDrawUIPass : ScriptableRenderPass
	{
		private static readonly int _s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
		private static readonly int _s_ScaleBiasRtPropID = Shader.PropertyToID("_ScaleBiasRt");
		private static readonly int _s_ScreenParamsPropID = Shader.PropertyToID("_ScreenParams");

		private readonly ProfilingSampler _profilingSampler;
		private ShaderTagId _shaderTagId = new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
		private FilteringSettings _filteringSettings = new FilteringSettings(RenderQueueRange.transparent, ~0);
		private RenderStateBlock _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
		private RTHandle _colorTargetHandle;
		private RTHandle _depthTargetHandle;
		private bool _isCameraRenderTarget;

		public SekaiDrawUIPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiDrawUIPass));
			_profilingSampler = new ProfilingSampler(profilerTag);
		}

		public void Setup(
			LayerMask layerMask,
			StencilState stencilState,
			int stencilReference,
			RTHandle colorTargetHandle,
			RTHandle depthTargetHandle,
			bool isCameraRenderTarget,
			RenderQueueRange renderQueueRange)
		{
			_shaderTagId = new ShaderTagId(SekaiShaderTag.GetTag(SekaiShaderTagType.Default));
			_filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
			_colorTargetHandle = colorTargetHandle;
			_depthTargetHandle = depthTargetHandle;
			_isCameraRenderTarget = isCameraRenderTarget;
			_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
			if (stencilState.enabled)
			{
				_renderStateBlock.stencilReference = stencilReference;
				_renderStateBlock.mask = RenderStateMask.Stencil;
				_renderStateBlock.stencilState = stencilState;
			}
		}

		private class PassData
		{
			public RendererListHandle rendererList;
			public TextureHandle color;
			public TextureHandle depth;
			public UniversalCameraData cameraData;
			public Vector4 screenParams;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (!_isCameraRenderTarget && _colorTargetHandle == null)
			{
				return;
			}

			var cameraData = frameData.Get<UniversalCameraData>();
			var renderingData = frameData.Get<UniversalRenderingData>();
			var resources = frameData.Get<UniversalResourceData>();
			var drawingSettings = RenderingUtils.CreateDrawingSettings(_shaderTagId, renderingData,
				cameraData, frameData.Get<UniversalLightData>(), (SortingCriteria)23);
			var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings)
			{
				tagValues = new NativeArray<ShaderTagId>(new[] { ShaderTagId.none }, Allocator.Temp),
				stateBlocks = new NativeArray<RenderStateBlock>(new[] { _renderStateBlock }, Allocator.Temp),
				isPassTagName = false
			};
			using (var builder = renderGraph.AddRasterRenderPass<PassData>(nameof(SekaiDrawUIPass), out var passData, _profilingSampler))
			{
				passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
				passData.color = _isCameraRenderTarget ? resources.activeColorTexture : renderGraph.ImportTexture(_colorTargetHandle);
				passData.depth = _isCameraRenderTarget ? resources.activeDepthTexture : renderGraph.ImportTexture(_depthTargetHandle);
				passData.cameraData = cameraData;
				passData.screenParams = GetScreenParams(cameraData);
				builder.UseRendererList(passData.rendererList);
				builder.SetRenderAttachment(passData.color, 0, AccessFlags.ReadWrite);
				if (passData.depth.IsValid())
				{
					builder.SetRenderAttachmentDepth(passData.depth, AccessFlags.Write);
				}
				builder.UseAllGlobalTextures(true);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					if (data.depth.IsValid())
					{
						context.cmd.ClearRenderTarget(RTClearFlags.DepthStencil, Color.black, 1f, 0);
					}
					context.cmd.SetGlobalVector(_s_DrawObjectPassDataPropID, Vector4.zero);
					var flipSign = data.cameraData.IsRenderTargetProjectionMatrixFlipped(data.color, data.depth) ? -1f : 1f;
					var scaleBias = flipSign < 0f ? new Vector4(flipSign, 1f, -1f, 1f) : new Vector4(flipSign, 0f, 1f, 1f);
					context.cmd.SetGlobalVector(_s_ScaleBiasRtPropID, scaleBias);
					context.cmd.SetGlobalVector(_s_ScreenParamsPropID, data.screenParams);
					context.cmd.DrawRendererList(data.rendererList);
				});
			}
		}

		private Vector4 GetScreenParams(UniversalCameraData cameraData)
		{
			var descriptor = cameraData.cameraTargetDescriptor;
			var width = descriptor.width;
			var height = descriptor.height;
			if (!_isCameraRenderTarget)
			{
				var scale = SekaiUIEffectSettings.Blur.CaptureResolutionScale;
				width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
				height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
			}

			return new Vector4(width, height, 1f + 1f / width, 1f + 1f / height);
		}
	}
}
