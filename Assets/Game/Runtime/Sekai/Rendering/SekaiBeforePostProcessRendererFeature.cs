using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiBeforePostProcessRendererFeature : ScriptableRendererFeature
	{
		[Serializable]
		protected class SekaiBeforePostProcessSettings
		{
						private RenderPassEvent _Event = RenderPassEvent.AfterRenderingPostProcessing;
			public RenderPassEvent Event
			{
			    get => _Event;
			    set => _Event = value;
			}
						private RenderQueueType _RenderQueueType = RenderQueueType.Transparent;
			public RenderQueueType RenderQueueType
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
		}

		[SerializeField]
		private SekaiBeforePostProcessSettings _beforePostProcessSettings = new SekaiBeforePostProcessSettings();

		public override void Create()
		{
			// TODO: Port SekaiMeshFlareParaPass from rendering_tmp/Sekai.Rendering.
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
		}
	}
}
