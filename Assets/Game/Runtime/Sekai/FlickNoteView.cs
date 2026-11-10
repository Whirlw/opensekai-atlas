using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FlickNoteView : BaseNoteView
	{
		protected static readonly float ScrollCount;

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

		protected SpriteRenderer currentArrow;

		protected Transform currentMoveRoot;

		public override void Spawn(INote note, float posZ)
		{
			base.Spawn(note, posZ);

			int widthIndex = Mathf.Clamp(note.LaneEnd - note.LaneStart, 0, Mathf.Max(0, (_defaultArrowSprites?.Length ?? 1) - 1));
			bool useSlan = note.Direction != NoteDirection.Default;
			SetArrow(_defaultArrow, _defaultArrowSprites, widthIndex, !useSlan);
			SetArrow(_slanArrow, _slanArrowSprites, widthIndex, useSlan);

			currentArrow = useSlan ? _slanArrow : _defaultArrow;
			currentMoveRoot = useSlan ? _slanMoveRoot : _defaultMoveRoot;

			Transform directionRoot = _slanRoot;
			if (directionRoot != null)
			{
				if (note.Direction == NoteDirection.Left)
				{
					directionRoot.localScale = LiveUtility.Vector3One;
					directionRoot.localEulerAngles = LiveUtility.FlickLeftEulerAngles;
				}
				else
				{
					directionRoot.localScale = LiveUtility.Vector3OneMinusX;
					directionRoot.localEulerAngles = LiveUtility.FlickRightEulerAngles;
				}
			}
		}

		public override void Move(float musicTime)
		{
			base.Move(musicTime);
			if (currentMoveRoot == null || currentArrow == null)
			{
				return;
			}

			float t = Mathf.Repeat(musicTime * ScrollCount, 1f);
			float y = t * 2f;
			currentMoveRoot.localPosition = Vector3.up * y;
			float alphaT = Mathf.Clamp01(y - 1f);
			currentArrow.color = Color.Lerp(ColorUtility.WHITE_ALPHA_1, ColorUtility.WHITE_ALPHA_0, alphaT);
		}

		public FlickNoteView()
		{
		}

		static FlickNoteView()
		{
			ScrollCount = 2f;
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
