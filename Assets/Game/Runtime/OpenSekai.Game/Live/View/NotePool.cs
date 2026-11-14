using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class NotePool : MonoBehaviour
	{
		private GameObject _notePrefab;

		private List<BaseNoteView> _noteArray;

		private int _nextSpawinIndex;

		private float _noteShowRate;

		public void Setup(int poolCount, GameObject _notePrefab, float _noteShowRate)
		{
			this._notePrefab = _notePrefab;
			this._noteShowRate = _noteShowRate;
			_nextSpawinIndex = 0;
			_noteArray = new List<BaseNoteView>();

			int count = Mathf.Max(poolCount, 1);
			for (int i = 0; i < count; i++)
			{
				CreateNote(_noteShowRate);
			}
		}

		public void SetupNoteMask(float NoteShowRate)
		{
			if (_noteArray == null)
			{
				return;
			}

			foreach (BaseNoteView noteView in _noteArray)
			{
				if (noteView != null)
				{
					noteView.Setup(LiveConfig.SpawnPosition, LiveConfig.JudgmentPositions, NoteShowRate);
				}
			}
		}

		private BaseNoteView CreateNote(float _noteShowRate)
		{
			if (_notePrefab == null)
			{
				Debug.LogWarningFormat("NotePool {0} has no note prefab.", name);
				return null;
			}

			GameObject instance = Instantiate(_notePrefab, transform);
			BaseNoteView noteView = instance.GetComponent<BaseNoteView>();
			if (noteView == null)
			{
				Debug.LogWarningFormat("Note prefab {0} does not contain BaseNoteView.", _notePrefab.name);
				Destroy(instance);
				return null;
			}

			noteView.Setup(LiveConfig.SpawnPosition, LiveConfig.JudgmentPositions, _noteShowRate);
			noteView.Unspawn();
			_noteArray.Add(noteView);
			return noteView;
		}

		public BaseNoteView Spawn(INote note)
		{
			if (_noteArray == null || _noteArray.Count == 0)
			{
				CreateNote(_noteShowRate);
			}

			if (_noteArray == null || _noteArray.Count == 0)
			{
				return null;
			}

			BaseNoteView noteView = _noteArray[_nextSpawinIndex];
			_nextSpawinIndex = (_nextSpawinIndex + 1) % _noteArray.Count;
			if (noteView == null || noteView.gameObject.activeSelf)
			{
				noteView = CreateNote(_noteShowRate);
			}

			if (noteView == null || note == null)
			{
				return null;
			}

			float z = note.MusicScoreInfo.time * 0.001f + note.LaneStart * 0.0000001f;
			noteView.Spawn(note, z);
			return noteView;
		}

		public NotePool()
		{
		}
	}
}
