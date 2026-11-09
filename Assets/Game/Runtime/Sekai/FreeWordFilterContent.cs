using System;
using System.Runtime.CompilerServices;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class FreeWordFilterContent : MonoBehaviour
	{
		[SerializeField]
		private CustomInputFieldTextMesh _inputField;

		[SerializeField]
		private GameObject _searchIconObject;

		[SerializeField]
		private CustomButton _clearTextButton;

				[SerializeField] private Action<string> _OnFiltered;
		public Action<string> OnFiltered
		{
		    get => _OnFiltered;
		    set => _OnFiltered = value;
		}

		public string FreeWord
		{
			get
			{
				throw null;
			}
		}

		public bool IsFiltered
		{
			get
			{
				throw null;
			}
		}

		public bool IsInputFieldTextEmptyHide
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		private void Awake()
		{
			throw null;
		}

		public void SetEnable(bool enable)
		{
			throw null;
		}

		private void SwitchActiveSearchIconAndClearTextButton(bool isInputTextEmpty)
		{
			throw null;
		}

		private void OnValueChanged(string text)
		{
			throw null;
		}

		private void SetActiveTextComponentIfNeeded(string text)
		{
			throw null;
		}

		private void OnEndEdit(string text)
		{
			throw null;
		}

		public void Clear()
		{
			throw null;
		}

		public void SetText(string text)
		{
			throw null;
		}

		public FreeWordFilterContent()
		{
			throw null;
		}
	}
}
