using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FlickNoteView : BaseNoteView
	{
		private const float ScrollCount = 2f;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("defaultArrowSprites")]
		private Sprite[] _defaultArrowSprites;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanArrowSprites")]
		private Sprite[] _slanArrowSprites;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("defaultArrow")]
		private SpriteRenderer _defaultArrow;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanArrow")]
		private SpriteRenderer _slanArrow;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("defaultMoveRoot")]
		private Transform _defaultMoveRoot;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanMoveRoot")]
		private Transform _slanMoveRoot;

		[SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("slanRoot")]
		private Transform _slanRoot;

		private SpriteRenderer currentArrow;

		private Transform currentMoveRoot;
		private bool _downOmni;

		public override void Spawn(INote note, float posZ)
		{
			base.Spawn(note, posZ);

			int widthIndex = Mathf.Clamp(note.LaneEnd - note.LaneStart, 0, Mathf.Max(0, (_defaultArrowSprites?.Length ?? 1) - 1));
			_downOmni = note.Direction == NoteDirection.Down;
			bool useSlan = note.Direction is not (NoteDirection.Default or NoteDirection.Down);
			bool showArrow = note is not (FlickNote { IsConnectorHeadView: true } or FrictionFlickNote { IsConnectorHeadView: true });
			SetArrow(_defaultArrow, _defaultArrowSprites, widthIndex, showArrow && !useSlan);
			SetArrow(_slanArrow, _slanArrowSprites, widthIndex, showArrow && useSlan);
			if (_defaultArrow != null)
			{
				_defaultArrow.flipY = _downOmni;
			}

			currentArrow = useSlan ? _slanArrow : _defaultArrow;
			currentMoveRoot = useSlan ? _slanMoveRoot : _defaultMoveRoot;

			if (_slanRoot != null && useSlan)
			{
				bool down = note.Direction is NoteDirection.DownLeft or NoteDirection.DownRight;
				bool rotateFromLeft = note.Direction is NoteDirection.Left or NoteDirection.DownRight;
				_slanRoot.localScale = rotateFromLeft ? LiveUtility.Vector3One : LiveUtility.Vector3OneMinusX;
				Vector3 angles = rotateFromLeft ? LiveUtility.FlickLeftEulerAngles : LiveUtility.FlickRightEulerAngles;
				if (down) angles.z += 180f;
				_slanRoot.localEulerAngles = angles;
			}
		}

		public override void Move(float musicTime)
		{
			base.Move(musicTime);
			UpdateFlickArrow(musicTime);
		}

		protected void UpdateFlickArrow(float musicTime)
		{
			if (currentMoveRoot == null || currentArrow == null)
			{
				return;
			}

			float t = Mathf.Repeat(musicTime * ScrollCount, 1f);
			float y = t * 2f;
			currentMoveRoot.localPosition = Vector3.up * (_downOmni ? -y : y);
			float alphaT = Mathf.Clamp01(y - 1f);
			currentArrow.color = Color.Lerp(ColorUtility.WHITE_ALPHA_1, ColorUtility.WHITE_ALPHA_0, alphaT);
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
