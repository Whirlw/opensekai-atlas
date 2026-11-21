using OpenSekai.Shared.Settings;
using System.Collections.Generic;
using OpenSekai.Shared.Live;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class JudgmentView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_miss")]
		[SerializeField]
		private Sprite miss;

		[UnityEngine.Serialization.FormerlySerializedAs("_bad")]
		[SerializeField]
		private Sprite bad;

		[UnityEngine.Serialization.FormerlySerializedAs("_good")]
		[SerializeField]
		private Sprite good;

		[UnityEngine.Serialization.FormerlySerializedAs("_great")]
		[SerializeField]
		private Sprite great;

		[UnityEngine.Serialization.FormerlySerializedAs("_perfect")]
		[SerializeField]
		private Sprite perfect;

		[UnityEngine.Serialization.FormerlySerializedAs("_justPerfect")]
		[SerializeField]
		private Sprite justPerfect;

		[UnityEngine.Serialization.FormerlySerializedAs("_auto")]
		[SerializeField]
		private Sprite auto;

		[UnityEngine.Serialization.FormerlySerializedAs("_spriteRenderer")]
		[SerializeField]
		private SpriteRenderer spriteRenderer;

		private Dictionary<NoteResult, (Sprite builtin, MaterialGlyph glyph)> _sprites;

		private SpriteRendererBaseline _baseline;

		private float _time;

		private int _frame = -1;

		private NoteResult _frameResult;

		private void Awake()
		{
			Hide();
		}

		private void Start()
		{
			if (_sprites == null) Setup(null, LiveSettingsStore.Shared.Load()?.UsesAutoFakePerfectMode ?? false);
		}

		public void Setup(LiveSpriteSkin skin, bool useFakePerfect)
		{
			if (spriteRenderer != null) _baseline ??= new SpriteRendererBaseline(spriteRenderer);
			_frame = -1;
			_sprites = new Dictionary<NoteResult, (Sprite, MaterialGlyph)>
			{
				{ NoteResult.Miss, Bind("judgment.miss", miss) },
				{ NoteResult.Bad, Bind("judgment.bad", bad) },
				{ NoteResult.Good, Bind("judgment.good", good) },
				{ NoteResult.Great, Bind("judgment.great", great) },
				{ NoteResult.Perfect, Bind("judgment.perfect", perfect) },
				{ NoteResult.JustPerfect, Bind("judgment.perfect", perfect) },
				{ NoteResult.Auto, useFakePerfect ? Bind("judgment.perfect", perfect) : Bind("judgment.auto", auto) },
			};
			Hide();

			(Sprite, MaterialGlyph) Bind(string slot, Sprite builtin) => (builtin, skin?.Resolve(slot, builtin));
		}

		/// <summary>
		/// Shows the worst judgement of the current frame and reports whether this call owns the popup,
		/// so the fast/late caption follows the same note instead of the last one judged.
		/// </summary>
		public bool Excute((NoteResult result, NoteResultDescription description) judgeInfo)
		{
			if (_sprites == null)
			{
				Start();
			}
			if (Time.frameCount != _frame)
			{
				_frame = Time.frameCount;
				_frameResult = judgeInfo.result;
			}
			else
			{
				NoteResult worst = JudgmentAggregator.Worse(_frameResult, judgeInfo.result);
				if (worst == _frameResult && judgeInfo.result != _frameResult)
				{
					// A better result than the frame's worst (or an Auto) must not repaint the popup.
					return false;
				}

				_frameResult = worst;
			}

			if (_sprites == null || !_sprites.TryGetValue(_frameResult, out var entry) || spriteRenderer == null)
			{
				return false;
			}

			// Unity converts renderer dimensions when switching Simple/Sliced and needs a nonzero scale.
			spriteRenderer.transform.localScale = Vector3.one;
			_baseline.Draw(spriteRenderer, entry.glyph, entry.builtin);
			_time = 0f;
			spriteRenderer.transform.localScale = Vector3.zero;
			return true;
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

		public JudgmentView()
		{
		}
	}
}
