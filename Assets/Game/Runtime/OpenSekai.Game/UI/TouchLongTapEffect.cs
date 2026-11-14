using UnityEngine;

namespace Sekai.UI
{
	public class TouchLongTapEffect : MonoBehaviour
	{
		[SerializeField]
		private ParticleSystem[] _particles;

		[SerializeField]
		private float _longTapSec;

		private float _longTapSecCnt;

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
			_longTapSecCnt = 0f;
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

		public bool CountLongTap()
		{
			_longTapSecCnt = Mathf.Min(_longTapSecCnt + Time.deltaTime, _longTapSec);
			return _longTapSecCnt >= _longTapSec;
		}

		public TouchLongTapEffect()
		{
		}
	}
}
