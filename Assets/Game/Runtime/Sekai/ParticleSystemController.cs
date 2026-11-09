using UnityEngine;

namespace Sekai
{
	public class ParticleSystemController : MonoBehaviour
	{
		private ParticleSystem _particleSystem;

		private ParticleSystem[] _particleSystems;

		private ParticleSystemRenderer[] _renderers;

		private bool _isPlayInternal;

		public bool IsPlaying
		{
			get
			{
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
			if (_isPlayInternal && !IsPlaying)
			{
				Stop();
			}
		}

		public void Play()
		{
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

		public void Stop()
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

			_isPlayInternal = false;
		}

		public ParticleSystemController()
		{
		}
	}
}
