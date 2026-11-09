using System;
using UnityEngine;

namespace Sekai.Core
{
	[Serializable]
	public struct MotionInfo
	{
		[SerializeField]
		[HideInInspector]
		private MotionType _motionType;

		[HideInInspector]
		[SerializeField]
		private int[] _uniqueCharacterIds;

		public MotionType MotionType
		{
			get
			{
				throw null;
			}
		}

		public int[] UniqueCharacterIds
		{
			get
			{
				throw null;
			}
		}
	}
}
