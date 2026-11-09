using UnityEngine;

namespace Sekai
{
	public class ScreenLayerState
	{
				private ScreenLayerData _Data;
		public ScreenLayerData Data
		{
		    get => _Data;
		    set => _Data = value;
		}
				private GameObject _RefInstance;
		public GameObject RefInstance
		{
		    get => _RefInstance;
		    set => _RefInstance = value;
		}
				private ScreenLayer _ScreenLayer;
		public ScreenLayer ScreenLayer
		{
		    get => _ScreenLayer;
		    set => _ScreenLayer = value;
		}
	}
}
