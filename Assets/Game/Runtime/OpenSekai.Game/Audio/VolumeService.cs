using UnityEngine;

namespace Sekai
{
	internal sealed class VolumeService
	{
		private float _master = 1f;
		private float _bgm = 1f;
		private float _se = 1f;

		internal float Bgm => _master * _bgm;
		internal float SoundEffect => _master * _se;

		internal void Set(float master, float bgm, float se)
		{
			_master = Mathf.Clamp01(master);
			_bgm = Mathf.Clamp01(bgm);
			_se = Mathf.Clamp01(se);
		}
	}
}
