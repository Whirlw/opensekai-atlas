using Sekai.Core.Live;
using System.Collections.Generic;
using UnityEngine;
using Sekai;

namespace Sekai.Live
{
	public static class LiveViewFactory
	{
		public static LiveViewBase[] Create(LiveBootDataBase bootData, Transform root)
		{
			List<LiveViewBase> views = new List<LiveViewBase>();
			views.Add(LoadLiveView("Live/View/FrontUIView", root));
			views.Add(LoadLiveView("Live/View/Background2DView", root));
			return views.ToArray();
		}

		private static LiveViewBase LoadLiveView(string path, Transform root)
		{
			GameObject prefab = Resources.Load<GameObject>(path);
			if (prefab == null)
			{
				Debug.LogWarning($"Live view prefab was not found. path: {path}");
				return CreateFallbackView(path, root);
			}

			GameObject instance = Object.Instantiate(prefab, root, false);
			LiveViewBase view = instance.GetComponent<LiveViewBase>();
			if (view != null)
			{
				return view;
			}

			// Some restored prefabs still have child dependencies pending; keep the chain alive
			// while their concrete view scripts are restored from IDA.
			Debug.LogWarning($"Live view prefab has no LiveViewBase component. path: {path}");
			return instance.AddComponent<LiveViewBase>();
		}

		private static LiveViewBase CreateFallbackView(string path, Transform root)
		{
			GameObject liveViewObject = new GameObject(path.Substring(path.LastIndexOf('/') + 1));
			liveViewObject.transform.SetParent(root, false);
			return liveViewObject.AddComponent<LiveViewBase>();
		}
	}
}
