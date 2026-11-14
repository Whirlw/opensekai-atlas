using OpenSekai.Shared.Settings;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class JudgmentDescriptionView : MonoBehaviour
	{
		[SerializeField]
		private Sprite _fast;

		[SerializeField]
		private Sprite _late;

		[SerializeField]
		private Sprite _flickMiss;

		[SerializeField]
		private SpriteRenderer _spriteRenderer;

		private Dictionary<NoteResultDescription, Sprite> _sprites;

		private float _time;

		private bool _isFastLateFlick;

		private void Awake()
		{
			Hide();
		}

		private void Start()
		{
			_sprites = new Dictionary<NoteResultDescription, Sprite>
			{
				{ NoteResultDescription.Fast, _fast },
				{ NoteResultDescription.Late, _late },
				{ NoteResultDescription.FlickMiss, _flickMiss }
			};
			LiveSettingData liveSetting = LiveSettingsStore.Shared.Load();
			_isFastLateFlick = liveSetting == null || liveSetting.IsFastLateFlick;
			Hide();
		}

		public void Excute((NoteResult result, NoteResultDescription description) judgeInfo)
		{
			if (!_isFastLateFlick || judgeInfo.result < NoteResult.Bad || judgeInfo.result > NoteResult.Great)
			{
				return;
			}
			if (_sprites == null)
			{
				Start();
			}
			if (_spriteRenderer == null)
			{
				return;
			}
			_sprites.TryGetValue(judgeInfo.description, out Sprite sprite);
			_spriteRenderer.sprite = sprite;
			_time = 0f;
			_spriteRenderer.transform.localScale = Vector3.zero;
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

		public JudgmentDescriptionView()
		{
		}
	}
}
