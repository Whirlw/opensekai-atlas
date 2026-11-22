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

		private bool _isFastLateFlick;

		private void Awake()
		{
			Hide();
		}

		private void Start()
		{
			if (_sprites == null) Setup(null, LiveSettingsStore.Shared.Load()?.IsFastLateFlick ?? true);
		}

		public void Setup(LiveSpriteSkin skin, bool showTiming)
		{
			if (spriteRenderer != null) _baseline ??= new SpriteRendererBaseline(spriteRenderer);
			_isFastLateFlick = showTiming;
			_sprites = new Dictionary<NoteResultDescription, (Sprite, MaterialGlyph)>
			{
				{ NoteResultDescription.Fast, Bind("judgment.fast", fast) },
				{ NoteResultDescription.Late, Bind("judgment.late", late) },
				{ NoteResultDescription.FlickMiss, Bind("judgment.flickMiss", flickMiss) },
			};
			Hide();

			(Sprite, MaterialGlyph) Bind(string slot, Sprite builtin) => (builtin, skin?.Resolve(slot, builtin));
		}

		public void Excute((NoteResult result, NoteResultDescription description) judgeInfo)
		{
			if (_sprites == null)
			{
				Start();
			}
			if (!_isFastLateFlick || judgeInfo.result < NoteResult.Bad || judgeInfo.result > NoteResult.Great)
			{
				return;
			}
			if (spriteRenderer == null)
			{
				return;
			}
			_sprites.TryGetValue(judgeInfo.description, out var entry);
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
				spriteRenderer.transform.localScale = Vector3.zero;
			}
		}

		public JudgmentDescriptionView()
		{
		}
	}
}
