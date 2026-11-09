using UnityEngine;

namespace Sekai.UI
{
	public class TouchFollowEffect : MonoBehaviour
	{
		[SerializeField]
		private ParticleSystem[] _particles;

		[SerializeField]
		private Transform _root;

		public Transform Root
		{
			get
			{
				return _root;
			}
		}

		public void StartEffect()
		{
			if (_particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in _particles)
			{
				if (particle == null)
				{
					continue;
				}

				var main = particle.main;
				main.loop = true;
				var emission = particle.emission;
				emission.enabled = true;
				if (!particle.isPlaying)
				{
					particle.Play();
				}
			}
		}

		public void EndEffect()
		{
			if (_particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in _particles)
			{
				if (particle == null)
				{
					continue;
				}

				var main = particle.main;
				main.loop = false;
				var emission = particle.emission;
				emission.enabled = false;
			}
		}

		public void StopEffect()
		{
			if (_particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in _particles)
			{
				if (particle == null)
				{
					continue;
				}

				particle.Stop();
			}
		}

		public TouchFollowEffect()
		{
		}
	}
}
