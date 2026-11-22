using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FlickNoteView : BaseNoteView
	{
		private const float SCROLL_COUNT = 2f;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultArrowSprites")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("defaultArrowSprites")]
		private Sprite[] defaultArrowSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_slanArrowSprites")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanArrowSprites")]
		private Sprite[] slanArrowSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultArrow")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("defaultArrow")]
		private SpriteRenderer defaultArrow;

		[UnityEngine.Serialization.FormerlySerializedAs("_slanArrow")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanArrow")]
		private SpriteRenderer slanArrow;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultMoveRoot")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("defaultMoveRoot")]
		private Transform defaultMoveRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_slanMoveRoot")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanMoveRoot")]
		private Transform slanMoveRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_slanRoot")]
		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanRoot")]
		private Transform slanRoot;

		private SpriteRenderer _currentArrow;

		private Transform _currentMoveRoot;
		private bool _downOmni;

		public override void Spawn(INote note, float posZ)
		{
			base.Spawn(note, posZ);

			int widthIndex = note is NoteBase { IsNextRush: true }
				? Mathf.RoundToInt(note.LaneEndF - note.LaneStartF + 1f) - 1
				: note.LaneEnd - note.LaneStart;
			_downOmni = note.Direction == NoteDirection.Down;
			bool useSlan = note.Direction is not (NoteDirection.Default or NoteDirection.Down);
			bool showArrow = note is not NoteBase { IsNextRushConnectorHeadVisual: true };
			SetArrow(defaultArrow, defaultArrowSprites, widthIndex, showArrow && !useSlan);
			SetArrow(slanArrow, slanArrowSprites, widthIndex, showArrow && useSlan);
			if (defaultArrow != null)
			{
				defaultArrow.flipY = _downOmni;
			}

			_currentArrow = useSlan ? slanArrow : defaultArrow;
			_currentMoveRoot = useSlan ? slanMoveRoot : defaultMoveRoot;

			if (slanRoot != null && useSlan)
			{
				bool down = note.Direction is NoteDirection.DownLeft or NoteDirection.DownRight;
				bool rotateFromLeft = note.Direction is NoteDirection.Left or NoteDirection.DownRight;
				slanRoot.localScale = rotateFromLeft ? LiveUtility.Vector3One : LiveUtility.Vector3OneMinusX;
				Vector3 angles = rotateFromLeft ? LiveUtility.FlickLeftEulerAngles : LiveUtility.FlickRightEulerAngles;
				if (down) angles.z += 180f;
				slanRoot.localEulerAngles = angles;
			}
		}

		public override void Move(float musicTime)
		{
			base.Move(musicTime);
			UpdateFlickArrow(musicTime);
		}

		protected void UpdateFlickArrow(float musicTime)
		{
			if (_currentMoveRoot == null || _currentArrow == null)
			{
				return;
			}

			float t = Mathf.Repeat(musicTime * SCROLL_COUNT, 1f);
			if (note is NoteBase { IsNextRush: true })
			{
				bool down = note.Direction >= NoteDirection.Down;
				float distance = down ? 1f - t : t;
				float horizontal = note.Direction switch
				{
					NoteDirection.Left or NoteDirection.DownRight => -1f,
					NoteDirection.Right or NoteDirection.DownLeft => 1f,
					_ => 0f
				};
				var offset = new Vector3(horizontal, 2f, 0f) * distance;
				_currentMoveRoot.localPosition = _currentMoveRoot == slanMoveRoot && slanRoot != null
					? Vector3.Scale(Quaternion.Inverse(slanRoot.localRotation) * offset, slanRoot.localScale)
					: offset;
				_currentArrow.color = new Color(1f, 1f, 1f, 1f - t * t * t);
				return;
			}

			float y = t * 2f;
			_currentMoveRoot.localPosition = Vector3.up * (_downOmni ? -y : y);
			float alphaT = Mathf.Clamp01(y - 1f);
			_currentArrow.color = Color.Lerp(ColorUtility.WHITE_ALPHA_1, ColorUtility.WHITE_ALPHA_0, alphaT);
		}

		public FlickNoteView()
		{
		}

		private static void SetArrow(SpriteRenderer renderer, Sprite[] sprites, int index, bool visible)
		{
			if (renderer == null)
			{
				return;
			}

			renderer.transform.localScale = visible ? Vector3.one : Vector3.zero;
			if (sprites != null && sprites.Length > 0)
			{
				renderer.sprite = sprites[Mathf.Clamp(index, 0, sprites.Length - 1)];
			}
		}
	}
}
