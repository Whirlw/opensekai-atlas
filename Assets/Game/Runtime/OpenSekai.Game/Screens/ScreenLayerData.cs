using UnityEngine;
using UnityEngine.Serialization;

namespace Sekai
{
	public class ScreenLayerData : ScriptableObject
	{
		[FormerlySerializedAs("ScreenType")]
		[SerializeField]
		private MenuScreenType screenType;

		public MenuScreenType ScreenType => screenType;

		[FormerlySerializedAs("DisplayLayer")]
		[SerializeField]
		private DisplayLayerType displayLayer;

		public DisplayLayerType DisplayLayer => displayLayer;

		[FormerlySerializedAs("StartAnimationType")]
		[SerializeField]
		private ScreenInAnimType startAnimationType;

		public ScreenInAnimType StartAnimationType => startAnimationType;

		[FormerlySerializedAs("MaskInTexture")]
		[SerializeField]
		private Texture2D maskInTexture;

		public Texture2D MaskInTexture => maskInTexture;

		[FormerlySerializedAs("MaskInUV")]
		[SerializeField]
		private Rect maskInUV;

		public Rect MaskInUV => maskInUV;

		[FormerlySerializedAs("MaskInHaveDirection")]
		[SerializeField]
		private bool maskInHaveDirection;

		public bool MaskInHaveDirection => maskInHaveDirection;

		[FormerlySerializedAs("ExitAnimationType")]
		[SerializeField]
		private ScreenOutAnimType exitAnimationType;

		public ScreenOutAnimType ExitAnimationType => exitAnimationType;

		[FormerlySerializedAs("MaskOutTexture")]
		[SerializeField]
		private Texture2D maskOutTexture;

		public Texture2D MaskOutTexture => maskOutTexture;

		[FormerlySerializedAs("MaskOutUV")]
		[SerializeField]
		private Rect maskOutUV;

		public Rect MaskOutUV => maskOutUV;

		[FormerlySerializedAs("MaskOutHaveDirection")]
		[SerializeField]
		private bool maskOutHaveDirection;

		public bool MaskOutHaveDirection => maskOutHaveDirection;

		[FormerlySerializedAs("DisplayHeader")]
		[SerializeField]
		private HeaderDisplay displayHeader;

		public HeaderDisplay DisplayHeader => displayHeader;

		[FormerlySerializedAs("DisplayCategory")]
		[SerializeField]
		private HeaderCategory displayCategory;

		public HeaderCategory DisplayCategory => displayCategory;

		[FormerlySerializedAs("DisplayPlayerInfo")]
		[SerializeField]
		private HeaderDisplay displayPlayerInfo;

		public HeaderDisplay DisplayPlayerInfo => displayPlayerInfo;

		[FormerlySerializedAs("DisplayBackUIScreen")]
		[SerializeField]
		private HeaderDisplay displayBackUIScreen;

		public HeaderDisplay DisplayBackUIScreen => displayBackUIScreen;

		[FormerlySerializedAs("EnableBackUIScreen")]
		[SerializeField]
		private bool enableBackUIScreen;

		public bool EnableBackUIScreen => enableBackUIScreen;

		[FormerlySerializedAs("DisplayScreenName")]
		[SerializeField]
		private HeaderDisplay displayScreenName;

		public HeaderDisplay DisplayScreenName => displayScreenName;

		[FormerlySerializedAs("ScreenName")]
		[SerializeField]
		private string screenName;

		public string ScreenName => screenName;

		[FormerlySerializedAs("ScreenSubName")]
		[SerializeField]
		private string screenSubName;

		public string ScreenSubName => screenSubName;

		[FormerlySerializedAs("EnableTapScreenAnimation")]
		[SerializeField]
		private bool enableTapScreenAnimation;

		public bool EnableTapScreenAnimation => enableTapScreenAnimation;

		[FormerlySerializedAs("IncludeChildCanvasForAlphaTransiton")]
		[SerializeField]
		private bool includeChildCanvasForAlphaTransiton;

		public bool IncludeChildCanvasForAlphaTransiton => includeChildCanvasForAlphaTransiton;

		[SerializeField]
		private bool hasLayerCamera;

		public bool HasLayerCamera => hasLayerCamera;

		[SerializeField]
		private int layerCameraPriority;

		public int LayerCameraPriority => layerCameraPriority;

		[SerializeField]
		private ScreenLayerBgmType bgmType;

		public ScreenLayerBgmType BgmType => bgmType;

		[FormerlySerializedAs("BackgroundType")]
		[SerializeField]
		private ScreenLayerBackgroundType backgroundType;

		public ScreenLayerBackgroundType BackgroundType => backgroundType;

		public GameObject LoadScreenLayerPrefab()
		{
			// Try the legacy resource path first, then the lowercase path used by this project.
			var prefab = Resources.Load<GameObject>("Screen/Prefabs/" + name);
			return prefab != null ? prefab : Resources.Load<GameObject>("screen/prefabs/" + name);
		}
	}
}
