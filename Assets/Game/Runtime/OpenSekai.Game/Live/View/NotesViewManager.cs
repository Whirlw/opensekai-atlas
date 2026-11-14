using System.Collections.Generic;
using Sekai.Core.Live;
using UnityEngine;
using Sekai;

namespace Sekai.Live
{
	public class NotesViewManager
	{
		private static readonly int DefaultNoteDictCapacity;

		private static readonly int NoteShowRateId = Shader.PropertyToID("_NoteShowRate");

		private Dictionary<INote, BaseNoteView> _spawnNoteDict;

		private Transform _noteRoot;

		private Dictionary<NoteCategory, Dictionary<NoteType, NotePool>> _notePoolDict;

		private NoteLineView _noteLineView;

		private NoteLineView _guideLineView;

		private PairNoteLineView _pairNoteLineView;

		private bool _isEnablePairNotesLine;

		public void Setup(Transform liveRoot)
		{
			_spawnNoteDict = new Dictionary<INote, BaseNoteView>(DefaultNoteDictCapacity);
			GameObject root = new GameObject("NoteRoot");
			_noteRoot = root.transform;
			_noteRoot.SetParent(liveRoot, false);
		}

		public void CreateNotePool(BaseLiveController baseController, Dictionary<(NoteCategory, NoteType), int> noteCountDictionary)
		{
			if (baseController == null || _notePoolDict != null)
			{
				return;
			}

			float noteShowRate = 1f;
			if (baseController.Settings != null && !baseController.Settings.NoteShowRate.Equals(1f))
			{
				noteShowRate = LiveUtility.CalcNoteShowRate(baseController.Settings.NoteShowRate);
			}

			_isEnablePairNotesLine = baseController.Settings?.UseSimultaneousPushingLine ?? true;
			_notePoolDict = new Dictionary<NoteCategory, Dictionary<NoteType, NotePool>>(7)
			{
				[NoteCategory.Normal] = CreateTypedPools(noteCountDictionary, NoteCategory.Normal, "NormalNotePool", "NormalNote", "NormalCriticalNotePool", "NormalCrtNote", noteShowRate),
				[NoteCategory.Long] = CreateTypedPools(noteCountDictionary, NoteCategory.Long, "LongNotePool", "LongNote", "LongCriticalPool", "LongCrtNote", noteShowRate),
				[NoteCategory.Connection] = CreateTypedPools(noteCountDictionary, NoteCategory.Connection, "ConnectionNotePool", "ConnectionNote", "ConnectionCriticalPool", "ConnectionCrtNote", noteShowRate),
				[NoteCategory.Flick] = CreateTypedPools(noteCountDictionary, NoteCategory.Flick, "FlickNotePool", "FlickNote", "FlickCriticalPool", "FlickCrtNote", noteShowRate),
				[NoteCategory.Friction] = CreateTypedPools(noteCountDictionary, NoteCategory.Friction, "FrictionNotePool", "FrictionNote", "FrictionCriticalNotePool", "FrictionCrtNote", noteShowRate),
				[NoteCategory.FrictionLong] = CreateTypedPools(noteCountDictionary, NoteCategory.FrictionLong, "FrictionLongNotePool", "FrictionLongNote", "FrictionLongCriticalNotePool", "FrictionLongCrtNote", noteShowRate),
				[NoteCategory.FrictionFlick] = CreateTypedPools(noteCountDictionary, NoteCategory.FrictionFlick, "FrictionFlickNotePool", "FrictionFlickNote", "FrictionFlickCriticalNotePool", "FrictionFlickCrtNote", noteShowRate)
			};

			bool isCustomMusicScore = baseController.BootData?.IsCustomMusicScore ?? false;
			LiveBundleBuildData bundleBuildData = baseController.BootData?.BundleBuildData;
			_noteLineView = CreateLineView<NoteLineView>("NoteLineView");
			_noteLineView.Setup(bundleBuildData, "longNoteLine.png", LiveConfig.LongNoteAlpha, isCustomMusicScore);
			_guideLineView = CreateLineView<NoteLineView>("GuideLineView");
			_guideLineView.Setup(bundleBuildData, "traceLine_eff.png", LiveConfig.GuideAlpha, isCustomMusicScore);
			if (_isEnablePairNotesLine)
			{
				_pairNoteLineView = CreateLineView<PairNoteLineView>("PairNoteLineView");
				_pairNoteLineView.Setup();
			}

			SetupNoteMask(noteShowRate);
		}

		private void SetupNoteMask(float NoteShowRate)
		{
			ApplyNoteMask(_noteLineView?.Material, NoteShowRate);
			ApplyNoteMask(_guideLineView?.Material, NoteShowRate);
			ApplyNoteMask(_pairNoteLineView?.Material, NoteShowRate);

			if (_notePoolDict == null)
			{
				return;
			}

			foreach (Dictionary<NoteType, NotePool> typedPools in _notePoolDict.Values)
			{
				if (typedPools == null)
				{
					continue;
				}

				foreach (NotePool pool in typedPools.Values)
				{
					pool?.SetupNoteMask(NoteShowRate);
				}
			}
		}

		private static void ApplyNoteMask(Material material, float NoteShowRate)
		{
			if (material != null)
			{
				material.SetFloat(NoteShowRateId, NoteShowRate);
			}
		}

		public void OnUpdate(float time)
		{
			if (_spawnNoteDict != null)
			{
				foreach (BaseNoteView noteView in _spawnNoteDict.Values)
				{
					noteView?.Move(time);
				}
			}
			_noteLineView?.Excute(time);
			_guideLineView?.Excute(time);
			if (_isEnablePairNotesLine)
			{
				_pairNoteLineView?.Execute(time);
			}
		}

		public void Clear()
		{
			if (_spawnNoteDict != null)
			{
				foreach (BaseNoteView noteView in _spawnNoteDict.Values)
				{
					noteView?.Unspawn();
				}
				_spawnNoteDict.Clear();
			}
			_noteLineView?.Clear();
			_guideLineView?.Clear();
			_pairNoteLineView?.Clear();
		}

		public void SpawnNote(INote note)
		{
			if (note == null || _spawnNoteDict == null || _spawnNoteDict.ContainsKey(note))
			{
				return;
			}

			if (note is GuideNote guideNote)
			{
				_guideLineView?.Add(guideNote);
				return;
			}

			if (note is LongNote longNote)
			{
				_noteLineView?.Add(longNote);
			}

			NotePool pool = FindPool(note.Category, note.Type);
			BaseNoteView noteView = pool?.Spawn(note);
			if (noteView != null)
			{
				_spawnNoteDict[note] = noteView;
			}

			if (_isEnablePairNotesLine && note.PairNote != null && !(note is NoteBase decorationNote && decorationNote.IsDecoration))
			{
				_pairNoteLineView?.Add(note);
			}
		}

		public void UnspawnNote(INote note)
		{
			if (note == null)
			{
				return;
			}

			if (note is GuideNote guideNote)
			{
				_guideLineView?.Remove(guideNote);
				return;
			}

			if (note is LongNote longNote)
			{
				_noteLineView?.Remove(longNote);
				// Unspawn all child notes for decoration long notes
				if (longNote.IsDecoration && longNote.NoteList != null)
				{
					foreach (NoteBase child in longNote.NoteList)
					{
						if (child != null && child != longNote
							&& !((longNote.HasIndependentTail || longNote.HasManagedTail) && ReferenceEquals(child, longNote.TailNote))
							&& _spawnNoteDict != null && _spawnNoteDict.TryGetValue(child, out BaseNoteView childView))
						{
							childView?.Unspawn();
							_spawnNoteDict.Remove(child);
						}
					}
				}
			}

			if (_spawnNoteDict != null && _spawnNoteDict.TryGetValue(note, out BaseNoteView noteView))
			{
				noteView?.Unspawn();
				_spawnNoteDict.Remove(note);
			}
		}

		private NotePool CreateNotePool(int poolCount, string poolName, string noteResourceName, float noteShowRate)
		{
			GameObject poolObject = new GameObject(poolName);
			poolObject.transform.SetParent(_noteRoot, false);
			NotePool pool = poolObject.AddComponent<NotePool>();
			GameObject notePrefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.NoteBundleName, noteResourceName, false);
			pool.Setup(Mathf.Max(poolCount, 1), notePrefab, noteShowRate);
			return pool;
		}

		public NotesViewManager()
		{
		}

		static NotesViewManager()
		{
			DefaultNoteDictCapacity = 100;
		}

		private Dictionary<NoteType, NotePool> CreateTypedPools(
			Dictionary<(NoteCategory, NoteType), int> counts,
			NoteCategory category,
			string defaultPoolName,
			string defaultResourceName,
			string criticalPoolName,
			string criticalResourceName,
			float noteShowRate)
		{
			return new Dictionary<NoteType, NotePool>(2)
			{
				[NoteType.Default] = CreateNotePool(GetPoolCount(counts, category, NoteType.Default), defaultPoolName, defaultResourceName, noteShowRate),
				[NoteType.Critical] = CreateNotePool(GetPoolCount(counts, category, NoteType.Critical), criticalPoolName, criticalResourceName, noteShowRate)
			};
		}

		private static int GetPoolCount(Dictionary<(NoteCategory, NoteType), int> counts, NoteCategory category, NoteType type)
		{
			if (counts != null && counts.TryGetValue((category, type), out int count))
			{
				return Mathf.Max(count + 1, 1);
			}

			return 4;
		}

		private NotePool FindPool(NoteCategory category, NoteType type)
		{
			if (_notePoolDict == null)
			{
				return null;
			}

			if (!_notePoolDict.TryGetValue(category, out Dictionary<NoteType, NotePool> typedPools))
			{
				return null;
			}

			if (typedPools.TryGetValue(type, out NotePool pool))
			{
				return pool;
			}

			return null;
		}

		private T CreateLineView<T>(string viewName) where T : Component
		{
			GameObject viewObject = new GameObject(viewName, typeof(T));
			viewObject.transform.SetParent(_noteRoot, false);
			return viewObject.GetComponent<T>();
		}
	}
}
