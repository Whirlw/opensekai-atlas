using UnityEngine;

public class UIBlurAnimator : MonoBehaviour
{
	private const float ANIMATION_INTERVAL = 2f;

	private UIBlurController _controller;
	private float _baseSamplingDistance;
	private float _elapsed;

	private void Start()
	{
		_controller = GetComponent<UIBlurController>();
		_baseSamplingDistance = _controller != null ? _controller.BlurSamplingDistance : 1.5f;
	}

	private void Update()
	{
		if (_controller == null)
		{
			return;
		}

		_elapsed += Time.deltaTime;
		var t = Mathf.Sin(_elapsed / ANIMATION_INTERVAL * Mathf.PI * 2f) * 0.5f + 0.5f;
		_controller.BlurSamplingDistance = Mathf.Lerp(_baseSamplingDistance * 0.9f, _baseSamplingDistance * 1.1f, t);
	}
}
