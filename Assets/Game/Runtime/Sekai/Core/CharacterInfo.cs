using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sekai.Core
{
	[Serializable]
	public struct CharacterInfo
	{
		[HideInInspector]
		[SerializeField]
		private int _id;

		[HideInInspector]
		[SerializeField]
		private string _headOptional;

		[HideInInspector]
		[SerializeField]
		private string _face;

		[HideInInspector]
		[SerializeField]
		private string _body;

		[HideInInspector]
		[SerializeField]
		private string _colorVariation;

		[HideInInspector]
		[SerializeField]
		private string _headOptionalColorVariation;

		[HideInInspector]
		[SerializeField]
		private ModelPrefabType _prefabType;

		[SerializeField]
		[HideInInspector]
		private bool _isHeelOffset;

		[HideInInspector]
		[SerializeField]
		private float _defaultHeelOffset;

		[SerializeField]
		[HideInInspector]
		private MotionInfo _motionInfo;

		[HideInInspector]
		[SerializeField]
		private MusicItemInfo[] _musicItemInfos;

		[HideInInspector]
		[SerializeField]
		public bool _useHairShadow;

		[SerializeField]
		[HideInInspector]
		private bool _disinheritCharacterInfo;

		[SerializeField]
		[HideInInspector]
		private bool _isLoadInActive;

		[HideInInspector]
		[SerializeField]
		private bool _isInsertCharacter;

		[HideInInspector]
		[SerializeField]
		private bool _isUseSpringBoneController;

		public int Id
		{
			get
			{
				throw null;
			}
		}

		public string HeadOptional
		{
			get
			{
				throw null;
			}
		}

		public string Face
		{
			get
			{
				throw null;
			}
		}

		public string Body
		{
			get
			{
				throw null;
			}
		}

		public string ColorVariation
		{
			get
			{
				throw null;
			}
		}

		public string HeadOptionalColorVariation
		{
			get
			{
				throw null;
			}
		}

		public ModelPrefabType PrefabType
		{
			get
			{
				throw null;
			}
		}

		public FaceModelType FaceModelType
		{
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool IsHeelOffset
		{
			get
			{
				throw null;
			}
		}

		public float DefaultHeelOffset
		{
			get
			{
				throw null;
			}
		}

		public MotionInfo MotionInfo
		{
			get
			{
				throw null;
			}
		}

		public MusicItemInfo[] MusicItemInfos
		{
			get
			{
				throw null;
			}
		}

		public bool UseHairShadow
		{
			get
			{
				throw null;
			}
		}

		public bool DisinheritCharacterInfo
		{
			get
			{
				throw null;
			}
		}

		public bool IsLoadInActive
		{
			get
			{
				throw null;
			}
		}

		public bool IsInsertCharacter
		{
			get
			{
				throw null;
			}
		}

		public bool IsUseSpringBoneController
		{
			get
			{
				throw null;
			}
		}

		public CharacterInfo(int _id = 1)
		{
			throw null;
		}

		public void SetDefaultHeelOffset(float heelOffset)
		{
			throw null;
		}
	}
}
