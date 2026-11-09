using System;
using UnityEngine;

namespace Sekai
{
	public abstract class HeaderExtensionBase : MonoBehaviour
	{
				[SerializeField] private Action<HeaderExtensionBase> _OnInitialize;
		public Action<HeaderExtensionBase> OnInitialize
		{
		    get => _OnInitialize;
		    set => _OnInitialize = value;
		}

		public abstract HeaderCategory HeaderCategory { get; }

		public virtual void Initialize()
		{
			OnInitialize?.Invoke(this);
		}

		public virtual void UpdateInfo()
		{
		}
	}
}
