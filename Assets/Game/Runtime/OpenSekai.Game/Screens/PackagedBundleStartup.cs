using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sekai
{
	/// <summary>Both player scenes must have a catalog before their first synchronous bundle load.</summary>
	public sealed class PackagedBundleStartup : MonoBehaviour
	{
		private bool _retry;

		public static async UniTask<bool> WaitForReadyAsync(MonoBehaviour entry)
		{
			var token = entry.destroyCancellationToken;
			while (!token.IsCancellationRequested)
			{
				try
				{
					if (await AssetBundleManager.Instance.PrepareStreamingAssetsAsync(token)) return true;
					Debug.LogError("Packaged AssetBundle catalog is empty.");
				}
				catch (OperationCanceledException) when (token.IsCancellationRequested || Application.exitCancellationToken.IsCancellationRequested)
				{
					return false;
				}
				catch (Exception error)
				{
					Debug.LogError("Packaged AssetBundle startup failed: " + error.Message);
				}

				var prompt = entry.gameObject.AddComponent<PackagedBundleStartup>();
				bool cancelled = await UniTask.WaitUntil(() => prompt._retry, cancellationToken: token)
					.SuppressCancellationThrow();
				if (cancelled) return false;
				Destroy(prompt);
			}
			return false;
		}

		// The normal screen and font assets may themselves be unavailable at this point.
		private void OnGUI()
		{
			float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 640f, Screen.height / 360f));
			var previous = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width / 2f, Screen.height / 2f), Quaternion.identity,
				new Vector3(scale, scale, 1f));
			GUI.Box(new Rect(-190, -56, 380, 112), "游戏资源加载失败");
			if (GUI.Button(new Rect(-70, -4, 140, 44), "重新加载")) _retry = true;
			GUI.matrix = previous;
		}
	}
}
