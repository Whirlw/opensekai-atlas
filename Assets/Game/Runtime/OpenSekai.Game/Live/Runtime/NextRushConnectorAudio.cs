using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Live;

namespace Sekai.Core.Live
{
	internal sealed class NextRushConnectorAudio
	{
		private struct ManualHold
		{
			internal NextRushConnectorSound Sound;
			internal bool Ready, TouchedHead, Held, HasSlot;
		}

		private readonly List<ManualHold> _pending = new();
		private readonly HashSet<int> _sampledFingers = new();
		private readonly List<float> _contacts = new();
		private readonly NextRushConnectorSound[] _owner = new NextRushConnectorSound[2];
		private readonly NextRushConnectorSound[] _playing = new NextRushConnectorSound[2];
		private readonly uint[] _handles = new uint[2];
		private NextRushConnectorSound[] _sounds;
		private float _previous = float.NegativeInfinity;
		private int _cursor;
		private (double spawn, NextRushConnectorSound sound)[] _manualOrder;
		private readonly int[] _reserved = new int[2];
		internal Func<string, uint> Play = cue =>
		{
			var sound = SoundManager.Instance;
			return sound.PlayHoldLoop(sound.ExistsLiveCue(cue) ? cue : LiveSoundDefine.NextRushFallbackCue(cue));
		};
		internal Action<uint> Stop = handle => SoundManager.Instance.StopSE(handle);

		internal void Update(MusicScore score, float now, bool auto, List<LiveLogic.InputTmp> inputs, double displayLead = 0)
		{
			var sounds = score?.ConnectorSounds;
			if (_sounds != sounds || now < _previous)
			{
				Reset();
				_sounds = sounds;
			}
			if (sounds == null || sounds.Length == 0) return;
			if (auto)
			{
				// Consume even intervals skipped by a late frame: they still displaced the older owner.
				while (_cursor < sounds.Length && sounds[_cursor].Start <= now)
				{
					var sound = sounds[_cursor++];
					if (sound.AutoChannel >= 0) _owner[sound.AutoChannel] = sound;
				}
			}
			else
			{
				_manualOrder ??= sounds.Select(s => (spawn: s.SpawnTime(displayLead), sound: s)).OrderBy(e => e.spawn).ToArray();
				while (_cursor < _manualOrder.Length && _manualOrder[_cursor].spawn <= now)
				{
					var sound = _manualOrder[_cursor++].sound;
					_pending.Add(new ManualHold { Sound = sound });
				}
				if (_pending.Count > 0) ReadContacts(inputs);
				for (int i = 0; i < _pending.Count; i++)
				{
					var hold = _pending[i];
					var sound = hold.Sound;
					int channel = sound.ManualChannel;
					bool finished = (sound.Tail.HasJudgment ? sound.Tail.State == NoteState.Done : now >= sound.End)
						|| _previous > sound.End + 8.5f / 60;
					// The slide manager reserves once, even for silent connectors. A denied slot is
					// never retried. Normal and critical each have ten reservations for manual play.
					int pool = Math.Max(0, channel);
					if (!hold.HasSlot && !finished && _reserved[pool] < 10)
					{
						hold.HasSlot = true;
						_reserved[pool]++;
					}
					if (!hold.HasSlot) { _pending.RemoveAt(i--); continue; }
					bool held = channel >= 0 && !finished && now < sound.End + TailGrace(sound.Head)
						&& IsHeld(ref hold, now);
					if (held && !hold.Held) _owner[channel] = sound;
					if (!held && channel >= 0 && _owner[channel] == sound) _owner[channel] = null;
					if (finished)
					{
						_reserved[pool]--;
						_pending.RemoveAt(i--);
					}
					else
					{
						hold.Held = held;
						_pending[i] = hold;
					}
				}
			}
			for (int channel = 0; channel < 2; channel++)
			{
				var sound = _owner[channel];
				if (auto && sound != null && (now >= sound.End || sound.AutoStyle.NativeScroll.SampleFrame(now).Hidden))
					sound = null;
				SetPlaying(channel, sound);
			}
			_previous = now;
		}

		private static float TailGrace(NoteBase head)
		{
			if (head is ConnectionNote connection) return connection.LateContactSeconds;
			if (!head.HasJudgment) return 5f / 60;
			if (head is NextRushDamageNote) return 0f;
			if (head is FlickNote or FrictionFlickNote { IsTailFlickOwner: true } or NormalNote { IsIndependentRelease: true }) return 8.5f / 60;
			return .125f;
		}

		private void ReadContacts(List<LiveLogic.InputTmp> inputs)
		{
			_sampledFingers.Clear();
			_contacts.Clear();
			for (int i = inputs.Count - 1; i >= 0; i--)
			{
				var input = inputs[i];
				if (_sampledFingers.Add(input.Touch.touchId) && !float.IsNegativeInfinity(input.Lane)
					&& !LiveInputRouter.IsEndedOrCanceled(input.Touch.phase)) _contacts.Add(input.Touch.worldPosition.x);
			}
		}

		private bool IsHeld(ref ManualHold hold, float now)
		{
			var sound = hold.Sound;
			var head = sound.Head;
			bool began = false;
			if (!hold.Ready)
			{
				if (now >= sound.Start && (hold.TouchedHead || !head.HasJudgment))
					began = hold.Ready = true;
				else if (_previous > sound.Start + .125f
					|| head is FrictionFlickNote { IsTailFlickOwner: true, HasCapturedPress: true }
					|| (head is LongNote or NormalNote { IsIndependentRelease: false } or FlickNote)
						&& (head.Result >= NoteResult.Bad || head is FlickNote { State: NoteState.Input }))
					hold.Ready = true;
				else if (head is FrictionNote trace)
				{
					if (trace.AcceptsContactAt(now, sound.Start)
						&& sound.HasContact(now, _contacts, atHead: true)) hold.TouchedHead = true;
				}
			}
			return hold.Ready && !head.NativeScroll.SampleFrame(now).Hidden
				&& (began || sound.FakeActive || sound.HasContact(now, _contacts));
		}

		private void SetPlaying(int channel, NextRushConnectorSound sound)
		{
			if (_playing[channel] == sound) return;
			if (_handles[channel] != 0) Stop(_handles[channel]);
			_handles[channel] = sound == null ? 0 : Play(channel == 0
				? LiveSoundDefine.SE_LIVE_LONG : LiveSoundDefine.SE_LIVE_LONG_CRITICAL);
			_playing[channel] = sound;
		}

		internal void Silence()
		{
			SetPlaying(0, null);
			SetPlaying(1, null);
		}

		internal void Reset()
		{
			Silence();
			Array.Clear(_owner, 0, _owner.Length);
			_pending.Clear();
			Array.Clear(_reserved, 0, _reserved.Length);
			_manualOrder = null;
			_cursor = 0;
			_previous = float.NegativeInfinity;
		}
	}
}
