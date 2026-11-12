namespace Sekai
{
	public class HeaderUtility
	{
		public static void SetDisplayHeader(ScreenLayerData data) => ScreenManager.Instance?.Header?.SetDisplayHeader(data);
		public static void ShowBackUIScreen(bool isAnim = true) => ScreenManager.Instance?.Header?.ShowBackUIScreen(isAnim);
		public static void HideBackUIScreen(bool isAnim = true) => ScreenManager.Instance?.Header?.HideBackUIScreen(isAnim);
		public static void SetScreenSubNameText(string screenName) => ScreenManager.Instance?.Header?.SetScreenSubNameText(screenName);
		public static void ShowMenuButton(bool isAnim = true) => ScreenManager.Instance?.Header?.ShowMenuButton(isAnim);
		public static void HideMenuButton(bool isAnim = true) => ScreenManager.Instance?.Header?.HideMenuButton(isAnim);
		public static void HidePlayerInfo(bool isAnim = true) => ScreenManager.Instance?.Header?.HidePlayerInfo(isAnim);
		public static void ShowHeader(bool isAnim = true) => ScreenManager.Instance?.Header?.ShowHeader(isAnim);
		public static void HideHeader(bool isAnim = true) => ScreenManager.Instance?.Header?.HideHeader(isAnim);
		public static void SetEnableHeader(bool enable) => ScreenManager.Instance?.Header?.SetEnableHeader(enable);
	}
}
