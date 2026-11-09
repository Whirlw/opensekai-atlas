using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiCharacterOutlineFeature : ScriptableRendererFeature
	{
		[SerializeField]
		private SekaiCharacterOutlinePass.OutlineSettings _settings = new SekaiCharacterOutlinePass.OutlineSettings();

		private SekaiCharacterOutlinePass _pass;

		public override void Create()
		{
			_pass = new SekaiCharacterOutlinePass();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (_pass != null)
			{
				_pass.Setup(_settings);
				_pass.renderPassEvent = RenderPassEvent.BeforeRendering;
				renderer.EnqueuePass(_pass);
			}
		}
	}
}
