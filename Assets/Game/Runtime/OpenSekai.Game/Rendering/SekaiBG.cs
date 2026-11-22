using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class SekaiBG : MonoBehaviour
	{
		public enum Status
		{
			None = 0,
			Showing = 1,
			Show = 2,
			Closing = 3
		}

		[UnityEngine.Serialization.FormerlySerializedAs("_background")]
		[SerializeField]
		private CustomImage background;

		[UnityEngine.Serialization.FormerlySerializedAs("_triParticle")]
		[SerializeField]
		private GameObject triParticle;

		private Status _status;

		public bool IsActive => _status == Status.Show || _status == Status.Showing;

		public bool IsDuringTransition => _status == Status.Showing || _status == Status.Closing;

		public Status CurrentStatus => _status;

		public void Initialize()
		{
			_status = Status.None;
			SetTriParticleActive(false);
		}

		public void Show(System.Action onFinished, System.Action onShowUI)
		{
			_status = Status.Showing;
			gameObject.SetActive(true);
			SetTriParticleActive(true);
			onShowUI?.Invoke();
			_status = Status.Show;
			onFinished?.Invoke();
		}

		public void Transition()
		{
			gameObject.SetActive(true);
			SetTriParticleActive(true);
		}

		public void Hidden(System.Action onFinished)
		{
			_status = Status.Closing;
			SetTriParticleActive(false);
			_status = Status.None;
			onFinished?.Invoke();
		}

		public void SetColor(Color color)
		{
			if (background != null)
			{
				background.color = color;
			}
		}

		public void SetTriParticleActive(bool active)
		{
			if (triParticle != null)
			{
				triParticle.SetActive(active);
			}
		}
	}
}
