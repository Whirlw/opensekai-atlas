using UnityEngine;

namespace Sekai.UI
{
	public class TouchTapEffect : MonoBehaviour
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
				particle?.Play();
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
				particle?.Stop();
			}
		}

		public TouchTapEffect()
		{
		}
	}
}
