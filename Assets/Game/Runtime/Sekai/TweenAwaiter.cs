using System;
using System.Runtime.CompilerServices;
using System.Threading;
using DG.Tweening;

namespace Sekai
{
	public struct TweenAwaiter : ICriticalNotifyCompletion, INotifyCompletion
	{
		private Tween _tween;

		private CancellationToken _cancellationToken;

		public bool IsCompleted
		{
			get
			{
				throw null;
			}
		}

		public TweenAwaiter(Tween _tween, CancellationToken _cancellationToken)
		{
			throw null;
		}

		public void GetResult()
		{
			throw null;
		}

		public void OnCompleted(Action continuation)
		{
			throw null;
		}

		public void UnsafeOnCompleted(Action continuation)
		{
			throw null;
		}

		public TweenAwaiter GetAwaiter()
		{
			throw null;
		}
	}
}
