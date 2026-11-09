using System;
using UnityEngine;
using Sekai;

namespace Sekai.Core.Live
{
	public class SkillSpriteReferences : MonoBehaviour
	{
		[Serializable]
		public class SkillSprite
		{
			[SerializeField]
			private Sprite _baseSprite;

			[SerializeField]
			private Sprite _iconSprite;

			[SerializeField]
			private Sprite _textSprite;

			[SerializeField]
			private Sprite _effectPostfixSprites;

			public Sprite BaseSprite
			{
				get
				{
					return _baseSprite;
				}
			}

			public Sprite IconSprite
			{
				get
				{
					return _iconSprite;
				}
			}

			public Sprite TextSprite
			{
				get
				{
					return _textSprite;
				}
			}

			public SkillSprite()
			{
			}
		}

		[Serializable]
		public class LevelSprite
		{
			[SerializeField]
			private Sprite _textSprite;

			[SerializeField]
			private Sprite _shadowSprite;

			public Sprite TextSprite
			{
				get
				{
					return _textSprite;
				}
			}

			public Sprite ShadowSprite
			{
				get
				{
					return _shadowSprite;
				}
			}

			public LevelSprite()
			{
			}
		}

		[SerializeField]
		private LevelSprite[] _levelSprites;

		[SerializeField]
		private SkillSprite[] _skillSprites;

		public SkillSprite GetSkillSprite(SkillSpriteType spriteType)
		{
			int index = (int)spriteType;
			if (_skillSprites == null || _skillSprites.Length == 0)
			{
				return null;
			}

			if (index < 0 || index >= _skillSprites.Length)
			{
				index = 0;
			}

			return _skillSprites[index];
		}

		public LevelSprite GetLevelSprite(int skillLevel)
		{
			if (_levelSprites == null || _levelSprites.Length == 0)
			{
				return null;
			}

			int index = Mathf.Clamp(skillLevel - 1, 0, _levelSprites.Length - 1);
			return _levelSprites[index];
		}

		public SkillSpriteReferences()
		{
		}
	}
}
