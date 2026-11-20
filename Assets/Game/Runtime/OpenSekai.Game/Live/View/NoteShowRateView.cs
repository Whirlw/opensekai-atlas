using UnityEngine;

namespace Sekai
{
	public class NoteShowRateView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_hideAreaView")]
		[SerializeField]
		private SpriteRenderer hideAreaView;

		[UnityEngine.Serialization.FormerlySerializedAs("_showLine")]
		[SerializeField]
		private SpriteRenderer showLine;

		[UnityEngine.Serialization.FormerlySerializedAs("_minShowLineSize")]
		[SerializeField]
		private Vector2 minShowLineSize;

		[UnityEngine.Serialization.FormerlySerializedAs("_maxShowLineSize")]
		[SerializeField]
		private Vector2 maxShowLineSize;

		private Material _material;

		public void Setup(LiveSettingData liveSetting)
		{
			if (liveSetting == null)
			{
				gameObject.SetActive(false);
				return;
			}
			float settingsRate = liveSetting.NoteShowRate;
			if (Mathf.Approximately(settingsRate, 1f))
			{
				gameObject.SetActive(false);
				return;
			}

			Vector2 position = LiveUtility.CalcNoteShowRatePosition(settingsRate);
			float progress = LiveUtility.CalcNoteShowRate(position);
			if (showLine != null)
			{
				float t = Mathf.Clamp01(settingsRate);
				showLine.size = Vector2.Lerp(maxShowLineSize, minShowLineSize, t);
				showLine.transform.localPosition = new Vector3(0f, position.y + showLine.size.y * 0.5f, 0f);
			}

			if (hideAreaView != null)
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
					hideAreaView.material = _material;
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
				if (hideAreaView != null)
				{
					hideAreaView.material = null;
				}
			}
		}

		public NoteShowRateView()
		{
		}
	}
}
