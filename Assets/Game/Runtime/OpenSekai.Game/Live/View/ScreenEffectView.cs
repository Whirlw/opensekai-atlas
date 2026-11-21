using UnityEngine;
using Sekai;

namespace Sekai.Live
{
	public class ScreenEffectView : MonoBehaviour
	{
		private ParticleSystemController _damageEffectController;

		public void Setup(LiveHitEffectSkin skin = null)
		{
			_damageEffectController = CreateEffect(LivePrefabDefine.FX_SCREEN_DAMAGE, skin);
			RefreshScreenSize();
		}

		public void RefreshScreenSize()
		{
			float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7778f;
			transform.localScale = Vector3.one * Mathf.Max(1.7778f / aspect, 1f);
			if (_damageEffectController != null)
			{
				_damageEffectController.transform.localScale = new Vector3(aspect * 10f, 10f, 1f);
			}
		}

		public void Excute(INote note)
		{
			if (note == null || note.Result == NoteResult.Pass || note.Category == NoteCategory.Hidden)
			{
				return;
			}

			if (note.Result == NoteResult.Miss || note.Result == NoteResult.Bad)
			{
				_damageEffectController?.Play();
			}
		}

		private ParticleSystemController CreateEffect(string effectName, LiveHitEffectSkin skin)
		{
			float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7778f;
			GameObject prefab = LiveHitEffectSkin.LoadPrefab(LiveConfig.EffectBundleName, effectName, skin);
			if (prefab == null)
			{
				return null;
			}

			var controller = ParticleSystemController.Create(prefab, transform, skin);
			controller.Place(prefab.transform.localPosition);
			controller.transform.localScale = new Vector3(aspect * 10f, 10f, 1f);
			return controller;
		}
	}
}
