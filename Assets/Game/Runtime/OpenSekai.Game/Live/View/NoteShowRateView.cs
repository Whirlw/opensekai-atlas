using UnityEngine;

namespace Sekai
{
	public class NoteShowRateView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _hideAreaView;

		[SerializeField]
		private SpriteRenderer _showLine;

		[SerializeField]
		private Vector2 _minShowLineSize;

		[SerializeField]
		private Vector2 _maxShowLineSize;

		private Material _material;

		public void Setup(LiveSettingData liveSetting)
		{
			if (liveSetting == null)
			{
				gameObject.SetActive(false);
				return;
			}
			float settingsRate = 1f - liveSetting._noteShowRate;
			if (Mathf.Approximately(settingsRate, 1f))
			{
				gameObject.SetActive(false);
				return;
			}

			Vector2 position = LiveUtility.CalcNoteShowRatePosition(settingsRate);
			float progress = LiveUtility.CalcNoteShowRate(position);
			if (_showLine != null)
			{
				float t = Mathf.Clamp01(settingsRate);
				_showLine.size = Vector2.Lerp(_maxShowLineSize, _minShowLineSize, t);
				_showLine.transform.localPosition = new Vector3(0f, position.y + _showLine.size.y * 0.5f, 0f);
			}

			if (_hideAreaView != null)
			{
				if (_material == null)
				{
					Shader shader = Shader.Find("Sekai/Live/Note/HideArea");
					if (shader != null)
					{
						_material = new Material(shader);
					}
				}
				if (_material != null)
				{
					_hideAreaView.material = _material;
					_material.SetFloat("_NoteShowRate", progress);
				}
			}
			gameObject.SetActive(true);
		}

		private void OnDestroy()
		{
			if (_material != null)
			{
				Destroy(_material);
				_material = null;
				if (_hideAreaView != null)
				{
					_hideAreaView.material = null;
				}
			}
		}

		public NoteShowRateView()
		{
		}
	}
}
