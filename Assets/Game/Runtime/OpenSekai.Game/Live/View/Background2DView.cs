using System.IO;
using Sekai.Core.Live;
using OpenSekai.Shared.Packages;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;
using CriWare;
using Sekai;

namespace Sekai.Live
{
	public class Background2DView : LiveViewBase
	{
		private const string DEFAULT_BACKGROUND_BUNDLE_NAME = "live/2dmode/background/default";
		private const string DEFAULT_BACKGROUND_ASSET_NAME = "default";
		private const float BASE_ASPECT = 16f / 9f;
		private const string MOVIE_QUAD_NAME = "OpenSekaiMovieQuad";
		private const int DEFAULT_MOVIE_TEXTURE_WIDTH = 1920;
		private const int DEFAULT_MOVIE_TEXTURE_HEIGHT = 1080;

		[UnityEngine.Serialization.FormerlySerializedAs("_movieCamera")]
		[SerializeField]
		private Camera movieCamera;

		[UnityEngine.Serialization.FormerlySerializedAs("_jacketCamera")]
		[SerializeField]
		private Camera jacketCamera;

		[UnityEngine.Serialization.FormerlySerializedAs("_jacketModeRoot")]
		[SerializeField]
		private GameObject jacketModeRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_movieModeRoot")]
		[SerializeField]
		private GameObject movieModeRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_backgroundRenderer")]
		[SerializeField]
		private SpriteRenderer backgroundRenderer;

		[UnityEngine.Serialization.FormerlySerializedAs("_movieRenderer")]
		[SerializeField]
		private SpriteRenderer movieRenderer;

		[UnityEngine.Serialization.FormerlySerializedAs("_jackets")]
		[SerializeField]
		private SpriteRenderer[] jackets;

		private BaseLiveController _baseController;
		private LiveBackgroundSkin _skin;
		private LiveBackgroundComposition _composition;
		private Texture2D _externalJacketTexture;
		private Sprite _externalJacketSprite;
		private int _lastRenderedScreenWidth;
		private int _lastRenderedScreenHeight;
		private VideoPlayer _videoPlayer;
		private RenderTexture _movieTexture;
		private GameObject _movieQuadObject;
		private Mesh _movieQuadMesh;
		private MeshRenderer _movieQuadRenderer;
		private Material _movieMaterial;
		private bool _movieModeActive;
		private bool _pendingMoviePlay;
		private bool _movieStarted;
		private float _pendingMovieStartTime;
		private string _moviePlaybackPath;

		private void Awake()
		{
			DisableMovieMode();
			if (jacketCamera != null)
			{
				jacketCamera.enabled = false;
			}
		}

		public override void Setup(BaseLiveController controller)
		{
			_composition?.Dispose();
			_composition = null;
			_skin = (controller?.BootData as FreeLiveBootData)?.BackgroundSkin;
			_baseController = controller;
			DisableMovieMode();
			SetupRenderTarget();
			SetJacketsActive(false);
		}

		public override void OnLoad()
		{
			if ((_skin == null || _skin.PreferVideo) && TrySetupMovie())
			{
				return;
			}

			SetupStaticBackground();
		}

		private void SetupStaticBackground()
		{
			if (_skin != null && jacketCamera != null)
			{
				DisableMovieMode();
				_composition?.Dispose();
				_composition = _skin.Create(jacketCamera, transform, backgroundRenderer.sharedMaterial, _skin.UsesJacket ? LoadJacketTexture() : null);
				if (_composition.HasVisibleLayers)
				{
					jacketModeRoot.SetActive(false);
					RenderComposition();
					return;
				}
				_composition.Dispose();
				_composition = null;
			}
			SetupJacket();
		}

		private void RenderComposition()
		{
			gameObject.SetActive(true);
			_baseController.EnsureBackgroundTextureSizeForCurrentScreen();
			_composition.Render(_baseController.BackgroundTexture);
			_lastRenderedScreenWidth = Screen.width;
			_lastRenderedScreenHeight = Screen.height;
			gameObject.SetActive(false);
		}

		private void SetupRenderTarget()
		{
			bool useMovieCamera = ShouldUseMovieCamera();
			if (jacketCamera != null)
			{
				jacketCamera.enabled = false;
				jacketCamera.targetTexture = useMovieCamera ? null : _baseController?.BackgroundTexture;
			}

			if (movieCamera != null)
			{
				movieCamera.enabled = false;
				movieCamera.targetTexture = useMovieCamera ? _baseController?.BackgroundTexture : null;
			}
		}

		private bool ShouldUseMovieCamera()
		{
			return _baseController?.BootData?.MusicCategory == MusicCategory.mv_2d;
		}

		private void SetupJacket()
		{
			if (jacketModeRoot != null)
			{
				jacketModeRoot.SetActive(true);
			}
			DisableMovieMode();

			LoadDefaultBackground();
			Texture2D jacketTexture = LoadJacketTexture();
			if (jacketTexture != null)
			{
				ApplyJacketTexture(jacketTexture);
			}

			RenderJacketBackground();
			gameObject.SetActive(false);
		}

		private void LoadDefaultBackground()
		{
			if (backgroundRenderer != null)
			{
				Sprite sprite = AssetBundleUtility.LoadAsset<Sprite>(DEFAULT_BACKGROUND_BUNDLE_NAME, DEFAULT_BACKGROUND_ASSET_NAME);
				if (sprite != null)
				{
					backgroundRenderer.sprite = sprite;
				}
				backgroundRenderer.gameObject.SetActive(true);
			}
		}

		public override void OnUpdate(float musicTime)
		{
			if (_movieModeActive)
			{
				if (Screen.width != _lastRenderedScreenWidth || Screen.height != _lastRenderedScreenHeight)
				{
					UpdateMovieRenderTarget();
				}
				return;
			}

			if (Screen.width == _lastRenderedScreenWidth && Screen.height == _lastRenderedScreenHeight)
			{
				return;
			}

			if (_composition != null) RenderComposition();
			else RenderJacketBackground();
		}

		public override void MusicStart(float musicTime)
		{
			// The info page is up now, but the music does not start until it is gone: only get the
			// movie ready here, and start it with the audio.
			if (!_movieModeActive || _videoPlayer == null || _videoPlayer.isPrepared)
			{
				return;
			}

			_videoPlayer.Prepare();
		}

		public override void MusicPlaybackStart(float musicTime)
		{
			if (!_movieModeActive)
			{
				return;
			}

			_movieStarted = true;
			PlayMovieAt(Mathf.Max(GetInitialMovieStartTime(), musicTime));
		}

		public override void MusicSeek(float musicTime)
		{
			if (!_movieModeActive || !_movieStarted || _videoPlayer == null)
			{
				return;
			}

			if (!_videoPlayer.isPrepared)
			{
				_pendingMovieStartTime = Mathf.Max(0f, musicTime);
				return;
			}

			if (_videoPlayer.canSetTime)
			{
				_videoPlayer.time = Mathf.Max(0f, musicTime);
			}
		}

		public override void Pause()
		{
			if (_movieModeActive && _videoPlayer != null && _videoPlayer.isPlaying)
			{
				_videoPlayer.Pause();
			}
		}

		public override void Resume(float musicTime)
		{
			// A pause before the music started (the run-in) must not start the movie early.
			if (!_movieModeActive || !_movieStarted)
			{
				return;
			}

			if (_videoPlayer == null || !_videoPlayer.isPrepared)
			{
				PlayMovieAt(Mathf.Max(GetInitialMovieStartTime(), musicTime));
				return;
			}

			_videoPlayer.Play();
		}

		public override void Retry()
		{
			if (!_movieModeActive)
			{
				return;
			}

			_pendingMoviePlay = false;
			_movieStarted = false;
			_pendingMovieStartTime = 0f;
			if (_videoPlayer != null)
			{
				_videoPlayer.Stop();
				_videoPlayer.Prepare();
			}
		}

		public override void Finish()
		{
			StopMovie();
		}

		public override void Finish(float duration)
		{
			Finish();
		}

		public override void OnUnload()
		{
			StopMovie();
		}

		private void RenderJacketBackground()
		{
			if (jacketCamera == null)
			{
				return;
			}

			bool wasActive = gameObject.activeSelf;
			if (!wasActive)
			{
				gameObject.SetActive(true);
			}

			if (jacketModeRoot != null)
			{
				jacketModeRoot.SetActive(true);
			}
			DisableMovieMode();

			_baseController?.EnsureBackgroundTextureSizeForCurrentScreen();
			jacketCamera.targetTexture = _baseController?.BackgroundTexture;
			float aspect = GetRenderTargetAspect();
			if (aspect > 0f)
			{
				jacketCamera.aspect = aspect;
				jacketCamera.ResetProjectionMatrix();
			}
			jacketCamera.enabled = true;
			jacketCamera.Render();
			jacketCamera.enabled = false;
			_lastRenderedScreenWidth = Screen.width;
			_lastRenderedScreenHeight = Screen.height;

			if (!wasActive)
			{
				gameObject.SetActive(false);
			}
		}

		private bool TrySetupMovie()
		{
			string moviePath = ResolveCustomMoviePath();
			if (!ShouldUseMovieCamera() || string.IsNullOrEmpty(moviePath) || !File.Exists(moviePath) || movieCamera == null || movieRenderer == null)
			{
				return false;
			}
			_moviePlaybackPath = LiveMediaFiles.PrepareMoviePlaybackPath(moviePath);
			if (string.IsNullOrEmpty(_moviePlaybackPath) || !File.Exists(_moviePlaybackPath))
			{
				return false;
			}

			gameObject.SetActive(true);
			_movieModeActive = true;
			_pendingMoviePlay = false;
			_pendingMovieStartTime = 0f;
			SetJacketsActive(false);
			if (jacketModeRoot != null)
			{
				jacketModeRoot.SetActive(false);
			}
			if (movieModeRoot != null)
			{
				movieModeRoot.SetActive(true);
			}
			movieRenderer.gameObject.SetActive(true);
			movieRenderer.enabled = false;

			EnsureMovieQuad();
			SetupVideoPlayer(_moviePlaybackPath);
			UpdateMovieRenderTarget();
			movieCamera.enabled = true;
			return true;
		}

		private string ResolveCustomMoviePath()
		{
			FreeLiveBootData bootData = _baseController?.BootData as FreeLiveBootData;
			if (bootData == null || string.IsNullOrEmpty(bootData.CustomMusicScorePath))
			{
				return null;
			}

			// Only the resolved paths are needed here: reading the package manifest is packaging, and
			// the App-side entry wrapper would also build an audio object for nothing.
			SongPackage package = SongPackageStorage.LoadEntry(bootData.CustomMusicScorePath);
			string moviePath = package?.VideoPath;
			return string.IsNullOrEmpty(moviePath) ? null : moviePath;
		}

		private void SetupVideoPlayer(string moviePath)
		{
			if (_videoPlayer == null)
			{
				_videoPlayer = movieRenderer.GetComponent<VideoPlayer>();
			}
			if (_videoPlayer == null)
			{
				_videoPlayer = movieRenderer.gameObject.AddComponent<VideoPlayer>();
			}

			_videoPlayer.prepareCompleted -= OnMoviePrepared;
			_videoPlayer.errorReceived -= OnMovieError;
			_videoPlayer.prepareCompleted += OnMoviePrepared;
			_videoPlayer.errorReceived += OnMovieError;
			_videoPlayer.playOnAwake = false;
			_videoPlayer.isLooping = false;
			_videoPlayer.waitForFirstFrame = true;
			_videoPlayer.skipOnDrop = true;
			_videoPlayer.timeReference = VideoTimeReference.Freerun;
			_videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
			_videoPlayer.renderMode = VideoRenderMode.RenderTexture;
			_videoPlayer.source = VideoSource.Url;
			_videoPlayer.url = ToFileUri(moviePath);
			_videoPlayer.targetTexture = EnsureMovieTexture(DEFAULT_MOVIE_TEXTURE_WIDTH, DEFAULT_MOVIE_TEXTURE_HEIGHT);
			ApplyMovieTexture();
			_videoPlayer.Prepare();
		}

		private void OnMoviePrepared(VideoPlayer source)
		{
			if (!_movieModeActive || source != _videoPlayer)
			{
				return;
			}

			int width = source.width > 0 ? (int)source.width : DEFAULT_MOVIE_TEXTURE_WIDTH;
			int height = source.height > 0 ? (int)source.height : DEFAULT_MOVIE_TEXTURE_HEIGHT;
			source.targetTexture = EnsureMovieTexture(width, height);
			ApplyMovieTexture();
			UpdateMovieRenderTarget();
			if (_pendingMoviePlay)
			{
				PlayMovieAt(_pendingMovieStartTime);
			}
		}

		private void OnMovieError(VideoPlayer source, string message)
		{
			if (!_movieModeActive || source != _videoPlayer)
			{
				return;
			}

			Debug.LogWarningFormat("2DMV playback failed. url:{0} error:{1}", source != null ? source.url : string.Empty, message);
			_movieModeActive = false;
			StopMovie();
			SetupStaticBackground();
		}

		private RenderTexture EnsureMovieTexture(int width, int height)
		{
			width = Mathf.Max(1, width);
			height = Mathf.Max(1, height);
			if (_movieTexture != null && _movieTexture.width == width && _movieTexture.height == height)
			{
				return _movieTexture;
			}

			if (_movieTexture != null)
			{
				_movieTexture.Release();
				Destroy(_movieTexture);
			}
			_movieTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
			_movieTexture.name = "OpenSekai 2DMV Texture";
			_movieTexture.Create();
			return _movieTexture;
		}

		private void EnsureMovieQuad()
		{
			if (_movieQuadObject != null)
			{
				_movieQuadObject.SetActive(true);
				return;
			}

			Transform parent = movieModeRoot != null ? movieModeRoot.transform : transform;
			_movieQuadObject = new GameObject(MOVIE_QUAD_NAME);
			_movieQuadObject.layer = gameObject.layer;
			_movieQuadObject.transform.SetParent(parent, false);
			_movieQuadObject.transform.localPosition = Vector3.zero;
			_movieQuadObject.transform.localRotation = Quaternion.identity;
			MeshFilter meshFilter = _movieQuadObject.AddComponent<MeshFilter>();
			_movieQuadMesh = CreateMovieQuadMesh();
			meshFilter.sharedMesh = _movieQuadMesh;
			_movieQuadRenderer = _movieQuadObject.AddComponent<MeshRenderer>();
			_movieQuadRenderer.shadowCastingMode = ShadowCastingMode.Off;
			_movieQuadRenderer.receiveShadows = false;
			_movieMaterial = CreateMovieMaterial();
			_movieQuadRenderer.sharedMaterial = _movieMaterial;
		}

		private static Mesh CreateMovieQuadMesh()
		{
			Mesh mesh = new Mesh();
			mesh.name = "OpenSekai 2DMV Quad";
			mesh.vertices = new[]
			{
				new Vector3(-0.5f, -0.5f, 0f),
				new Vector3(0.5f, -0.5f, 0f),
				new Vector3(-0.5f, 0.5f, 0f),
				new Vector3(0.5f, 0.5f, 0f)
			};
			mesh.uv = new[]
			{
				new Vector2(0f, 0f),
				new Vector2(1f, 0f),
				new Vector2(0f, 1f),
				new Vector2(1f, 1f)
			};
			mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
			mesh.RecalculateBounds();
			return mesh;
		}

		private Material CreateMovieMaterial()
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				shader = Shader.Find("Sprites/Default");
			}

			Material material = shader != null
				? new Material(shader)
				: new Material(movieRenderer.sharedMaterial);
			material.name = "OpenSekai 2DMV Material";
			if (material.HasProperty("_Cull"))
			{
				material.SetFloat("_Cull", (float)CullMode.Off);
			}
			return material;
		}

		private void ApplyMovieTexture()
		{
			if (_movieMaterial == null || _movieTexture == null)
			{
				return;
			}

			_movieMaterial.mainTexture = _movieTexture;
			if (_movieMaterial.HasProperty("_BaseMap"))
			{
				_movieMaterial.SetTexture("_BaseMap", _movieTexture);
			}
			float brightness = Mathf.Clamp01(_baseController?.Settings?.MovieBrightness ?? 1f);
			var tint = new Color(brightness, brightness, brightness, 1f);
			if (_movieMaterial.HasProperty("_Color"))
			{
				_movieMaterial.SetColor("_Color", tint);
			}
			if (_movieMaterial.HasProperty("_BaseColor"))
			{
				_movieMaterial.SetColor("_BaseColor", tint);
			}
		}

		private void UpdateMovieRenderTarget()
		{
			if (movieCamera == null)
			{
				return;
			}

			_baseController?.EnsureBackgroundTextureSizeForCurrentScreen();
			movieCamera.targetTexture = _baseController?.BackgroundTexture;
			float aspect = GetRenderTargetAspect(movieCamera);
			if (aspect > 0f)
			{
				movieCamera.aspect = aspect;
				movieCamera.ResetProjectionMatrix();
			}
			UpdateMovieQuadScale(aspect);
			_lastRenderedScreenWidth = Screen.width;
			_lastRenderedScreenHeight = Screen.height;
		}

		private void UpdateMovieQuadScale(float targetAspect)
		{
			if (_movieQuadObject == null || movieCamera == null)
			{
				return;
			}

			if (targetAspect <= 0f)
			{
				targetAspect = BASE_ASPECT;
			}
			float videoAspect = GetMovieAspect();
			float viewHeight = movieCamera.orthographic ? movieCamera.orthographicSize * 2f : 10f;
			float viewWidth = viewHeight * targetAspect;
			float quadWidth = viewWidth;
			float quadHeight = viewHeight;
			if (videoAspect > targetAspect)
			{
				quadWidth = quadHeight * videoAspect;
			}
			else if (videoAspect > 0f)
			{
				quadHeight = quadWidth / videoAspect;
			}

			_movieQuadObject.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
		}

		private float GetMovieAspect()
		{
			if (_videoPlayer != null && _videoPlayer.width > 0 && _videoPlayer.height > 0)
			{
				return (float)_videoPlayer.width / _videoPlayer.height;
			}
			if (_movieTexture != null && _movieTexture.height > 0)
			{
				return (float)_movieTexture.width / _movieTexture.height;
			}
			return BASE_ASPECT;
		}

		private void PlayMovieAt(float musicTime)
		{
			if (_videoPlayer == null)
			{
				return;
			}

			_pendingMoviePlay = true;
			_pendingMovieStartTime = Mathf.Max(0f, musicTime);
			if (!_videoPlayer.isPrepared)
			{
				_videoPlayer.Prepare();
				return;
			}

			_pendingMoviePlay = false;
			if (_videoPlayer.canSetTime && _pendingMovieStartTime > 0.05f)
			{
				_videoPlayer.time = _pendingMovieStartTime;
			}
			_videoPlayer.Play();
		}

		private float GetInitialMovieStartTime()
		{
			long startMusicTimeMs = _baseController?.BootData?.MusicData?.StartMusicTimeMs ?? 0L;
			if (startMusicTimeMs >= 1L)
			{
				return startMusicTimeMs / 1000f;
			}
			return 0f;
		}

		private void StopMovie()
		{
			_pendingMoviePlay = false;
			_movieStarted = false;
			_pendingMovieStartTime = 0f;
			if (_videoPlayer != null)
			{
				_videoPlayer.Stop();
			}
		}

		private float GetRenderTargetAspect()
		{
			return GetRenderTargetAspect(jacketCamera);
		}

		private float GetRenderTargetAspect(Camera targetCamera)
		{
			RenderTexture targetTexture = targetCamera != null ? targetCamera.targetTexture : null;
			if (targetTexture != null && targetTexture.height > 0)
			{
				return (float)targetTexture.width / targetTexture.height;
			}

			return Screen.height > 0 ? (float)Screen.width / Screen.height : BASE_ASPECT;
		}

		private void DisableMovieMode()
		{
			_movieModeActive = false;
			StopMovie();
			if (movieModeRoot != null)
			{
				movieModeRoot.SetActive(false);
			}
			if (_movieQuadObject != null)
			{
				_movieQuadObject.SetActive(false);
			}
			if (movieRenderer != null)
			{
				movieRenderer.enabled = false;
				movieRenderer.gameObject.SetActive(false);
			}
			if (movieCamera != null)
			{
				movieCamera.enabled = false;
				movieCamera.targetTexture = null;
			}
		}

		private Texture2D LoadJacketTexture()
		{
			Texture2D customTexture = LoadCustomJacketTexture(_baseController?.BootData as FreeLiveBootData);
			if (customTexture != null)
			{
				return customTexture;
			}

			int musicId = _baseController?.BootData?.MusicData?.Music?.id ?? 0;
			if (musicId <= 0)
			{
				return null;
			}

			string resourceName = $"jacket_s_{musicId}";
			Texture2D texture = AssetBundleUtility.LoadAsset<Texture2D>("thumbnail/music_jacket", resourceName, false);
			return texture != null
				? texture
				: AssetBundleUtility.LoadAsset<Texture2D>("startapp/thumbnail/music_jacket", resourceName, false);
		}

		private Texture2D LoadCustomJacketTexture(FreeLiveBootData bootData)
		{
			ClearExternalJacketAssets();
			_externalJacketTexture = LiveMediaFiles.LoadCustomJacketTexture(bootData);
			return _externalJacketTexture;
		}

		private void ApplyJacketTexture(Texture2D texture)
		{
			if (jackets == null)
			{
				return;
			}

			if (_externalJacketSprite != null)
			{
				Destroy(_externalJacketSprite);
				_externalJacketSprite = null;
			}

			int side = Mathf.Min(texture.width, texture.height);
			var region = new Rect((texture.width - side) * 0.5f, (texture.height - side) * 0.5f, side, side);
			_externalJacketSprite = Sprite.Create(texture, region, new Vector2(0.5f, 0.5f),
				100f, 0, SpriteMeshType.FullRect);

			foreach (SpriteRenderer jacket in jackets)
			{
				if (jacket != null)
				{
					Vector2 size = SizeToCoverMask(jacket);
					jacket.sprite = _externalJacketSprite;
					jacket.drawMode = SpriteDrawMode.Sliced;
					jacket.size = size;
					jacket.gameObject.SetActive(true);
				}
			}
		}

		private Vector2 SizeToCoverMask(SpriteRenderer jacket)
		{
			Vector2 size = jacket.size;
			Transform surface = jacket.transform;
			SpriteMask mask = surface.parent != null ? surface.parent.GetComponentInChildren<SpriteMask>(true) : null;
			if (jacketCamera == null || mask == null || mask.sprite == null || size.x <= 0f || size.y <= 0f)
				return size;

			// The mask and the art are on differently tilted planes. Project the mask's
			// corners onto the art plane so the image fills the visible frame from the camera.
			Bounds bounds = mask.sprite.bounds;
			var plane = new Plane(surface.forward, surface.position);
			float scale = 1f;
			for (int corner = 0; corner < 4; corner++)
			{
				var edge = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
					(corner & 2) == 0 ? bounds.min.y : bounds.max.y, bounds.center.z);
				Vector3 viewport = jacketCamera.WorldToViewportPoint(mask.transform.TransformPoint(edge));
				Ray ray = jacketCamera.ViewportPointToRay(viewport);
				if (!plane.Raycast(ray, out float distance)) continue;

				Vector3 point = surface.InverseTransformPoint(ray.GetPoint(distance));
				scale = Mathf.Max(scale, Mathf.Max(2f * Mathf.Abs(point.x) / size.x, 2f * Mathf.Abs(point.y) / size.y));
			}

			return size * scale;
		}

		private void SetJacketsActive(bool active)
		{
			if (jackets == null)
			{
				return;
			}

			foreach (SpriteRenderer jacket in jackets)
			{
				if (jacket != null)
				{
					jacket.gameObject.SetActive(active);
				}
			}
		}

		private void OnDestroy()
		{
			_composition?.Dispose();
			if (_videoPlayer != null)
			{
				_videoPlayer.prepareCompleted -= OnMoviePrepared;
				_videoPlayer.errorReceived -= OnMovieError;
			}
			ClearExternalJacketAssets();
			if (_movieTexture != null)
			{
				_movieTexture.Release();
				Destroy(_movieTexture);
				_movieTexture = null;
			}
			if (_movieMaterial != null)
			{
				Destroy(_movieMaterial);
				_movieMaterial = null;
			}
			if (_movieQuadMesh != null)
			{
				Destroy(_movieQuadMesh);
				_movieQuadMesh = null;
			}
		}

		private void ClearExternalJacketAssets()
		{
			if (_externalJacketSprite != null)
			{
				Destroy(_externalJacketSprite);
				_externalJacketSprite = null;
			}
			if (_externalJacketTexture != null)
			{
				Destroy(_externalJacketTexture);
				_externalJacketTexture = null;
			}
		}

		private static string ToFileUri(string path)
		{
			return new System.Uri(Path.GetFullPath(path)).AbsoluteUri;
		}
	}
}
