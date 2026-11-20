using UnityEngine;

namespace Sekai
{
	public class ScreenLayerState
	{
				private ScreenLayerData _data;
		public ScreenLayerData Data
		{
		    get => _data;
		    set => _data = value;
		}
				private GameObject _refInstance;
		public GameObject RefInstance
		{
		    get => _refInstance;
		    set => _refInstance = value;
		}
				private ScreenLayer _screenLayer;
		public ScreenLayer ScreenLayer
		{
		    get => _screenLayer;
		    set => _screenLayer = value;
		}
	}
}
