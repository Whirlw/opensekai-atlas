using Sekai.Live;
using System.Collections.Generic;
using UnityEngine;

namespace Sekai
{
	public class LongTapEffectView : MonoBehaviour
	{
		public class EffectPair
		{
						private ParticleSystemController _Aura;
			public ParticleSystemController Aura
			{
			    get => _Aura;
			    set => _Aura = value;
			}

						private ParticleSystemController _Gen;
			public ParticleSystemController Gen
			{
			    get => _Gen;
			    set => _Gen = value;
			}

			internal bool SoundRequested;
			private uint _PlaybackId;
			public uint PlaybackId
			{
			    get => _PlaybackId;
			    set => _PlaybackId = value;
			}

						private bool _IsUsed;
			public bool IsUsed
			{
			    get => _IsUsed;
			    set => _IsUsed = value;
			}

			internal void StopSound()
			{
				SoundManager.Instance.StopSE(PlaybackId);
				PlaybackId = 0;
				SoundRequested = false;
			}

			public EffectPair(string bundleName, string genPath, string auraPath, Transform parent)
			{
				Gen = LoadTapEffect(bundleName, genPath, parent);
				Aura = LoadTapEffect(bundleName, auraPath, parent);
			}

			private ParticleSystemController LoadTapEffect(string bundleName, string name, Transform parent)
			{
				GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(bundleName, name);
				if (prefab == null)
				{
					return null;
				}

				GameObject instance = Object.Instantiate(prefab, parent);
				return instance.AddComponent<ParticleSystemController>();
			}
		}

		private EffectPair[] _tapKeeps;

		private EffectPair[] _criticalTapKeeps;

		private Dictionary<INote, EffectPair> _longNotes;

		public void Setup()
		{
			_tapKeeps = new EffectPair[10];
			_criticalTapKeeps = new EffectPair[10];
			for (int i = 0; i < _tapKeeps.Length; i++)
			{
				_tapKeeps[i] = new EffectPair(LiveConfig.EffectBundleName, LivePrefabDefine.FX_LONG_HOLD_GEN, LivePrefabDefine.FX_LONG_HOLD_AURA, transform);
				_criticalTapKeeps[i] = new EffectPair(LiveConfig.EffectBundleName, LivePrefabDefine.FX_CRITICAL_LONG_HOLD_GEN, LivePrefabDefine.FX_CRITICAL_LONG_HOLD_AURA, transform);
			}

			Warmup(_tapKeeps);
			Warmup(_criticalTapKeeps);
		}

		public void Add(INote note)
		{
			if (note == null || note.IsSkip || _longNotes.ContainsKey(note))
			{
				return;
			}

			EffectPair[] pairs = note.Type == NoteType.Critical ? _criticalTapKeeps : _tapKeeps;
			if (pairs == null)
			{
				return;
			}

			for (int i = 0; i < pairs.Length; i++)
			{
				EffectPair pair = pairs[i];
				if (pair == null || pair.IsUsed)
				{
					continue;
				}

				_longNotes.Add(note, pair);
				pair.IsUsed = true;
				return;
			}
		}

		public void Remove(INote note)
		{
			if (note == null || !_longNotes.TryGetValue(note, out EffectPair pair))
			{
				return;
			}

			_longNotes.Remove(note);
			pair.Gen?.Stop();
			pair.Aura?.Stop();
			pair.IsUsed = false;
			pair.StopSound();
		}

		public void Clear()
		{
			foreach (EffectPair pair in _longNotes.Values)
			{
				if (pair == null)
				{
					continue;
				}

				pair.Gen?.Stop();
				pair.Aura?.Stop();
				pair.IsUsed = false;
				pair.StopSound();
			}

			_longNotes.Clear();
		}

		internal void Silence()
		{
			foreach (var pair in _longNotes.Values)
			{
				pair.StopSound();
			}
		}

		private void OnDisable() => Silence();

		public void Excute(float time)
		{
			foreach (var entry in _longNotes)
			{
				INote note = entry.Key;
				EffectPair pair = entry.Value;
				if (note == null || pair == null)
				{
					continue;
				}

				// A finger on the bar right now, asked of the hold itself; the state lags a refresh behind.
				// The rule lives in LiveUtility so a plain note can be tested for it without the prefabs.
				if (LiveUtility.IsBeingHeldForEffect(note, time))
				{
					int style = note is NoteBase styled ? styled.NativeStyle : 0;
					if (note is LongNote { NativeConnector: not null } bar
						&& bar.TryNativeEdge(time, out var start, out _))
						style = start.NativeConnector.HeadStyle(style);
					pair.Gen?.SetStyle(style);
					pair.Aura?.SetStyle(style);
					float x = LiveUtility.CalcLaneTransformX((note.LaneStartF + note.LaneEndF) * 0.5f);
					float width = note.LaneEndF + 1f - note.LaneStartF;
					if (pair.Gen != null)
					{
						pair.Gen.transform.localPosition = new Vector3(x, 0f, 0f);
					}

					if (pair.Aura != null)
					{
						pair.Aura.transform.localPosition = new Vector3(x, 0f, 0f);
						pair.Aura.transform.localScale = new Vector3(width, 1f, 1f);
					}

					if (pair.Gen != null && pair.Aura != null && (!pair.Gen.IsPlaying || !pair.Aura.IsPlaying))
					{
						pair.Gen.Play(style);
						pair.Aura.Play(style);
					}

					if (!pair.SoundRequested)
					{
						pair.SoundRequested = true;
						string cue = HoldCueName(note);
						if (cue != null) pair.PlaybackId = SoundManager.Instance.PlayHoldLoop(cue);
						if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Hold))
							OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Hold,
								"holdLoop cat=" + note.Category + " type=" + note.Type + " cue=" + (cue ?? "-") + " id=" + note.Id
								+ " sound=" + (cue == null ? "none" : "start"));
					}
				}

				if (note.ShowsAsReleased)
				{
					pair.Gen?.Stop();
					pair.Aura?.Stop();
				}

				// The sound only stops once the bar is really let go: the grey window is grace,
				// and a finger coming back inside it should not restart the hold from silence.
				if (note.State == NoteState.Release)
				{
					pair.StopSound();
				}
			}
		}

		public LongTapEffectView()
		{
			_longNotes = new Dictionary<INote, EffectPair>();
		}

		private static string HoldCueName(INote note)
		{
			if (note is LongNote { NativeConnector: not null }) return null;
			// A friction bar is rubbed, not pressed, so it keeps the trace loop while the finger is on
			// it. The hide variant is the opposite by design: it is a silent carrier for hidden
			// checkpoints, so it starts no sound at all.
			switch (note.Category)
			{
				case NoteCategory.Friction:
				case NoteCategory.FrictionLong:
					return note.Type == NoteType.Critical ? LiveSoundDefine.SE_LIVE_FRICTION_CRITICAL : LiveSoundDefine.SE_LIVE_TRACE;
				case NoteCategory.FrictionHide:
				case NoteCategory.FrictionHideLong:
					return null;
				default:
					return note.Type == NoteType.Critical ? LiveSoundDefine.SE_LIVE_LONG_CRITICAL : LiveSoundDefine.SE_LIVE_LONG;
			}
		}

		private static void Warmup(EffectPair[] pairs)
		{
			if (pairs == null || pairs.Length == 0 || pairs[0] == null)
			{
				return;
			}

			pairs[0].Aura?.Play();
			pairs[0].Gen?.Play();
			pairs[0].Aura?.Stop();
			pairs[0].Gen?.Stop();
		}
	}
}
