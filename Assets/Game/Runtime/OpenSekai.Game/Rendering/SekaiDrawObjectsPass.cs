using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiDrawObjectsPass : ScriptableRenderPass
	{
		private static readonly int _s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
		private static readonly int _s_ScaleBiasRtPropID = Shader.PropertyToID("_ScaleBiasRt");

		private readonly List<ShaderTagId> _shaderTagIdList = new List<ShaderTagId>();
		private readonly ProfilingSampler _profilingSampler;
		private FilteringSettings _filteringSettings;
		private RenderStateBlock _renderStateBlock;
		private bool _isOpaque;
		private bool _skipExecution;

		public SekaiDrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, bool skipExecution = false)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiDrawObjectsPass));
			_profilingSampler = new ProfilingSampler(profilerTag);
			renderPassEvent = evt;
			_isOpaque = opaque;
			_skipExecution = skipExecution;
			_filteringSettings = new FilteringSettings(renderQueueRange, ~0);
			_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

			foreach (var shaderTagId in shaderTagIds)
			{
				_shaderTagIdList.Add(shaderTagId);
			}
		}

		public SekaiDrawObjectsPass(string profilerTag, SekaiShaderTagType shaderTagType, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange)
			: this(
				profilerTag,
				new[] { new ShaderTagId(SekaiShaderTag.GetTag(shaderTagType)) },
				opaque,
				evt,
				renderQueueRange,
				shaderTagType == SekaiShaderTagType.Default)
		{
		}

		internal void Setup(LayerMask layerMask, StencilState stencilState, int stencilReference)
		{
			_filteringSettings.layerMask = layerMask;
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
			public bool isOpaque;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			// The built-in UniversalRenderer already draws the SRPDefaultUnlit tag.
			// Replaying Default here makes transparent UI accumulate alpha twice.
			if (_skipExecution)
			{
				return;
			}

			var cameraData = frameData.Get<UniversalCameraData>();
			var renderingData = frameData.Get<UniversalRenderingData>();
			var resources = frameData.Get<UniversalResourceData>();
			var sortingCriteria = _isOpaque ? cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
			var drawingSettings = RenderingUtils.CreateDrawingSettings(_shaderTagIdList, renderingData,
				cameraData, frameData.Get<UniversalLightData>(), sortingCriteria);
			var filteringSettings = _filteringSettings;
#if UNITY_EDITOR
			if (cameraData.isPreviewCamera)
			{
				filteringSettings.layerMask = -1;
			}
#endif
			var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings)
			{
				tagValues = new NativeArray<ShaderTagId>(new[] { ShaderTagId.none }, Allocator.Temp),
				stateBlocks = new NativeArray<RenderStateBlock>(new[] { _renderStateBlock }, Allocator.Temp),
				isPassTagName = false
			};
			using (var builder = renderGraph.AddRasterRenderPass<PassData>(nameof(SekaiDrawObjectsPass), out var passData, _profilingSampler))
			{
				passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
				passData.color = resources.activeColorTexture;
				passData.depth = resources.activeDepthTexture;
				passData.cameraData = cameraData;
				passData.isOpaque = _isOpaque;
				builder.UseRendererList(passData.rendererList);
				builder.SetRenderAttachment(passData.color, 0, AccessFlags.ReadWrite);
				if (passData.depth.IsValid())
				{
					builder.SetRenderAttachmentDepth(passData.depth, AccessFlags.ReadWrite);
				}
				builder.UseAllGlobalTextures(true);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					context.cmd.SetGlobalVector(_s_DrawObjectPassDataPropID, new Vector4(0f, 0f, 0f, data.isOpaque ? 1f : 0f));
					var flipSign = data.cameraData.IsRenderTargetProjectionMatrixFlipped(data.color, data.depth) ? -1f : 1f;
					var scaleBias = flipSign < 0f ? new Vector4(flipSign, 1f, -1f, 1f) : new Vector4(flipSign, 0f, 1f, 1f);
					context.cmd.SetGlobalVector(_s_ScaleBiasRtPropID, scaleBias);
					context.cmd.DrawRendererList(data.rendererList);
				});
			}
		}
	}
}
