using UnityEngine;

namespace Sekai.UI
{
	public class TouchLongTapEffect : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_particles")]
		[SerializeField]
		private ParticleSystem[] particles;

		[UnityEngine.Serialization.FormerlySerializedAs("_longTapSec")]
		[SerializeField]
		private float longTapSec;

		private float _longTapSecCnt;

		public void StartEffect()
		{
			if (particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in particles)
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
			if (particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in particles)
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
			if (particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in particles)
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
			_longTapSecCnt = Mathf.Min(_longTapSecCnt + Time.deltaTime, longTapSec);
			return _longTapSecCnt >= longTapSec;
		}

		public TouchLongTapEffect()
		{
		}
	}
}
