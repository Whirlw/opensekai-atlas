using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sekai.Rendering
{
	public class SekaiCharacterOutlinePass : ScriptableRenderPass
	{
		private static class PropertyId
		{
			public static readonly int OutlineFactorId = Shader.PropertyToID("_SekaiOutlineFactor");
			public static readonly int OutlineWidthId = Shader.PropertyToID("_SekaiOutlineWidth");
		}

		[Serializable]
		public class OutlineSettings
		{
			[SerializeField]
			private float outlineWidthMin = 0.04f;

			public float OutlineWidthMin => outlineWidthMin;

			[SerializeField]
			private float outlineWidthMax = 0.95f;

			public float OutlineWidthMax => outlineWidthMax;

			[SerializeField]
			private float outlineDistanceNear = 0.45f;

			public float OutlineDistanceNear => outlineDistanceNear;

			[SerializeField]
			private float outlineDistanceFar = 20f;

			public float OutlineDistanceFar => outlineDistanceFar;

			[SerializeField]
			private AnimationCurve fovCurve = AnimationCurve.Linear(0f, 1f, 100f, 1f);

			public AnimationCurve FovCurve => fovCurve;
		}

		private OutlineSettings _settings;

		public void Setup(OutlineSettings settings)
		{
			_settings = settings;
		}

		private class PassData
		{
			public Vector4 width;
			public Vector4 factor;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			var camera = frameData.Get<UniversalCameraData>().camera;
			if (camera == null || _settings == null)
			{
				return;
			}

			var fov = camera.fieldOfView;
			var fovScale = _settings.FovCurve != null ? _settings.FovCurve.Evaluate(fov) : fov;
			if (Mathf.Approximately(fovScale, 0f))
			{
				fovScale = 1f;
			}

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(nameof(SekaiCharacterOutlinePass), out var passData))
			{
				passData.width = new Vector4(_settings.OutlineWidthMin * 0.01f, _settings.OutlineWidthMax * 0.01f, 0f, 0f);
				passData.factor = new Vector4(_settings.OutlineDistanceNear,
					1f / (_settings.OutlineDistanceFar - _settings.OutlineDistanceNear), fov / fovScale, 0f);
				builder.AllowGlobalStateModification(true);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					context.cmd.SetGlobalVector(PropertyId.OutlineWidthId, data.width);
					context.cmd.SetGlobalVector(PropertyId.OutlineFactorId, data.factor);
				});
			}
		}
	}
}
