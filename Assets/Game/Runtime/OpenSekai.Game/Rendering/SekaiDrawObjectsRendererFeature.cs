using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiDrawObjectsRendererFeature : ScriptableRendererFeature
	{
		[Serializable]
		protected enum SekaiDrawObjectsRenderQueueType
		{
			opaque = 0,
			transparent = 1
		}

		[Serializable]
		protected class SekaiDrawObjectsSettings
		{
			[FormerlySerializedAs("Event")]
			[SerializeField]
			private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingOpaques;

			public RenderPassEvent Event => passEvent;

			[FormerlySerializedAs("RenderQueueType")]
			[SerializeField]
			private SekaiDrawObjectsRenderQueueType renderQueueType = SekaiDrawObjectsRenderQueueType.opaque;

			public SekaiDrawObjectsRenderQueueType RenderQueueType => renderQueueType;

			[FormerlySerializedAs("UseCustomRenderQueueRange")]
			[SerializeField]
			private bool useCustomRenderQueueRange;

			public bool UseCustomRenderQueueRange => useCustomRenderQueueRange;

			[FormerlySerializedAs("RenderQueueLowerBound")]
			[SerializeField]
			private int renderQueueLowerBound;

			public int RenderQueueLowerBound => renderQueueLowerBound;

			[FormerlySerializedAs("RenderQueueUpperBound")]
			[SerializeField]
			private int renderQueueUpperBound;

			public int RenderQueueUpperBound => renderQueueUpperBound;

			[FormerlySerializedAs("ShaderTagTypes")]
			[SerializeField]
			private SekaiShaderTagType[] shaderTagTypes = Array.Empty<SekaiShaderTagType>();

			public SekaiShaderTagType[] ShaderTagTypes => shaderTagTypes;
		}

		[UnityEngine.Serialization.FormerlySerializedAs("_settings")]
		[SerializeField]
		private SekaiDrawObjectsSettings settings = new SekaiDrawObjectsSettings();

		private SekaiDrawObjectsPass[] _passes;

		protected SekaiDrawObjectsSettings Settings => settings;

		protected SekaiDrawObjectsPass[] Passes => _passes;

		public override void Create()
		{
			CreatePasses(settings, out _passes);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			SetupRenderPass(settings, _passes, renderingData.cameraData.camera);
			EnqueuePasses(_passes, renderer);
		}

		protected void CreatePasses(in SekaiDrawObjectsSettings settings, out SekaiDrawObjectsPass[] passes)
		{
			if (settings == null || settings.ShaderTagTypes == null || settings.ShaderTagTypes.Length == 0)
			{
				passes = null;
				return;
			}

			var renderQueueRange = settings.UseCustomRenderQueueRange
				? new RenderQueueRange(settings.RenderQueueLowerBound, settings.RenderQueueUpperBound)
				: settings.RenderQueueType == SekaiDrawObjectsRenderQueueType.transparent
					? RenderQueueRange.transparent
					: RenderQueueRange.opaque;
			var isOpaque = settings.RenderQueueType == SekaiDrawObjectsRenderQueueType.opaque;
			passes = new SekaiDrawObjectsPass[settings.ShaderTagTypes.Length];

			for (var i = 0; i < settings.ShaderTagTypes.Length; i++)
			{
				passes[i] = new SekaiDrawObjectsPass(name, settings.ShaderTagTypes[i], isOpaque, settings.Event, renderQueueRange);
			}
		}

		protected void EnqueuePasses(in SekaiDrawObjectsPass[] passes, ScriptableRenderer renderer)
		{
			if (passes == null || renderer == null)
			{
				return;
			}

			foreach (var pass in passes)
			{
				if (pass != null)
				{
					renderer.EnqueuePass(pass);
				}
			}
		}

		private void SetupRenderPass(in SekaiDrawObjectsSettings settings, in SekaiDrawObjectsPass[] passes, Camera camera)
		{
			if (settings == null || passes == null)
			{
				return;
			}

			var layerMask = camera != null ? camera.cullingMask : ~0;
			foreach (var pass in passes)
			{
				pass?.Setup(layerMask, StencilState.defaultValue, 0);
			}
		}
	}
}
