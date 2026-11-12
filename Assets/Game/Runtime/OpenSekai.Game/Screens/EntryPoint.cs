using Cysharp.Threading.Tasks;
using UnityEngine;
using Sekai;
using Sekai.Core.Live;

namespace Sekai.Core
{
	public class EntryPoint : MonoBehaviour
	{
		public static PlayMode PlayMode { get; set; }

		private void Awake()
		{
			CreateController().Forget();
		}

		private async UniTask<BaseController> CreateController()
		{
			await UniTask.WaitUntil(
				() => ShaderPreloader.Status != ShaderPreloader.PreloadStatus.Loading
					&& ShaderPreloader.Status != ShaderPreloader.PreloadStatus.Compiling,
				PlayerLoopTiming.Update);

			ShaderPreloader.Status = ShaderPreloader.PreloadStatus.None;
			SceneTransitionLoadingIndicator loadingIndicator = FindFirstObjectByType<SceneTransitionLoadingIndicator>();
			if (loadingIndicator != null)
			{
				Destroy(loadingIndicator.gameObject);
			}

			BaseController controller = null;
			switch (PlayMode)
			{
				case PlayMode.SoloLive:
					// The one place that reads the retained boot boundary for a live: the controller
					// receives a context instead of reaching for the statics during its own Awake.
					LiveContext context = LiveContext.FromRetainedBoot();
					Live.SoloLiveController solo = gameObject.AddComponent<Live.SoloLiveController>();
					solo.Initialize(context);
					controller = solo;
					break;
				default:
					Debug.LogWarning($"Unsupported Core PlayMode: {PlayMode}");
					break;
			}

			return controller;
		}
	}
}
