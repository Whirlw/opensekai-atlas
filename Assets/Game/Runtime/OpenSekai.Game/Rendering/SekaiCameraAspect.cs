namespace Sekai.Core
{
	using UnityEngine;

	public static class SekaiCameraAspect
	{
		private static int _screenHeight;

		private static int _screenWidth;

		private static float _currentAspect;

		public static readonly float TargetAspect;

		public static float CurrentAspect
		{
			get
			{
				if (_screenHeight != Screen.height || _screenWidth != Screen.width)
				{
					_screenHeight = Screen.height;
					_screenWidth = Screen.width;
					_currentAspect = _screenHeight > 0 ? (float)_screenWidth / _screenHeight : TargetAspect;
				}

				return _currentAspect;
			}
		}

		public static bool IsVertical
		{
			get
			{
				return CurrentAspect < TargetAspect;
			}
		}

		public static bool IsHorizontal
		{
			get
			{
				return TargetAspect < CurrentAspect;
			}
		}

		public static float CalculateVerticalFov(float currentFov)
		{
			return IsVertical ? CalculateFov(currentFov, TargetAspect, CurrentAspect) : currentFov;
		}

		public static float CalculateInvertVerticalFov(float currentFov)
		{
			return IsVertical ? CalculateFov(currentFov, CurrentAspect, TargetAspect) : currentFov;
		}

		static SekaiCameraAspect()
		{
			TargetAspect = 1.7778f;
			_screenHeight = -1;
			_screenWidth = -1;
			_currentAspect = TargetAspect;
		}

		private static float CalculateFov(float fov, float fromAspect, float toAspect)
		{
			if (toAspect <= 0f)
			{
				return fov;
			}

			var rad = fov * Mathf.Deg2Rad;
			return Mathf.Atan(Mathf.Tan(rad * 0.5f) * fromAspect / toAspect) * 2f * Mathf.Rad2Deg;
		}
	}
}
