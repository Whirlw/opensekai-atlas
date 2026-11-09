using System.Collections.Generic;

namespace UiEffect
{
	public static class UiEffectListPool<T>
	{
		private static readonly UiEffectObjectPool<List<T>> _s_ListPool;

		public static List<T> Get()
		{
			return _s_ListPool.Get();
		}

		public static void Release(List<T> toRelease)
		{
			_s_ListPool.Release(toRelease);
		}

		static UiEffectListPool()
		{
			_s_ListPool = new UiEffectObjectPool<List<T>>(null, list => list.Clear());
		}
	}
}
