using System;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[Serializable]
	public class CommonMultiButtonDialogObjects
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_key")]
		[SerializeField]
		private string key;

		[UnityEngine.Serialization.FormerlySerializedAs("_dialogButton")]
		[SerializeField]
		private CustomButton dialogButton;

		[UnityEngine.Serialization.FormerlySerializedAs("_dialogButtonLabelMesh")]
		[SerializeField]
		private CustomTextMesh dialogButtonLabelMesh;

		public string Key => key;
		public CustomButton DialogButton => dialogButton;
		public CustomTextMesh DialogButtonLabelMesh => dialogButtonLabelMesh;
	}
}
