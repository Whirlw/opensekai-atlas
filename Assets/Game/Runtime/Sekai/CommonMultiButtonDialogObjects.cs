using System;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[Serializable]
	public class CommonMultiButtonDialogObjects
	{
		[SerializeField]
		private string _key;

		[SerializeField]
		private CustomButton _dialogButton;

		[SerializeField]
		private CustomTextMesh _dialogButtonLabelMesh;

		public string Key => _key;
		public CustomButton DialogButton => _dialogButton;
		public CustomTextMesh DialogButtonLabelMesh => _dialogButtonLabelMesh;
	}
}
