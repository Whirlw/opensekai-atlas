using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiLegacyDrawUIRendererFeature : ScriptableRendererFeature
	{
		[Serializable]
		public class AreaCameraSettings
		{
						private int _Event_DrawUI = (int)RenderPassEvent.AfterRenderingTransparents;
			public int Event_DrawUI
			{
			    get => _Event_DrawUI;
			    set => _Event_DrawUI = value;
			}
						private int _Event_CopyScreenToCapture = (int)RenderPassEvent.AfterRenderingPostProcessing;
			public int Event_CopyScreenToCapture
			{
			    get => _Event_CopyScreenToCapture;
			    set => _Event_CopyScreenToCapture = value;
			}
		}

		[Serializable]
		public class PassSettings
		{
						private int _Event_DrawBlurUI = (int)RenderPassEvent.AfterRenderingOpaques;
			public int Event_DrawBlurUI
			{
			    get => _Event_DrawBlurUI;
			    set => _Event_DrawBlurUI = value;
			}
						private int _Event_Blur = (int)RenderPassEvent.AfterRenderingPostProcessing;
			public int Event_Blur
			{
			    get => _Event_Blur;
			    set => _Event_Blur = value;
			}
						private int _Event_DrawUI = (int)RenderPassEvent.AfterRendering;
			public int Event_DrawUI
			{
			    get => _Event_DrawUI;
			    set => _Event_DrawUI = value;
			}
		}

		[Serializable]
		public class UIBlurSettings
		{
						private LayerMask _BlurLayerMask;
			public LayerMask BlurLayerMask
			{
			    get => _BlurLayerMask;
			    set => _BlurLayerMask = value;
			}
						private LayerMask _UILayerMask;
			public LayerMask UILayerMask
			{
			    get => _UILayerMask;
			    set => _UILayerMask = value;
			}
						private RenderQueueRange _RenderQueueRange = RenderQueueRange.transparent;
			public RenderQueueRange RenderQueueRange
			{
			    get => _RenderQueueRange;
			    set => _RenderQueueRange = value;
			}
		}

		[SerializeField]
		private UIBlurSettings _blurSettings = new UIBlurSettings();

		[SerializeField]
		private PassSettings _passSettings = new PassSettings();

		[SerializeField]
		private AreaCameraSettings _areaCameraSettings = new AreaCameraSettings();

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

			_drawBlurUIPass.renderPassEvent = (RenderPassEvent)_passSettings.Event_DrawBlurUI;
			_drawBlurUIPass.Setup(
				cameraLayerMask & _blurSettings.BlurLayerMask,
				StencilState.defaultValue,
				0,
				SekaiUIBuffer.CaptureColorTexHandle,
				SekaiUIBuffer.CaptureDepthTexHandle,
				false,
				_blurSettings.RenderQueueRange);

			_blurPass.renderPassEvent = (RenderPassEvent)_passSettings.Event_Blur;
			_blurPass.Setup(SekaiUIBuffer.CaptureColorTexHandle, SekaiUIBuffer.UIBlurTexHandle);

			_drawUIPass.renderPassEvent = (RenderPassEvent)_passSettings.Event_DrawUI;
			_drawUIPass.Setup(
				cameraLayerMask & _blurSettings.UILayerMask,
				StencilState.defaultValue,
				0,
				null,
				null,
				true,
				_blurSettings.RenderQueueRange);
		}

		private void SetupDefaultCameraPass(ScriptableRenderer renderer, in RenderingData renderingData)
		{
			_drawUIPass.renderPassEvent = (RenderPassEvent)_areaCameraSettings.Event_DrawUI;
			_drawUIPass.Setup(
				GetCameraLayerMask(renderingData.cameraData.camera),
				StencilState.defaultValue,
				0,
				null,
				null,
				true,
				_blurSettings.RenderQueueRange);
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
