using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Events;

namespace UiEffect
{
	public class UiEffectObjectPool<T> where T : new()
	{
		private readonly Stack<T> _stack;

		private readonly UnityAction<T> _actionOnGet;

		private readonly UnityAction<T> _actionOnRelease;

		public int countAll
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public int countActive
		{
			get
			{
				return countAll - countInactive;
			}
		}

		public int countInactive
		{
			get
			{
				return _stack.Count;
			}
		}

		public UiEffectObjectPool(UnityAction<T> actionOnGet, UnityAction<T> actionOnRelease)
		{
			_stack = new Stack<T>();
			_actionOnGet = actionOnGet;
			_actionOnRelease = actionOnRelease;
		}

		public T Get()
		{
			T element;
			if (_stack.Count == 0)
			{
				element = new T();
				countAll++;
			}
			else
			{
				element = _stack.Pop();
			}

			_actionOnGet?.Invoke(element);
			return element;
		}

		public void Release(T element)
		{
			if (_stack.Count > 0 && ReferenceEquals(_stack.Peek(), element))
			{
				return;
			}

			_actionOnRelease?.Invoke(element);
			_stack.Push(element);
		}
	}
}
