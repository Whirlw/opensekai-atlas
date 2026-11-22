using UnityEngine;
using Sekai.Rendering;

[ExecuteAlways]
public class UIBlurController : MonoBehaviour
{
	[UnityEngine.Serialization.FormerlySerializedAs("m_BlurSamplingDistance")]
	[SerializeField]
	[Range(0.1f, 2f)]
	private float mBlurSamplingDistance = 1.5f;

	[UnityEngine.Serialization.FormerlySerializedAs("m_CaptureResolutionScale")]
	[SerializeField]
	[Range(0.2f, 1f)]
	private float mCaptureResolutionScale = 0.5f;

	public float BlurSamplingDistance
	{
		get => mBlurSamplingDistance;
		set => mBlurSamplingDistance = Mathf.Clamp(value, 0.1f, 2f);
	}

	public float CaptureResolutionScale
	{
		get => mCaptureResolutionScale;
		set => mCaptureResolutionScale = Mathf.Clamp(value, 0.2f, 1f);
	}

	private void Update()
	{
		Apply();
	}

	private void OnEnable()
	{
		Apply();
	}

	private void Apply()
	{
		SekaiUIEffectSettings.Blur.BlurSamplingDistance = mBlurSamplingDistance;
		SekaiUIEffectSettings.Blur.CaptureResolutionScale = mCaptureResolutionScale;
	}
}
