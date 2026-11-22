using UnityEngine;

namespace Sekai.UI
{
	public class TouchTapEffect : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_particles")]
		[SerializeField]
		private ParticleSystem[] particles;

		[UnityEngine.Serialization.FormerlySerializedAs("_root")]
		[SerializeField]
		private Transform root;

		public Transform Root
		{
			get
			{
				return root;
			}
		}

		public void StartEffect()
		{
			if (particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in particles)
			{
				particle?.Play();
			}
		}

		public void StopEffect()
		{
			if (particles == null)
			{
				return;
			}

			foreach (ParticleSystem particle in particles)
			{
				particle?.Stop();
			}
		}

		public TouchTapEffect()
		{
		}
	}
}
