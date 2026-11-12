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

		[SerializeField]
		private CustomImage _background;

		[SerializeField]
		private GameObject _triParticle;

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
			if (_background != null)
			{
				_background.color = color;
			}
		}

		public void SetTriParticleActive(bool active)
		{
			if (_triParticle != null)
			{
				_triParticle.SetActive(active);
			}
		}
	}
}
