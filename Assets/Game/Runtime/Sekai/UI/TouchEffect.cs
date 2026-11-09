using UnityEngine;

namespace Sekai.UI
{
	public class TouchEffect : MonoBehaviour
	{
		[SerializeField]
		private TouchTapEffect _tapEffect;

		[SerializeField]
		private TouchLongTapEffect _longTapEffect;

		[SerializeField]
		private TouchFollowEffect _followEffect;

		public void PlayTapEffect(Vector3 worldPos)
		{
			UpdatePosition(_tapEffect != null ? _tapEffect.Root : null, worldPos);
			if (!gameObject.activeSelf)
			{
				gameObject.SetActive(true);
			}

			_tapEffect?.StartEffect();
			_followEffect?.StopEffect();
			_longTapEffect?.StopEffect();
		}

		public void StopTapEffect()
		{
			_tapEffect?.StopEffect();
			gameObject.SetActive(false);
		}

		public void StartFollowEffect(Vector3 worldPos)
		{
			UpdatePosition(_followEffect != null ? _followEffect.Root : null, worldPos);
			_followEffect?.StartEffect();
		}

		public void EndFollowEffect()
		{
			_followEffect?.EndEffect();
		}

		private void StopFollowEffect()
		{
			_followEffect?.StopEffect();
		}

		public bool CountLongTap()
		{
			return _longTapEffect != null && _longTapEffect.CountLongTap();
		}

		public void StartLongTapEffect(Vector3 worldPos)
		{
			UpdatePosition(_followEffect != null ? _followEffect.Root : null, worldPos);
			_longTapEffect?.StartEffect();
		}

		public void EndLongTapEffect()
		{
			_longTapEffect?.EndEffect();
		}

		private void StopLongTapEffect()
		{
			_longTapEffect?.StopEffect();
		}

		private void UpdatePosition(Transform root, Vector3 worldPos)
		{
			if (root != null)
			{
				root.position = worldPos;
				var localPosition = root.localPosition;
				localPosition.z = 0f;
				root.localPosition = localPosition;
			}
		}

		public TouchEffect()
		{
		}
	}
}
