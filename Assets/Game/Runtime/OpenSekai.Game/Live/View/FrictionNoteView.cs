using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FrictionNoteView : BaseNoteView
	{
		[SerializeField]
		private Sprite _damageBody;

		private Sprite _frictionBody;
		private bool _showingDamage;

		[SerializeField]
		protected SpriteRenderer centerIconRenderer;

		public override void Spawn(INote note, float posZ)
		{
			if (note is NextRushDamageNote && _damageBody != null)
			{
				if (!_showingDamage)
				{
					_frictionBody = spriteRenderer.sprite;
					spriteRenderer.sprite = _damageBody;
					if (centerIconRenderer != null) centerIconRenderer.enabled = false;
					_showingDamage = true;
				}
			}
			else if (_showingDamage)
			{
				spriteRenderer.sprite = _frictionBody;
				if (centerIconRenderer != null) centerIconRenderer.enabled = true;
				_showingDamage = false;
			}
			base.Spawn(note, posZ);
		}

		public override void Move(float musicTime)
		{
			if (note == null)
			{
				return;
			}

			if (!LiveConfig.IsKeyVisible(note, musicTime))
			{
				transform.localScale = Vector3.zero;
				return;
			}

			float progress = CalcProgress(musicTime);
			if (note is NoteBase native)
			{
				ApplyStyle(native.NativeStyle);
				if (native.IsNextRushConnectorHeadVisual && spriteRenderer != null)
					spriteRenderer.size = LiveUtility.CalcSpriteRendererSize(note.LaneEndF - note.LaneStartF);
			}
			transform.localScale = Vector3.one * progress;
			transform.localPosition = CalcNotePosition(ref progress);
			UpdateSpritePayload(progress);
			if (centerIconRenderer != null && spriteRenderer != null)
			{
				centerIconRenderer.color = spriteRenderer.color;
			}
		}

		protected override Vector3 CalcNotePosition(ref float progress)
		{
			if (note == null)
			{
				return Vector3.zero;
			}

			Vector2 center = GetLaneCenter(note.LaneStartF, note.LaneEndF);
			Vector2 effectiveSpawnPos = spawnPosition;
			if (note is NoteBase noteBase && noteBase.IsNegativeSpeed)
			{
				// 使用 spawnPosition 关于判定线的对称点作为负流速起点
				effectiveSpawnPos = 2f * center - spawnPosition;
				progress = Mathf.Min(progress, 1f);
			}
			Vector2 position = Vector2.LerpUnclamped(effectiveSpawnPos, center, progress);
			return new Vector3(position.x, position.y, posZ);
		}

		private float CalcProgress(float musicTime)
		{
			if (note == null)
			{
				return 0f;
			}

			return LiveConfig.GetNoteViewProgress(LiveConfig.ViewProgressOf(note, musicTime));
		}

		public FrictionNoteView()
		{
		}
	}
}
