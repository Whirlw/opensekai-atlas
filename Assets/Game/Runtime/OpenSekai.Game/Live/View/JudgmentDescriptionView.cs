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
				{ NoteResultDescription.Fast, fast },
				{ NoteResultDescription.Late, late },
				{ NoteResultDescription.FlickMiss, flickMiss }
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
			if (spriteRenderer == null)
			{
				return;
			}
			_sprites.TryGetValue(judgeInfo.description, out Sprite sprite);
			spriteRenderer.sprite = sprite;
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
