using System.Collections.Generic;

namespace Sekai.Rendering
{
	public static class SekaiUIEffectSettings
	{
		private static List<SekaiUIEffectParameter> _parameters = new List<SekaiUIEffectParameter>();

		public static readonly SekaiUIBlurParameter Blur = new SekaiUIBlurParameter();

		public static IReadOnlyList<SekaiUIEffectParameter> Parameters => _parameters;

		public static void SetParameters(List<SekaiUIEffectParameter> _parameters)
		{
			SekaiUIEffectSettings._parameters = _parameters ?? new List<SekaiUIEffectParameter>();
			Blur.IsActive = false;
			foreach (var parameter in SekaiUIEffectSettings._parameters)
			{
				if ((parameter.EffectFlags & SekaiUIEffectFlags.Blur) == 0)
				{
					continue;
				}

				Blur.IsActive = true;
				Blur.CaptureResolutionScale = parameter.CaptureResolutionScale;
				Blur.BlurSamplingDistance = parameter.BlurSamplingDistance;
				break;
			}
		}
	}
}
