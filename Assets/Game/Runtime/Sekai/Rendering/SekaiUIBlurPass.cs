using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiUIBlurPass : ScriptableRenderPass
	{
		private static readonly int SamplingDistance = Shader.PropertyToID("_SamplingDistance");
		private static readonly int BlurResolutionParams = Shader.PropertyToID("_BlurResolutionParams");

		private readonly ProfilingSampler _profilingSampler;
		private readonly Material _uIEffectMaterial;
		private RTHandle _source;
		private RTHandle _destination;

		public SekaiUIBlurPass(string profilerTag)
		{
			profilingSampler = new ProfilingSampler(nameof(SekaiUIBlurPass));
			_profilingSampler = new ProfilingSampler(profilerTag);
			var shader = Shader.Find("Hidden/CP/PostEffect/UIEffect");
			if (shader == null)
			{
				shader = Shader.Find("Sekai/UI/UIGaussianBlur");
			}

			if (shader != null)
			{
				_uIEffectMaterial = CoreUtils.CreateEngineMaterial(shader);
			}
		}

		public void Setup(RTHandle source, RTHandle dest)
		{
			_source = source;
			_destination = dest;
		}

		private class PassData
		{
			public TextureHandle source;
			public TextureHandle destination;
			public TextureHandle temporary;
			public Material material;
			public float samplingDistance;
			public Vector4 resolution;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (_source == null || _destination == null || SekaiUIBuffer.BlurTempHandle == null || _uIEffectMaterial == null)
			{
				return;
			}

			using (var builder = renderGraph.AddUnsafePass<PassData>(nameof(SekaiUIBlurPass), out var passData, _profilingSampler))
			{
				var descriptor = frameData.Get<UniversalCameraData>().cameraTargetDescriptor;
				passData.source = renderGraph.ImportTexture(_source);
				passData.destination = renderGraph.ImportTexture(_destination);
				passData.temporary = renderGraph.ImportTexture(SekaiUIBuffer.BlurTempHandle);
				passData.material = _uIEffectMaterial;
				passData.samplingDistance = SekaiUIEffectSettings.Blur.BlurSamplingDistance;
				passData.resolution = new Vector4(1f / descriptor.width, 1f / descriptor.height, 0f, 0f);
				builder.UseTexture(passData.source, AccessFlags.Read);
				builder.UseTexture(passData.destination, AccessFlags.Write);
				builder.UseTexture(passData.temporary, AccessFlags.ReadWrite);
				builder.AllowGlobalStateModification(true);
				builder.AllowPassCulling(false);
				builder.SetGlobalTextureAfterPass(passData.destination, Shader.PropertyToID("_UIBlurTex"));
				builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
				{
					var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
					cmd.SetGlobalFloat(SamplingDistance, data.samplingDistance);
					cmd.SetGlobalVector(BlurResolutionParams, data.resolution);
					Blitter.BlitCameraTexture(cmd, data.source, data.temporary, data.material, 0);
					Blitter.BlitCameraTexture(cmd, data.temporary, data.destination, data.material, 1);
				});
			}
		}

		public void Cleanup()
		{
			CoreUtils.Destroy(_uIEffectMaterial);
		}
	}
}
