using System.Collections.Generic;
using UnityEngine;

namespace Sekai
{
	public class ScreenLayerAssetRemoving : ScreenLayer
	{
		private readonly HashSet<Object> _holdObjects = new HashSet<Object>();
		private int _removeAssetNum;
		private int _removedCount;

		public void Setup(int _removeAssetNum)
		{
			this._removeAssetNum = Mathf.Max(0, _removeAssetNum);
			_removedCount = 0;
		}

		public void ResetGauge()
		{
			_removedCount = 0;
		}

		public void UpdateGaugeProgress(int removeNum)
		{
			// TODO(original): drive the copied gauge view once its view scripts are restored.
			_removedCount = Mathf.Clamp(removeNum, 0, _removeAssetNum);
		}

		public void Release(Object holdObj)
		{
			if (holdObj != null)
			{
				_holdObjects.Remove(holdObj);
			}
		}

		public void Hold(Object holdObj)
		{
			if (holdObj != null)
			{
				_holdObjects.Add(holdObj);
			}
		}
	}
}
