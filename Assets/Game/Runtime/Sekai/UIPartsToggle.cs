using UnityEngine;

namespace Sekai
{
	public class UIPartsToggle : UIPartsToggleBase
	{
		[SerializeField]
		private GameObject _desableGroup;

		[SerializeField]
		private GameObject _offGroup;

		[SerializeField]
		private GameObject _onGroup;

		protected override void UpdateView(State state)
		{
			if (_desableGroup != null)
			{
				_desableGroup.SetActive(state == State.Disable);
			}
			if (_offGroup != null)
			{
				_offGroup.SetActive(state == State.Off);
			}
			if (_onGroup != null)
			{
				_onGroup.SetActive(state == State.On);
			}
		}

		public UIPartsToggle()
		{
		}
	}
}
