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
						private float _outlineWidthMin = 0.04f;
			public float outlineWidthMin
			{
			    get => _outlineWidthMin;
			    set => _outlineWidthMin = value;
			}
						private float _outlineWidthMax = 0.95f;
			public float outlineWidthMax
			{
			    get => _outlineWidthMax;
			    set => _outlineWidthMax = value;
			}
						private float _outlineDistanceNear = 0.45f;
			public float outlineDistanceNear
			{
			    get => _outlineDistanceNear;
			    set => _outlineDistanceNear = value;
			}
						private float _outlineDistanceFar = 20f;
			public float outlineDistanceFar
			{
			    get => _outlineDistanceFar;
			    set => _outlineDistanceFar = value;
			}
						private AnimationCurve _fovCurve = AnimationCurve.Linear(0f, 1f, 100f, 1f);
			public AnimationCurve fovCurve
			{
			    get => _fovCurve;
			    set => _fovCurve = value;
			}
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
			var fovScale = _settings.fovCurve != null ? _settings.fovCurve.Evaluate(fov) : fov;
			if (Mathf.Approximately(fovScale, 0f))
			{
				fovScale = 1f;
			}

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(nameof(SekaiCharacterOutlinePass), out var passData))
			{
				passData.width = new Vector4(_settings.outlineWidthMin * 0.01f, _settings.outlineWidthMax * 0.01f, 0f, 0f);
				passData.factor = new Vector4(_settings.outlineDistanceNear,
					1f / (_settings.outlineDistanceFar - _settings.outlineDistanceNear), fov / fovScale, 0f);
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
