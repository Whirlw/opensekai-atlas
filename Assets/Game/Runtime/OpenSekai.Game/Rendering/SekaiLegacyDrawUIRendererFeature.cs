using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiLegacyDrawUIRendererFeature : ScriptableRendererFeature
	{
		[Serializable]
		public class AreaCameraSettings
		{
			[FormerlySerializedAs("Event_DrawUI")]
			[SerializeField]
			private int eventDrawUI = (int)RenderPassEvent.AfterRenderingTransparents;

			public int EventDrawUI => eventDrawUI;

			[FormerlySerializedAs("Event_CopyScreenToCapture")]
			[SerializeField]
			private int eventCopyScreenToCapture = (int)RenderPassEvent.AfterRenderingPostProcessing;

			public int EventCopyScreenToCapture => eventCopyScreenToCapture;
		}

		[Serializable]
		public class PassSettings
		{
			[FormerlySerializedAs("Event_DrawBlurUI")]
			[SerializeField]
			private int eventDrawBlurUI = (int)RenderPassEvent.AfterRenderingOpaques;

			public int EventDrawBlurUI => eventDrawBlurUI;

			[FormerlySerializedAs("Event_Blur")]
			[SerializeField]
			private int eventBlur = (int)RenderPassEvent.AfterRenderingPostProcessing;

			public int EventBlur => eventBlur;

			[FormerlySerializedAs("Event_DrawUI")]
			[SerializeField]
			private int eventDrawUI = (int)RenderPassEvent.AfterRendering;

			public int EventDrawUI => eventDrawUI;
		}

		[Serializable]
		public class UIBlurSettings
		{
			[FormerlySerializedAs("BlurLayerMask")]
			[SerializeField]
			private LayerMask blurLayerMask;

			public LayerMask BlurLayerMask => blurLayerMask;

			[FormerlySerializedAs("UILayerMask")]
			[SerializeField]
			private LayerMask uiLayerMask;

			public LayerMask UILayerMask => uiLayerMask;
			public RenderQueueRange RenderQueueRange => RenderQueueRange.transparent;
		}

		[UnityEngine.Serialization.FormerlySerializedAs("_blurSettings")]
		[SerializeField]
		private UIBlurSettings blurSettings = new UIBlurSettings();

		[UnityEngine.Serialization.FormerlySerializedAs("_passSettings")]
		[SerializeField]
		private PassSettings passSettings = new PassSettings();

		[UnityEngine.Serialization.FormerlySerializedAs("_areaCameraSettings")]
		[SerializeField]
		private AreaCameraSettings areaCameraSettings = new AreaCameraSettings();

		private SekaiUIBuffer _uIBuffer;
		private SekaiUIBlurSetupPass _setupPass;
		private SekaiCopyPass _copyScreenToCapturePass;
		private SekaiDrawUIPass _drawBlurUIPass;
		private SekaiUIBlurPass _blurPass;
		private SekaiDrawUIPass _drawUIPass;

		public override void Create()
		{
			_uIBuffer?.Dispose();
			_blurPass?.Cleanup();
			_uIBuffer = new SekaiUIBuffer();
			_setupPass = new SekaiUIBlurSetupPass();
			_drawBlurUIPass = new SekaiDrawUIPass("Render BlurUI [Sekai UICamera] ");
			_blurPass = new SekaiUIBlurPass("Execute Blur [Sekai UICamera] ");
			_drawUIPass = new SekaiDrawUIPass("Render UI [Sekai DefaultCamera]");
			_copyScreenToCapturePass = new SekaiCopyPass("Copy Screen to CaptureTex [Sekai]");
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			SetupRenderPasses(renderer, in renderingData);
			if (SekaiUIEffectSettings.Blur.IsActive && IsFinalCamera(renderingData.cameraData.camera))
			{
				renderer.EnqueuePass(_setupPass);
				renderer.EnqueuePass(_copyScreenToCapturePass);
				renderer.EnqueuePass(_drawBlurUIPass);
				renderer.EnqueuePass(_blurPass);
				renderer.EnqueuePass(_drawUIPass);
				return;
			}

			renderer.EnqueuePass(_drawUIPass);
		}

		private void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
		{
			if (renderer == null || _drawUIPass == null)
			{
				return;
			}

			if (SekaiUIEffectSettings.Blur.IsActive && IsFinalCamera(renderingData.cameraData.camera))
			{
				SetupUICameraPass(renderer, in renderingData);
			}
			else
			{
				SetupDefaultCameraPass(renderer, in renderingData);
			}
		}

		protected override void Dispose(bool disposing)
		{
			_blurPass?.Cleanup();
			_setupPass?.Dispose();
			_uIBuffer?.Dispose();
		}

		private void SetupUICameraPass(ScriptableRenderer renderer, in RenderingData renderingData)
		{
			var colorDescriptor = renderingData.cameraData.cameraTargetDescriptor;
			colorDescriptor.depthBufferBits = 0;
			var depthDescriptor = colorDescriptor;
			depthDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
			depthDescriptor.depthStencilFormat = SystemInfo.GetGraphicsFormat(UnityEngine.Experimental.Rendering.DefaultFormat.DepthStencil);
			depthDescriptor.msaaSamples = 1;
			var cameraLayerMask = GetCameraLayerMask(renderingData.cameraData.camera);

			_setupPass.renderPassEvent = RenderPassEvent.BeforeRendering;
			_setupPass.Setup(_uIBuffer, colorDescriptor, depthDescriptor);

			_copyScreenToCapturePass.renderPassEvent = (RenderPassEvent)1;
			_copyScreenToCapturePass.Setup(null, SekaiUIBuffer.CaptureColorTexHandle);

			_drawBlurUIPass.renderPassEvent = (RenderPassEvent)passSettings.EventDrawBlurUI;
			_drawBlurUIPass.Setup(
				cameraLayerMask & blurSettings.BlurLayerMask,
				StencilState.defaultValue,
				0,
				SekaiUIBuffer.CaptureColorTexHandle,
				SekaiUIBuffer.CaptureDepthTexHandle,
				false,
				blurSettings.RenderQueueRange);

			_blurPass.renderPassEvent = (RenderPassEvent)passSettings.EventBlur;
			_blurPass.Setup(SekaiUIBuffer.CaptureColorTexHandle, SekaiUIBuffer.UIBlurTexHandle);

			_drawUIPass.renderPassEvent = (RenderPassEvent)passSettings.EventDrawUI;
			_drawUIPass.Setup(
				cameraLayerMask & blurSettings.UILayerMask,
				StencilState.defaultValue,
				0,
				null,
				null,
				true,
				blurSettings.RenderQueueRange);
		}

		private void SetupDefaultCameraPass(ScriptableRenderer renderer, in RenderingData renderingData)
		{
			_drawUIPass.renderPassEvent = (RenderPassEvent)areaCameraSettings.EventDrawUI;
			_drawUIPass.Setup(
				GetCameraLayerMask(renderingData.cameraData.camera),
				StencilState.defaultValue,
				0,
				null,
				null,
				true,
				blurSettings.RenderQueueRange);
		}

		private static LayerMask GetCameraLayerMask(Camera camera)
		{
			return camera != null ? camera.cullingMask : ~0;
		}

		private static RenderTextureDescriptor GetDescriptor(RTHandle handle, RenderTextureDescriptor fallback)
		{
			return handle != null && handle.rt != null ? handle.rt.descriptor : fallback;
		}

		private static bool IsFinalCamera(Camera camera)
		{
			if (camera == null)
			{
				return true;
			}

			return camera.TryGetComponent<UnityEngine.Rendering.Universal.SekaiAdditionalCameraData>(out var data)
				? data.IsFinalCamera
				: true;
		}
	}
}
