using System.Collections.Generic;
using OpenSekai.Shared.Live;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class JudgmentView : MonoBehaviour
	{
		[SerializeField]
		private Sprite _miss;

		[SerializeField]
		private Sprite _bad;

		[SerializeField]
		private Sprite _good;

		[SerializeField]
		private Sprite _great;

		[SerializeField]
		private Sprite _perfect;

		[SerializeField]
		private Sprite _justPerfect;

		[SerializeField]
		private Sprite _auto;

		[SerializeField]
		private SpriteRenderer _spriteRenderer;

		private Dictionary<NoteResult, Sprite> _sprites;

		private float _time;

		private int _frame = -1;

		private NoteResult _frameResult;

		private void Awake()
		{
			Hide();
		}

		private void Start()
		{
			InitSprites();
			Hide();
		}

		private void InitSprites()
		{
			bool useFakePerfect = LiveSettingData.LoadFromStorage()?.UsesAutoFakePerfectMode ?? false;
			Sprite autoSprite = useFakePerfect ? _perfect : _auto;
			_sprites = new Dictionary<NoteResult, Sprite>
			{
				{ NoteResult.Miss, _miss },
				{ NoteResult.Bad, _bad },
				{ NoteResult.Good, _good },
				{ NoteResult.Great, _great },
				{ NoteResult.Perfect, _perfect },
				{ NoteResult.JustPerfect, _perfect },
				{ NoteResult.Auto, autoSprite }
			};
		}

		/// <summary>
		/// Shows the worst judgement of the current frame and reports whether this call owns the popup,
		/// so the fast/late caption follows the same note instead of the last one judged.
		/// </summary>
		public bool Excute((NoteResult result, NoteResultDescription description) judgeInfo)
		{
			if (_sprites == null)
			{
				InitSprites();
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

			if (_sprites == null || !_sprites.TryGetValue(_frameResult, out Sprite sprite) || _spriteRenderer == null)
			{
				return false;
			}

			_spriteRenderer.sprite = sprite;
			_time = 0f;
			_spriteRenderer.transform.localScale = Vector3.zero;
			return true;
		}

		private void Update()
		{
			if (_spriteRenderer == null)
			{
				return;
			}
			if (_time > 0.3f)
			{
				_spriteRenderer.transform.localScale = Vector3.zero;
				return;
			}
			float t = Mathf.Clamp01(_time * 20f);
			float inv = 1f - t;
			float scale = 1f - inv * inv * inv;
			_spriteRenderer.transform.localScale = Vector3.one * scale;
			_time += Time.deltaTime;
		}

		private void Hide()
		{
			_time = 1f;
			if (_spriteRenderer != null)
			{
				_spriteRenderer.transform.localScale = Vector3.zero;
			}
		}

		public JudgmentView()
		{
		}
	}
}
