using OpenSekai.Shared.Settings;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class JudgmentDescriptionView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_fast")]
		[SerializeField]
		private Sprite fast;

		[UnityEngine.Serialization.FormerlySerializedAs("_late")]
		[SerializeField]
		private Sprite late;

		[UnityEngine.Serialization.FormerlySerializedAs("_flickMiss")]
		[SerializeField]
		private Sprite flickMiss;

		[UnityEngine.Serialization.FormerlySerializedAs("_spriteRenderer")]
		[SerializeField]
		private SpriteRenderer spriteRenderer;

		private Dictionary<NoteResultDescription, (Sprite builtin, MaterialGlyph glyph)> _sprites;

		private SpriteRendererBaseline _baseline;

		private float _time;

		private void Awake()
		{
			Hide();
		}

		private void Start()
		{
			if (_sprites == null) Setup(null, LiveSettingsStore.Shared.Load());
		}

		public void Setup(LiveSpriteSkin skin, LiveSettingData settings)
		{
			if (spriteRenderer != null) _baseline ??= new SpriteRendererBaseline(spriteRenderer);
			_sprites = new Dictionary<NoteResultDescription, (Sprite, MaterialGlyph)>(3);
			if (settings?.ShowFastLate == true)
			{
				_sprites.Add(NoteResultDescription.Fast, Bind("judgment.fast", fast));
				_sprites.Add(NoteResultDescription.Late, Bind("judgment.late", late));
			}
			if (settings?.ShowFlickMiss == true)
			{
				_sprites.Add(NoteResultDescription.FlickMiss, Bind("judgment.flickMiss", flickMiss));
			}
			Hide();

			(Sprite, MaterialGlyph) Bind(string slot, Sprite builtin) => (builtin, skin?.Resolve(slot, builtin));
		}

		public void Excute((NoteResult result, NoteResultDescription description) judgeInfo)
		{
			if (_sprites == null)
			{
				Start();
			}
			if (spriteRenderer == null)
			{
				return;
			}
			if (judgeInfo.result is not (NoteResult.Great or NoteResult.Good or NoteResult.Bad)
				|| !_sprites.TryGetValue(judgeInfo.description, out var entry))
			{
				Hide();
				return;
			}
			// Unity converts renderer dimensions when switching Simple/Sliced and needs a nonzero scale.
			spriteRenderer.transform.localScale = Vector3.one;
			_baseline.Draw(spriteRenderer, entry.glyph, entry.builtin);
			_time = 0f;
			spriteRenderer.transform.localScale = Vector3.zero;
		}

		private void Update()
		{
			if (spriteRenderer == null)
			{
				return;
			}
			if (_time > 0.3f)
			{
				spriteRenderer.transform.localScale = Vector3.zero;
				return;
			}
			float t = Mathf.Clamp01(_time * 20f);
			float inv = 1f - t;
			float scale = 1f - inv * inv * inv;
			spriteRenderer.transform.localScale = Vector3.one * scale;
			_time += Time.deltaTime;
		}

		private void Hide()
		{
			_time = 1f;
			if (spriteRenderer != null)
			{
				spriteRenderer.sprite = null;
				spriteRenderer.transform.localScale = Vector3.zero;
			}
		}

		public JudgmentDescriptionView()
		{
		}
	}
}
