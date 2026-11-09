using System;
using Beebyte.Obfuscator;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public abstract class UIPartsToggleBase : MonoBehaviour
	{
		public enum State
		{
			Disable = 0,
			Off = 1,
			On = 2
		}

		[SerializeField]
		private SeType _seToggleOn;

		[SerializeField]
		private SeType _seToggleOff;

		private State _currentState;

				[SerializeField] private Action _OnToggleOn;
		public Action OnToggleOn
		{
		    get => _OnToggleOn;
		    set => _OnToggleOn = value;
		}

				[SerializeField] private Action _OnToggleOff;
		public Action OnToggleOff
		{
		    get => _OnToggleOff;
		    set => _OnToggleOff = value;
		}

		public State ToggleState
		{
			get
			{
				return _currentState;
			}
		}

		public virtual void Setup(State state = State.Disable)
		{
			_currentState = state;
			UpdateView(_currentState);
		}

		[Skip]
		public void OnClick()
		{
			if (_currentState == State.Disable)
			{
				return;
			}
			if (_currentState == State.On)
			{
				ToggleOff();
			}
			else
			{
				ToggleOn();
			}
		}

		public void ToggleOn()
		{
			_currentState = State.On;
			UpdateView(_currentState);
			OnToggleOn?.Invoke();
		}

		public void ToggleOff()
		{
			_currentState = State.Off;
			UpdateView(_currentState);
			OnToggleOff?.Invoke();
		}

		public void ToggleDisable()
		{
			_currentState = State.Disable;
			UpdateView(_currentState);
		}

		protected abstract void UpdateView(State state);

		protected UIPartsToggleBase()
		{
		}
	}
}
