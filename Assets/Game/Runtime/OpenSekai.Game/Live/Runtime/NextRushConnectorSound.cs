using System.Collections.Generic;
using UnityEngine;

namespace Sekai.Live
{
	// One activeHead/activeTail interval. Several geometry edges may move the input bounds.
	public sealed class NextRushConnectorSound
	{
		internal readonly NoteBase Head, Tail, AutoStyle;
		internal readonly int ManualChannel, AutoChannel;
		internal readonly bool FakeActive;
		private readonly LongNote[] _bars;
		private readonly NoteBase _firstEnd;
		private readonly bool _legacy;
		public float Start => Head.MusicScoreInfo.time;
		internal float End => Tail.MusicScoreInfo.time;

		public NextRushConnectorSound(NoteBase head, NoteBase tail, NoteBase firstEnd, NoteBase autoStyle, bool legacy, int manualKind,
			int autoKind, LongNote[] bars)
		{
			Head = head;
			Tail = tail;
			AutoStyle = autoStyle;
			_firstEnd = firstEnd;
			_legacy = legacy;
			ManualChannel = Channel(manualKind);
			AutoChannel = Channel(autoKind);
			FakeActive = manualKind >= 51 && manualKind <= 78 && ManualChannel >= 0;
			_bars = bars;
		}

		internal double SpawnTime(double lead)
		{
			if (_legacy) return System.Math.Min(Start, AutoStyle.NativeScroll.LegacyConnectorSpawnTime(AutoStyle.NativeTarget, lead));
			return System.Math.Min(Start, System.Math.Min(Head.PrepareNativeVisual(lead), _firstEnd.PrepareNativeVisual(lead)));
		}

		public static int Channel(int kind)
		{
			if (kind >= 51 && kind <= 88) kind -= 50;
			if (kind == 1 || kind >= 11 && kind <= 18) return 0;
			if (kind == 2 || kind >= 21 && kind <= 28) return 1;
			return -1;
		}

		internal bool HasContact(float now, List<float> contacts, bool atHead = false)
		{
			var point = now >= End && !atHead ? Tail : Head;
			float left = point.NativeLeft, right = point.NativeRight;
			float leniency = atHead ? .75f : 1f;
			if (now >= Start && now < End && !atHead)
			{
				// On a shared head the last active connector writes the input bounds.
				for (int i = _bars.Length - 1; i >= 0; i--)
				{
					if (!_bars[i].TryNativeEdge(now, out var a, out var b)) continue;
					float t = a.NativeConnector.Interpolate(Mathf.InverseLerp(a.MusicScoreInfo.time, b.MusicScoreInfo.time, now));
					left = Mathf.LerpUnclamped(a.NativeLeft, b.NativeLeft, t);
					right = Mathf.LerpUnclamped(a.NativeRight, b.NativeRight, t);
					leniency = a.NativeConnector.IsDamage ? 0 : 1;
					break;
				}
			}
			float min = left - .5f - leniency, max = right + .5f + leniency;
			if (leniency > 0 && left >= 0 && right <= 11)
			{
				const float outerMargin = 1.8f / 1.19f;
				if (min <= 0) min = -outerMargin;
				if (max >= 11) max = 11 + outerMargin;
			}
			float xMin = LiveConfig.NativeLanePosition(min).x;
			float xMax = LiveConfig.NativeLanePosition(max).x;
			foreach (float x in contacts)
				if (x >= xMin && x <= xMax) return true;
			return false;
		}
	}
}
