using System;
using UnityEngine;
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
						private RenderPassEvent _Event = RenderPassEvent.AfterRenderingOpaques;
			public RenderPassEvent Event
			{
			    get => _Event;
			    set => _Event = value;
			}
						private SekaiDrawObjectsRenderQueueType _RenderQueueType = SekaiDrawObjectsRenderQueueType.opaque;
			public SekaiDrawObjectsRenderQueueType RenderQueueType
			{
			    get => _RenderQueueType;
			    set => _RenderQueueType = value;
			}
						private bool _UseCustomRenderQueueRange;
			public bool UseCustomRenderQueueRange
			{
			    get => _UseCustomRenderQueueRange;
			    set => _UseCustomRenderQueueRange = value;
			}
						private int _RenderQueueLowerBound;
			public int RenderQueueLowerBound
			{
			    get => _RenderQueueLowerBound;
			    set => _RenderQueueLowerBound = value;
			}
						private int _RenderQueueUpperBound;
			public int RenderQueueUpperBound
			{
			    get => _RenderQueueUpperBound;
			    set => _RenderQueueUpperBound = value;
			}
						private SekaiShaderTagType[] _ShaderTagTypes = Array.Empty<SekaiShaderTagType>();
			public SekaiShaderTagType[] ShaderTagTypes
			{
			    get => _ShaderTagTypes;
			    set => _ShaderTagTypes = value;
			}
		}

		[SerializeField]
		private SekaiDrawObjectsSettings _settings = new SekaiDrawObjectsSettings();

		private SekaiDrawObjectsPass[] _passes;

		protected SekaiDrawObjectsSettings Settings => _settings;

		protected SekaiDrawObjectsPass[] Passes => _passes;

		public override void Create()
		{
			CreatePasses(_settings, out _passes);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			SetupRenderPass(_settings, _passes, renderingData.cameraData.camera);
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
