using System.Collections.Generic;

namespace UiEffect
{
	public static class UiEffectListPool<T>
	{
		private static readonly UiEffectObjectPool<List<T>> _sListPool;

		public static List<T> Get()
		{
			return _sListPool.Get();
		}

		public static void Release(List<T> toRelease)
		{
			_sListPool.Release(toRelease);
		}

		static UiEffectListPool()
		{
			_sListPool = new UiEffectObjectPool<List<T>>(null, list => list.Clear());
		}
	}
}
