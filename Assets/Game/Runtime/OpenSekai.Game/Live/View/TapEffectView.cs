using System;
using System.Collections.Generic;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using Sekai.SUS;

namespace Sekai
{
	public class TapEffectView : MonoBehaviour
	{
		private struct NoteInfo
		{
			public NoteResult result;

			public NoteCategory category;

			public NoteType type;

			public NoteDirection direction;

			public int laneStart;

			public int laneEnd;

			public INote pairNote;

			public int noteId;
		}

		private static readonly int LANE_COUNT;

		private const string COLLABORATION_SE_CATEGORY_1 = "collaborationTap1";

		private const string COLLABORATION_SE_CATEGORY_2 = "collaborationTap2";

		private ParticleSystemController[] _tapLaneEffectList;

		private ParticleSystemController[] _defaultLaneEffectList;

		private ParticleSystemController[] _criticalLaneEffectList;

		private ParticleSystemController[] _flickLaneEffectList;

		private ParticleSystemController[] _criticalFlickLaneEffectList;

		private Dictionary<int, EffectPool> _auraPoolDict;

		private Dictionary<int, EffectPool> _genPoolDict;

		private Dictionary<NoteType, EffectPool> _flashPoolDict;

		private Dictionary<NoteType, EffectPool> _connectionPoolDict;

		private Dictionary<int, EffectPool> _frictionPoolDict;

		private Dictionary<int, string> _tapSEDict;

		private Dictionary<int, string> _pairNoteSeMap;

		private NoteInfo _noteInfo;

		private int _lastFrame;

		private int[] _lastTapLanes;

		private bool _isEnableVibration;

		private Action _playSeAction;

		public void Setup(BaseLiveController liveBaseController)
		{
			_isEnableVibration = liveBaseController?.Settings?.UseVibration ?? true;
			_tapLaneEffectList = new ParticleSystemController[LANE_COUNT];
			_defaultLaneEffectList = new ParticleSystemController[LANE_COUNT];
			_criticalLaneEffectList = new ParticleSystemController[LANE_COUNT];
			_flickLaneEffectList = new ParticleSystemController[LANE_COUNT];
			_criticalFlickLaneEffectList = new ParticleSystemController[LANE_COUNT];
			_pairNoteSeMap = new Dictionary<int, string>();

			for (int i = 0; i < LANE_COUNT; i++)
			{
				_tapLaneEffectList[i] = LoadTapEffect(LivePrefabDefine.FX_LANE_TAP);
				_defaultLaneEffectList[i] = LoadTapEffect(LivePrefabDefine.FX_LANE_DEFAULT);
				_criticalLaneEffectList[i] = LoadTapEffect(LivePrefabDefine.FX_LANE_CRITICAL);
				_flickLaneEffectList[i] = LoadTapEffect(LivePrefabDefine.FX_LANE_DEFAULT_FLICK);
				_criticalFlickLaneEffectList[i] = LoadTapEffect(LivePrefabDefine.FX_LANE_CRITICAL_FLICK);
			}

			CreateTapEffect(LivePrefabDefine.FX_NORMAL_PERFECT_AURA, LivePrefabDefine.FX_NORMAL_PERFECT_GEN, NoteCategory.Normal, NoteType.Default, NoteResult.Perfect);
			CreateTapEffect(LivePrefabDefine.FX_NORMAL_GREAT_AURA, LivePrefabDefine.FX_NORMAL_GREAT_GEN, NoteCategory.Normal, NoteType.Default, NoteResult.Great);
			CreateTapEffect(LivePrefabDefine.FX_NORMAL_GOOD_AURA, LivePrefabDefine.FX_NORMAL_GOOD_GEN, NoteCategory.Normal, NoteType.Default, NoteResult.Good);
			CreateTapEffect(LivePrefabDefine.FX_CRITICAL_AURA, LivePrefabDefine.FX_CRITICAL_GEN, NoteCategory.Normal, NoteType.Critical, NoteResult.Perfect);
			CreateFrictionEffect(LivePrefabDefine.FX_FRICITION_AURA, NoteType.Default);
			CreateFrictionEffect(LivePrefabDefine.FX_CRITICAL_FRICITION_AURA, NoteType.Critical);
			CreateTapEffect(LivePrefabDefine.FX_FLICK_AURA, LivePrefabDefine.FX_FLICK_GEN, NoteCategory.Flick, NoteType.Default, NoteResult.Perfect);
			_flashPoolDict[NoteType.Default] = CreateEffectPool(LivePrefabDefine.FX_FLICK_FLASH, 5);
			CreateTapEffect(LivePrefabDefine.FX_CRITICAL_FLICK_AURA, LivePrefabDefine.FX_CRITICAL_FLICK_GEN, NoteCategory.Flick, NoteType.Critical, NoteResult.Perfect);
			_flashPoolDict[NoteType.Critical] = CreateEffectPool(LivePrefabDefine.FX_CRITICAL_FLICK_FLASH, 5);
			CreateTapEffect(LivePrefabDefine.FX_LONG_AURA, LivePrefabDefine.FX_LONG_GEN, NoteCategory.Long, NoteType.Default, NoteResult.Perfect);
			CreateTapEffect(LivePrefabDefine.FX_CRITICAL_LONG_AURA, LivePrefabDefine.FX_CRITICAL_LONG_GEN, NoteCategory.Long, NoteType.Critical, NoteResult.Perfect);
			_connectionPoolDict[NoteType.Default] = CreateEffectPool(LivePrefabDefine.FX_CONNECT_AURA, 5);
			_connectionPoolDict[NoteType.Critical] = CreateEffectPool(LivePrefabDefine.FX_CRITICAL_CONNECT_AURA, 5);

			bool isCollaborationMode = IsCollaborationMode(liveBaseController?.BootData);
			SetupSEDict(isCollaborationMode);
			SetupPlaySE(isCollaborationMode);
		}

		private bool IsCollaborationMode(LiveBootDataBase bootData)
		{
			return bootData?.MusicData?.CollaboModeState == LiveMusicData.CollaborationModeState.On;
		}

		private void SetupPlaySE(bool isCollaborationMode)
		{
			_playSeAction = isCollaborationMode ? PlayCollaborationSE : PlaySe;
		}

		public void Clear()
		{
			_lastFrame = 0;
			_pairNoteSeMap?.Clear();
		}

		public void Dispose()
		{
			Clear();
		}

		private void SetupSEDict(bool isCollaborationMode)
		{
			_tapSEDict = new Dictionary<int, string>
			{
				[GetTapSeKey(NoteCategory.Normal, NoteType.Default)] = LiveSoundDefine.SE_LIVE_PERFECT,
				[GetTapSeKey(NoteCategory.Normal, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_CRITICAL,
				[GetTapSeKey(NoteCategory.Long, NoteType.Default)] = LiveSoundDefine.SE_LIVE_PERFECT,
				[GetTapSeKey(NoteCategory.Long, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_PERFECT,
				[GetTapSeKey(NoteCategory.Connection, NoteType.Default)] = LiveSoundDefine.SE_LIVE_CONNECT,
				[GetTapSeKey(NoteCategory.Connection, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_CONNECT_CRITICAL,
				[GetTapSeKey(NoteCategory.Flick, NoteType.Default)] = LiveSoundDefine.SE_LIVE_FLICK,
				[GetTapSeKey(NoteCategory.Flick, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_FLICK_CRITICAL,
				[GetTapSeKey(NoteCategory.Friction, NoteType.Default)] = LiveSoundDefine.SE_LIVE_FRICTION,
				[GetTapSeKey(NoteCategory.Friction, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_FRICTION_CRITICAL,
				[GetTapSeKey(NoteCategory.FrictionLong, NoteType.Default)] = LiveSoundDefine.SE_LIVE_FRICTION,
				[GetTapSeKey(NoteCategory.FrictionLong, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_FRICTION_CRITICAL,
				[GetTapSeKey(NoteCategory.FrictionFlick, NoteType.Default)] = LiveSoundDefine.SE_LIVE_FLICK,
				[GetTapSeKey(NoteCategory.FrictionFlick, NoteType.Critical)] = LiveSoundDefine.SE_LIVE_FLICK_CRITICAL
			};
			_pairNoteSeMap = new Dictionary<int, string>();
		}

		private void CreateTapEffect(string effectName, string genName, NoteCategory category, NoteType type, NoteResult result = NoteResult.Perfect)
		{
			int key = GetTapEffectKey(category, type, result);
			_auraPoolDict[key] = CreateEffectPool(effectName, 12);
			_genPoolDict[key] = CreateEffectPool(genName, 6);
		}

		private void CreateFrictionEffect(string effectName, NoteType type)
		{
			_frictionPoolDict[(int)type] = CreateEffectPool(effectName, 12);
		}

		private void TapLane(int lane)
		{
			ParticleSystemController[] list;
			if (_noteInfo.category == NoteCategory.Flick || _noteInfo.category == NoteCategory.FrictionFlick)
			{
				list = _noteInfo.type == NoteType.Critical ? _criticalFlickLaneEffectList : _flickLaneEffectList;
			}
			else
			{
				list = _noteInfo.type == NoteType.Critical ? _criticalLaneEffectList : _defaultLaneEffectList;
			}

			if (list == null || lane < 0 || lane >= list.Length || list[lane] == null)
			{
				return;
			}

			list[lane].transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(lane), 0f, 0f);
			list[lane].Play();
		}

		private void TapAura()
		{
			EffectPool pool = GetTapEffectPool(_auraPoolDict);
			if (pool == null)
			{
				return;
			}

			for (int lane = _noteInfo.laneStart; lane <= _noteInfo.laneEnd; lane++)
			{
				ParticleSystemController controller = pool.Spawn();
				if (controller == null)
				{
					continue;
				}

				controller.transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(lane), 0f, 0f);
				controller.Play();
			}
		}

		private void TapGen()
		{
			EffectPool pool = GetTapEffectPool(_genPoolDict);
			ParticleSystemController controller = pool?.Spawn();
			if (controller == null)
			{
				return;
			}

			controller.transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(GetLaneCenter()), 0f, 0f);
			controller.Play();
		}

		private void FlickSlash()
		{
			if (_flashPoolDict == null || !_flashPoolDict.TryGetValue(_noteInfo.type, out EffectPool pool))
			{
				return;
			}

			ParticleSystemController controller = pool.Spawn();
			if (controller == null)
			{
				return;
			}

			controller.transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(GetLaneCenter()), 0f, 0f);
			controller.transform.localScale = new Vector3(_noteInfo.laneEnd - _noteInfo.laneStart + 1f, 1f, 1f);
			float z = _noteInfo.direction == NoteDirection.Right ? -15f : _noteInfo.direction == NoteDirection.Left ? 15f : 0f;
			controller.transform.localEulerAngles = new Vector3(0f, 0f, z);
			controller.Play();
		}

		private void PlayUnpickedEffect(int lane)
		{
			if (_tapLaneEffectList != null && lane >= 0 && lane < _tapLaneEffectList.Length)
			{
				ParticleSystemController controller = _tapLaneEffectList[lane];
				if (controller != null)
				{
					controller.transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(lane), 0f, 0f);
					controller.Play();
				}
			}

			SoundManager.Instance.PlayIngameSEOneShot(LiveSoundDefine.SE_LIVE_TAP);
		}

		public void Unpicked(int lane, ref LiveTouch touch)
		{
			if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
			{
				if (_lastFrame < Time.frameCount - 1)
				{
					return;
				}

				if (_lastTapLanes == null || touch.fingerId >= _lastTapLanes.Length)
				{
					_lastFrame = Time.frameCount;
					return;
				}

				if (_lastTapLanes[touch.fingerId] == lane)
				{
					_lastFrame = Time.frameCount;
					return;
				}
			}

			PlayUnpickedEffect(lane);
			_lastFrame = Time.frameCount;
			if (_lastTapLanes != null && touch.fingerId >= 0 && touch.fingerId < _lastTapLanes.Length)
			{
				_lastTapLanes[touch.fingerId] = lane;
			}
		}

		public void Excute(INote note, Func<bool> checkPlayedHaptic)
		{
			if (note == null)
			{
				return;
			}

			NoteResult result = note.Result;

			// Nothing has been played yet when the result is still None: a note that merely left the
			// line, or a tail the bar is still carrying, must never fire a tap effect or a hit sound.
			if (result == NoteResult.None || result == NoteResult.Miss || result == NoteResult.Pass || result == NoteResult.Bad)
			{
				return;
			}

			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Tail))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(
					OpenSekai.Shared.Diagnostics.LiveTraceCategory.Tail,
					"tapFx cat=" + note.Category + " type=" + note.Type + " res=" + result + " id=" + note.Id);
			}

			_noteInfo = new NoteInfo
			{
				result = result,
				category = note.Category,
				type = note.Type,
				direction = note.Direction,
				laneStart = note.LaneStart,
				laneEnd = note.LaneEnd,
				pairNote = note.PairNote,
				noteId = note.Id
			};
			switch (_noteInfo.category)
			{
				case NoteCategory.Connection:
					Connection();
					break;
				case NoteCategory.Flick:
				case NoteCategory.FrictionFlick:
					PlayLaneEffect();
					TapAura();
					TapGen();
					FlickSlash();
					break;
				case NoteCategory.Friction:
				case NoteCategory.FrictionLong:
					Friction();
					break;
				case NoteCategory.FrictionHide:
				case NoteCategory.FrictionHideLong:
				case NoteCategory.Combo:
				case NoteCategory.Hidden:
					return;
				default:
					PlayLaneEffect();
					TapAura();
					TapGen();
					break;
			}

			_playSeAction?.Invoke();
			if (checkPlayedHaptic == null || !checkPlayedHaptic())
			{
				PlayHaptic();
			}
		}

		private void PlayLaneEffect()
		{
			for (int lane = _noteInfo.laneStart; lane <= _noteInfo.laneEnd; lane++)
			{
				TapLane(lane);
			}
		}

		private void Connection()
		{
			if (_connectionPoolDict == null || !_connectionPoolDict.TryGetValue(_noteInfo.type, out EffectPool pool))
			{
				return;
			}

			ParticleSystemController controller = pool.Spawn();
			if (controller == null)
			{
				return;
			}

			controller.transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(GetLaneCenter()), 0f, 0f);
			controller.Play();
		}

		private void Friction()
		{
			if (_frictionPoolDict == null || !_frictionPoolDict.TryGetValue((int)_noteInfo.type, out EffectPool pool))
			{
				return;
			}

			ParticleSystemController controller = pool.Spawn();
			if (controller == null)
			{
				return;
			}

			controller.transform.localPosition = new Vector3(LiveUtility.CalcLaneTransformX(GetLaneCenter()), 0f, 0f);
			controller.Play();
		}

		private void PlaySe()
		{
			if (_tapSEDict == null || !_tapSEDict.TryGetValue(GetTapSeKey(_noteInfo.category, _noteInfo.type), out string mapped))
			{
				return;
			}

			if (_noteInfo.category == NoteCategory.Normal && _noteInfo.type == NoteType.Default)
			{
				switch (_noteInfo.result)
				{
					case NoteResult.Auto:
					case NoteResult.Perfect:
					case NoteResult.JustPerfect:
						PlaySE(LiveSoundDefine.SE_LIVE_PERFECT, "perfect", _noteInfo.noteId, _noteInfo.pairNote);
						return;
					case NoteResult.Good:
						PlaySE(LiveSoundDefine.SE_LIVE_GOOD, "good", _noteInfo.noteId, _noteInfo.pairNote);
						return;
					case NoteResult.Great:
						PlaySE(LiveSoundDefine.SE_LIVE_GREAT, "perfect", _noteInfo.noteId, _noteInfo.pairNote);
						return;
					default:
						return;
				}
			}

			PlaySE(mapped, mapped, _noteInfo.noteId, _noteInfo.pairNote);
		}

		private void PlayCollaborationSE()
		{
			if (_tapSEDict == null || !_tapSEDict.TryGetValue(GetTapSeKey(_noteInfo.category, _noteInfo.type), out string mapped))
			{
				return;
			}

			string category = GetCollaborationSECategory(_noteInfo);
			if (_noteInfo.category == NoteCategory.Normal && _noteInfo.type == NoteType.Default)
			{
				switch (_noteInfo.result)
				{
					case NoteResult.Auto:
					case NoteResult.Perfect:
					case NoteResult.JustPerfect:
						PlaySE(LiveSoundDefine.SE_LIVE_PERFECT, category, _noteInfo.noteId, _noteInfo.pairNote);
						return;
					case NoteResult.Good:
						PlaySE(LiveSoundDefine.SE_LIVE_GOOD, category, _noteInfo.noteId, _noteInfo.pairNote);
						return;
					case NoteResult.Great:
						PlaySE(LiveSoundDefine.SE_LIVE_GREAT, category, _noteInfo.noteId, _noteInfo.pairNote);
						return;
					default:
						return;
				}
			}

			PlaySE(mapped, category, _noteInfo.noteId, _noteInfo.pairNote);
		}

		private string GetCollaborationSECategory(NoteInfo _noteInfo)
		{
			return _noteInfo.type == NoteType.Critical ? COLLABORATION_SE_CATEGORY_2 : COLLABORATION_SE_CATEGORY_1;
		}

		private void PlayHaptic()
		{
			if (!_isEnableVibration)
			{
				return;
			}
		}

		private void PlaySE(string seName, string category, int myNoteId, INote pairNote)
		{
			if (string.IsNullOrEmpty(seName))
			{
				return;
			}

			category ??= seName;
			if (pairNote == null || _pairNoteSeMap == null)
			{
				SoundManager.Instance.PlayIngameSEOneShot(seName);
				return;
			}

			int pairNoteId = pairNote.Id;
			if (_pairNoteSeMap.ContainsKey(pairNoteId))
			{
				if (_pairNoteSeMap[pairNoteId] != category)
				{
					SoundManager.Instance.PlayIngameSEOneShot(seName);
				}
				_pairNoteSeMap.Remove(pairNoteId);
				return;
			}

			if (!_pairNoteSeMap.ContainsKey(myNoteId))
			{
				_pairNoteSeMap.Add(myNoteId, category);
				SoundManager.Instance.PlayIngameSEOneShot(seName);
				return;
			}

			if (_pairNoteSeMap[myNoteId] != category)
			{
				SoundManager.Instance.PlayIngameSEOneShot(seName);
			}
			_pairNoteSeMap.Remove(myNoteId);
		}

		private ParticleSystemController LoadTapEffect(string name)
		{
			GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.EffectBundleName, name);
			if (prefab == null)
			{
				return null;
			}

			GameObject instance = Instantiate(prefab, transform);
			return instance.AddComponent<ParticleSystemController>();
		}

		public TapEffectView()
		{
			_auraPoolDict = new Dictionary<int, EffectPool>();
			_genPoolDict = new Dictionary<int, EffectPool>();
			_flashPoolDict = new Dictionary<NoteType, EffectPool>();
			_connectionPoolDict = new Dictionary<NoteType, EffectPool>();
			_frictionPoolDict = new Dictionary<int, EffectPool>();
			_tapSEDict = new Dictionary<int, string>();
			_pairNoteSeMap = new Dictionary<int, string>();
			_lastTapLanes = new int[10];
			_isEnableVibration = true;
		}

		static TapEffectView()
		{
			LANE_COUNT = LiveUtility.LANE_COUNT + 1;
		}

		private static int GetTapSeKey(NoteCategory category, NoteType type)
		{
			return (int)category + 10 * (int)type;
		}

		private static int GetTapEffectKey(NoteCategory category, NoteType type, NoteResult result)
		{
			return (int)category + 10 * (int)type + 100 * (int)result;
		}

		private EffectPool CreateEffectPool(string effectName, int poolCount)
		{
			GameObject holder = new GameObject(effectName, typeof(EffectPool));
			EffectPool pool = holder.GetComponent<EffectPool>();
			holder.transform.SetParent(transform, false);
			GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.EffectBundleName, effectName);
			pool.Setup(prefab, poolCount);
			return pool;
		}

		private EffectPool GetTapEffectPool(Dictionary<int, EffectPool> dictionary)
		{
			if (dictionary == null)
			{
				return null;
			}

			int key = GetTapEffectKey(_noteInfo.category, _noteInfo.type, _noteInfo.result);
			if (dictionary.TryGetValue(key, out EffectPool pool))
			{
				return pool;
			}

			key = GetTapEffectKey(_noteInfo.category, _noteInfo.type, NoteResult.Perfect);
			return dictionary.TryGetValue(key, out pool) ? pool : null;
		}

		private float GetLaneCenter()
		{
			return (_noteInfo.laneEnd - _noteInfo.laneStart) * 0.5f + _noteInfo.laneStart;
		}
	}
}
