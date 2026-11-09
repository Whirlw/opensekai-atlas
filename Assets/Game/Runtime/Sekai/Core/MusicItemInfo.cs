using System;
using UnityEngine;

namespace Sekai.Core
{
	[Serializable]
	public struct MusicItemInfo
	{
		[SerializeField]
		[HideInInspector]
		private int _id;

		[SerializeField]
		[HideInInspector]
		private bool _useNonDefaultShader;

		public int Id
		{
			get
			{
				throw null;
			}
		}

		public bool UseNonDefaultShader
		{
			get
			{
				throw null;
			}
		}

		public MusicItemInfo(int _id = 1)
		{
			throw null;
		}
	}
}
