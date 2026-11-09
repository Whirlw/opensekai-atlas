using UnityEngine;

namespace Sekai.Core.Live
{
	public class SkillTextViewSkillTypeSprite : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _baseSprite;

		[SerializeField]
		private SpriteRenderer _icon;

		[SerializeField]
		private SpriteRenderer _text;

		[SerializeField]
		private SpriteRenderer _level;

		[SerializeField]
		private SpriteRenderer _levelShadow;

		public void Initialize(Sprite _baseSprite, Sprite iconSprite, Sprite textSprite, Sprite levelSprite, Sprite levelShadowSprite)
		{
			SetSprite(this._baseSprite, _baseSprite);
			SetSprite(_icon, iconSprite);
			SetSprite(_text, textSprite);
			SetSprite(_level, levelSprite);
			SetSprite(_levelShadow, levelShadowSprite);
		}

		public SkillTextViewSkillTypeSprite()
		{
		}

		private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
		{
			if (renderer == null)
			{
				return;
			}

			renderer.sprite = sprite;
			renderer.enabled = sprite != null;
		}
	}
}
