using System.Linq;
using OpenSekai.MaterialPacks;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class ParticleSystemController : MonoBehaviour
	{
		private ParticleSystem _particleSystem;

		private ParticleSystem[] _particleSystems;
		private (ParticleSystem particles, uint seed)[] _fixedSeedLayers;
		private static readonly System.Random _burstRandom = new System.Random();

		private ParticleSystemRenderer[] _renderers;
		private MaterialPropertyBlock _palette;
		private int _style;
		private static readonly int _layoutProperty = Shader.PropertyToID("_HitFxLayout");
		private Vector2 _scaleDelta;
		private Vector3 _offset;
		private Vector3? _lastPivot;

		internal static ParticleSystemController Create(GameObject prefab, Transform parent, LiveHitEffectSkin skin,
			bool varyBurst = false)
		{
			if (prefab == null) return null;
			var controller = Instantiate(prefab, parent, false).AddComponent<ParticleSystemController>();
			skin?.Apply(controller, prefab.name);
			if (prefab.name is LivePrefabDefine.FX_LONG_AURA or LivePrefabDefine.FX_CRITICAL_LONG_AURA
				or LivePrefabDefine.FX_LONG_HOLD_AURA or LivePrefabDefine.FX_CRITICAL_LONG_HOLD_AURA
				or LivePrefabDefine.FX_FLICK_AURA or LivePrefabDefine.FX_CRITICAL_FLICK_AURA)
			{
				if (controller.Sequence == null && skin?.HasTemplateTexture(prefab.name) != true)
					controller.AlignBuiltInHoldBeams();
			}
			if (varyBurst && controller.Sequence == null)
			{
				var layers = controller._particleSystems ?? controller.GetComponentsInChildren<ParticleSystem>(true);
				controller._fixedSeedLayers = layers.Where(p => !p.useAutoRandomSeed)
					.Select(p => (p, p.randomSeed)).ToArray();
			}
			return controller;
		}

		private void AlignBuiltInHoldBeams()
		{
			// Both the base beam and its additive pass draw the bright edge at the bottom of the cell.
			foreach (var renderer in _renderers ?? GetComponentsInChildren<ParticleSystemRenderer>(true))
			{
				if (renderer.name is not ("aura" or "aura_add")) continue;
				renderer.pivot = new Vector3(renderer.pivot.x, 0.5f, renderer.pivot.z);

				// The burst stretches and fades above the hit. Keep its base there as it grows.
				var particles = renderer.GetComponent<ParticleSystem>();
				var shape = particles.shape;
				shape.position = new Vector3(shape.position.x, 0f, shape.position.z);
				var velocity = particles.velocityOverLifetime;
				if (velocity.enabled) velocity.yMultiplier = 0f;
				var force = particles.forceOverLifetime;
				if (force.enabled) force.yMultiplier = 0f;
			}
		}

		internal void SetHitLayout(MaterialSpriteSlot layout, bool screen)
		{
			if (layout?.Scale != null) _scaleDelta = new Vector2(layout.Scale[0] - 1f, layout.Scale[1] - 1f);
			if (layout?.Position == null) return;
			float unit = screen ? 10f / 1080f : LiveUtility.CalcLaneTransformX(1) - LiveUtility.CalcLaneTransformX(0);
			_offset = new Vector3(layout.Position[0], layout.Position[1], 0f) * unit;
		}

		internal void Place(Vector3 anchor) => transform.localPosition = anchor + _offset;

		internal HitEffectSequence Sequence { get; set; }
		private bool _isPlayInternal;
		private bool _packPaused;

		internal void SetPackPaused(bool paused)
		{
			_packPaused = paused;
			if (Sequence != null || _particleSystems == null) return;
			foreach (var particles in _particleSystems)
			{
				if (paused && particles.isPlaying) particles.Pause(false);
				else if (!paused && particles.isPaused) particles.Play(false);
			}
		}

		internal void ResetPackPlayback()
		{
			_packPaused = false;
			Stop();
		}

		public bool IsPlaying
		{
			get
			{
				if (Sequence != null) return Sequence.Holding;
				int particleCount = 0;
				if (_particleSystems != null)
				{
					for (int i = 0; i < _particleSystems.Length; i++)
					{
						if (_particleSystems[i] != null)
						{
							particleCount += _particleSystems[i].particleCount;
						}
					}
				}

				return particleCount > 0 || (_particleSystem != null && _particleSystem.isPlaying);
			}
		}

		private void Awake()
		{
			_particleSystem = GetComponentInChildren<ParticleSystem>(true);
			_particleSystems = GetComponentsInChildren<ParticleSystem>(true);
			_renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);

			if (_particleSystem != null)
			{
				_particleSystem.Clear(true);
				_particleSystem.Stop(true);
			}

			if (_particleSystems == null)
			{
				return;
			}

			for (int i = 0; i < _particleSystems.Length; i++)
			{
				if (_particleSystems[i] == null)
				{
					continue;
				}

				_particleSystems[i].Clear(true);
				_particleSystems[i].Stop(true);
			}
		}

		private void Update()
		{
			if (_packPaused) return;
			if (Sequence != null) { Sequence.Advance(Time.deltaTime); return; }
			if (_isPlayInternal && !IsPlaying)
			{
				Stop();
			}
		}

		public void Play()
		{
			Play(0);
		}

		internal void Play(int style)
		{
			if (_packPaused) return;
			if (Sequence != null) { Sequence.Play(style); return; }
			SetStyle(style);
			if (_isPlayInternal)
			{
				if (_particleSystem != null)
				{
					_particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}

				if (_particleSystems != null)
				{
					for (int i = 0; i < _particleSystems.Length; i++)
					{
						_particleSystems[i]?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
					}
				}
			}

			if (_fixedSeedLayers is { Length: > 0 })
			{
				// A star and its trail share a seed in the prefab. Vary them together on each hit.
				uint variation = (uint)_burstRandom.Next(1, int.MaxValue);
#if UNITY_EDITOR
				if (Live.LiveMotion.CaptureScheduler != null) variation = 0;
#endif
				foreach (var layer in _fixedSeedLayers)
					layer.particles.randomSeed = layer.seed ^ variation;
			}

			if (_particleSystems != null)
			{
				for (int i = 0; i < _particleSystems.Length; i++)
				{
					_particleSystems[i]?.Play(true);
				}
			}

			if (_particleSystem != null)
			{
				_particleSystem.Play(true);
			}

			_isPlayInternal = true;
		}

		internal void SetStyle(int style)
		{
			if (Sequence != null) { Sequence.SetStyle(style); return; }
			NextRushPalette.Apply(_renderers, style, ref _style, ref _palette);
			if (_scaleDelta == Vector2.zero) return;
			// Scale the finished particle geometry around the hit, including world-space particles and trails.
			Vector3 pivot = transform.position;
			if (_lastPivot.HasValue && pivot == _lastPivot.Value) return;
			_lastPivot = pivot;
			_palette ??= new MaterialPropertyBlock();
			var layout = new Vector4(_scaleDelta.x, _scaleDelta.y, pivot.x, pivot.y);
			foreach (var renderer in _renderers)
			{
				if (renderer == null) continue;
				renderer.GetPropertyBlock(_palette);
				_palette.SetVector(_layoutProperty, layout);
				renderer.SetPropertyBlock(_palette);
			}
		}

		internal void ReleaseHold()
		{
			if (Sequence != null) Sequence.Release();
			else Stop();
		}

		public void Stop()
		{
			if (Sequence != null) { Sequence.Stop(); return; }
			if (_particleSystem != null)
			{
				_particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}

			if (_particleSystems != null)
			{
				for (int i = 0; i < _particleSystems.Length; i++)
				{
					_particleSystems[i]?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
			}

			_isPlayInternal = false;
		}

		public ParticleSystemController()
		{
		}
	}
}
