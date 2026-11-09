using System.Collections.Generic;

namespace Sekai.Rendering
{
	public static class SekaiMusicItemSettings
	{
		private static readonly List<ISekaiMusicItem> _currentTransparentMusicItems = new List<ISekaiMusicItem>();

		public static void ClearTransparentMusicItem()
		{
			_currentTransparentMusicItems.Clear();
		}

		public static void RegisterTransparentMusicItem(ISekaiMusicItem musicItem)
		{
			if (musicItem != null && !_currentTransparentMusicItems.Contains(musicItem))
			{
				_currentTransparentMusicItems.Add(musicItem);
			}
		}

		public static void UnregisterTransparentMusicItem(ISekaiMusicItem musicItem)
		{
			if (musicItem != null)
			{
				_currentTransparentMusicItems.Remove(musicItem);
			}
		}

		public static bool ExistTransparentMusicItem()
		{
			return _currentTransparentMusicItems.Count > 0;
		}
	}
}
