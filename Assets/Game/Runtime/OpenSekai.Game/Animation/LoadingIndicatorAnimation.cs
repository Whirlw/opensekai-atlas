using UnityEngine;

namespace Sekai
{
	public class LoadingIndicatorAnimation : MonoBehaviour
	{
		public enum State
		{
			Loading = 0,
			Complete = 1
		}

		[UnityEngine.Serialization.FormerlySerializedAs("_animator")]
		[SerializeField]
		private Animator animator;

		private State _currentState;
		private bool _hasPlayed;

		public void PlayAnimation(float progress)
		{
			State nextState = progress > 0.98f ? State.Complete : State.Loading;
			if (_hasPlayed && _currentState == nextState)
			{
				return;
			}

			if (animator != null)
			{
				animator.Play(nextState == State.Complete ? "clip_UIPartsDownloadIndicator_complete" : "clip_UIPartsDownloadIndicator", 0, 0f);
			}
			_currentState = nextState;
			_hasPlayed = true;
		}

		public LoadingIndicatorAnimation()
		{
		}
	}
}
