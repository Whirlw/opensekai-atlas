using UnityEngine;

namespace Sekai.Live
{
	/// <summary>A scored hazard. The input pass answers it once, after sampling the current contacts.</summary>
	public sealed class NextRushDamageNote : NoteBase
	{
		internal const int LifePenalty = 40;
		private PathPoint _path;
		public override bool IsVisible { get; }
		internal float InputStart { get; private set; }

		public NextRushDamageNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, bool hidden = false)
			: base(info, id, laneStart, laneEnd, NoteCategory.Friction, 1f, 0f)
		{
			IsVisible = !hidden;
			InputStart = info.time;
		}

		public void SetTickWindow(float start, PathPoint path)
		{
			InputStart = start;
			_path = path;
		}

		// Shared by ticks on the same chain. These bounds never follow a changing hold head.
		public sealed class PathPoint
		{
			internal readonly float Time, Left, Right;
			internal readonly int Ease;
			internal PathPoint Previous, Next;

			public PathPoint(float time, float left, float right, int ease)
			{
				Time = time;
				Left = left;
				Right = right;
				Ease = ease;
			}

			public void SetNeighbors(PathPoint previous, PathPoint next)
			{
				Previous = previous;
				Next = next;
			}
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime) =>
			AdvanceNote(currentFrameInfo, offsetTime, settleTimeout: false);

		internal bool Contains(float worldX, float time)
		{
			float left = LaneStartF - 0.5f, right = LaneEndF + 0.5f;
			var head = _path;
			while (head?.Previous != null && head.Time > time) head = head.Previous;
			if (head != null && head.Next == null) head = head.Previous;
			if (head?.Next is PathPoint tail)
			{
				float fraction = Mathf.InverseLerp(head.Time, tail.Time, time);
				float progress = NextRushConnectorSegment.Interpolate(head.Ease, fraction);
				left = Mathf.LerpUnclamped(head.Left, tail.Left, progress);
				right = Mathf.LerpUnclamped(head.Right, tail.Right, progress);
				if (right < left) left = right = (left + right) * 0.5f;
			}
			return LiveConfig.NativeLanePosition(left).x <= worldX
				&& worldX <= LiveConfig.NativeLanePosition(right).x;
		}

		internal void Resolve(bool touched)
		{
			JudgeInfo = (touched ? NoteResult.Miss : NoteResult.JustPerfect, NoteResultDescription.None);
			State = NoteState.Done;
		}
	}
}
