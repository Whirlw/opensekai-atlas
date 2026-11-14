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

			// Keep the view chain alive when a prefab has no LiveViewBase component.
			// The fallback component is added below.
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
