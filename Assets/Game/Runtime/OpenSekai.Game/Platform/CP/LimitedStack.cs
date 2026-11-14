using System;
using System.Collections.Generic;

namespace CP
{
	public class LimitedStack<T>
	{
		private readonly List<T> _list;
		private readonly int _maxStacks;

		public int Count => _list.Count;

		public T this[int index] => _list[index];

		public LimitedStack(int _maxStacks)
		{
			this._maxStacks = Math.Max(1, _maxStacks);
			_list = new List<T>(this._maxStacks);
		}

		public void Push(T obj)
		{
			_list.Add(obj);
			while (_list.Count > _maxStacks)
			{
				_list.RemoveAt(0);
			}
		}

		public T Pop()
		{
			if (_list.Count == 0)
			{
				return default;
			}

			var index = _list.Count - 1;
			var value = _list[index];
			_list.RemoveAt(index);
			return value;
		}

		public T Peek()
		{
			return _list.Count > 0 ? _list[_list.Count - 1] : default;
		}

		public void Clear()
		{
			_list.Clear();
		}
	}
}
