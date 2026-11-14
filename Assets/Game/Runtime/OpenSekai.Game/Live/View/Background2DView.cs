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

		[SerializeField]
		private Camera _movieCamera;

		[SerializeField]
		private Camera _jacketCamera;

		[SerializeField]
		private GameObject _jacketModeRoot;

		[SerializeField]
		private GameObject _movieModeRoot;

		[SerializeField]
		private SpriteRenderer _backgroundRenderer;

		[SerializeField]
		private SpriteRenderer _movieRenderer;

		[SerializeField]
		private SpriteRenderer[] _jackets;

		private BaseLiveController _baseController;
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
		private float _pendingMovieStartTime;
		private string _moviePlaybackPath;

		private void Awake()
		{
			DisableMovieMode();
			if (_jacketCamera != null)
			{
				_jacketCamera.enabled = false;
			}
		}

		public override void Setup(BaseLiveController controller)
		{
			_baseController = controller;
			DisableMovieMode();
			SetupRenderTarget();
			SetJacketsActive(false);
		}

		public override void OnLoad()
		{
			if (TrySetupMovie())
			{
				return;
			}

			SetupJacket();
		}

		private void SetupRenderTarget()
		{
			bool useMovieCamera = ShouldUseMovieCamera();
			if (_jacketCamera != null)
			{
				_jacketCamera.enabled = false;
				_jacketCamera.targetTexture = useMovieCamera ? null : _baseController?.BackgroundTexture;
			}

			if (_movieCamera != null)
			{
				_movieCamera.enabled = false;
				_movieCamera.targetTexture = useMovieCamera ? _baseController?.BackgroundTexture : null;
			}
		}

		private bool ShouldUseMovieCamera()
		{
			return _baseController?.BootData?.MusicCategory == MusicCategory.mv_2d;
		}

		private void SetupJacket()
		{
			if (_jacketModeRoot != null)
			{
				_jacketModeRoot.SetActive(true);
			}
			DisableMovieMode();

			LoadDefaultBackground();
			Texture2D jacketTexture = LoadJacketTexture();
			if (jacketTexture != null)
			{
				ApplyJacketTexture(jacketTexture, destroyWhenReplaced: IsExternalJacketTexture(jacketTexture));
			}

			RenderJacketBackground();
			gameObject.SetActive(false);
		}

		private void LoadDefaultBackground()
		{
			if (_backgroundRenderer != null)
			{
				Sprite sprite = AssetBundleUtility.LoadAsset<Sprite>(DEFAULT_BACKGROUND_BUNDLE_NAME, DEFAULT_BACKGROUND_ASSET_NAME);
				if (sprite != null)
				{
					_backgroundRenderer.sprite = sprite;
				}
				_backgroundRenderer.gameObject.SetActive(true);
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

			RenderJacketBackground();
		}

		public override void MusicStart(float musicTime)
		{
			if (!_movieModeActive)
			{
				return;
			}

			PlayMovieAt(GetInitialMovieStartTime());
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
			if (!_movieModeActive)
			{
				return;
			}

			if (_videoPlayer == null || !_videoPlayer.isPrepared)
			{
				PlayMovieAt(GetInitialMovieStartTime());
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
			if (_jacketCamera == null)
			{
				return;
			}

			bool wasActive = gameObject.activeSelf;
			if (!wasActive)
			{
				gameObject.SetActive(true);
			}

			if (_jacketModeRoot != null)
			{
				_jacketModeRoot.SetActive(true);
			}
			DisableMovieMode();

			_baseController?.EnsureBackgroundTextureSizeForCurrentScreen();
			_jacketCamera.targetTexture = _baseController?.BackgroundTexture;
			float aspect = GetRenderTargetAspect();
			if (aspect > 0f)
			{
				_jacketCamera.aspect = aspect;
				_jacketCamera.ResetProjectionMatrix();
			}
			_jacketCamera.enabled = true;
			_jacketCamera.Render();
			_jacketCamera.enabled = false;
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
			if (!ShouldUseMovieCamera() || string.IsNullOrEmpty(moviePath) || !File.Exists(moviePath) || _movieCamera == null || _movieRenderer == null)
			{
				return false;
			}
			_moviePlaybackPath = PrepareMoviePlaybackPath(moviePath);
			if (string.IsNullOrEmpty(_moviePlaybackPath) || !File.Exists(_moviePlaybackPath))
			{
				return false;
			}

			_movieModeActive = true;
			_pendingMoviePlay = false;
			_pendingMovieStartTime = 0f;
			SetJacketsActive(false);
			if (_jacketModeRoot != null)
			{
				_jacketModeRoot.SetActive(false);
			}
			if (_movieModeRoot != null)
			{
				_movieModeRoot.SetActive(true);
			}
			_movieRenderer.gameObject.SetActive(true);
			_movieRenderer.enabled = false;

			EnsureMovieQuad();
			SetupVideoPlayer(_moviePlaybackPath);
			UpdateMovieRenderTarget();
			_movieCamera.enabled = true;
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
				_videoPlayer = _movieRenderer.GetComponent<VideoPlayer>();
			}
			if (_videoPlayer == null)
			{
				_videoPlayer = _movieRenderer.gameObject.AddComponent<VideoPlayer>();
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
			if (source != _videoPlayer)
			{
				return;
			}

			Debug.LogWarningFormat("2DMV playback failed. url:{0} error:{1}", source != null ? source.url : string.Empty, message);
			_movieModeActive = false;
			StopMovie();
			SetupJacket();
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

			Transform parent = _movieModeRoot != null ? _movieModeRoot.transform : transform;
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
				shader = Shader.Find("Unlit/Texture");
			}
			if (shader == null)
			{
				shader = Shader.Find("Sprites/Default");
			}

			Material material = shader != null
				? new Material(shader)
				: new Material(_movieRenderer.sharedMaterial);
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
			if (_movieMaterial.HasProperty("_Color"))
			{
				_movieMaterial.SetColor("_Color", Color.white);
			}
			if (_movieMaterial.HasProperty("_BaseColor"))
			{
				_movieMaterial.SetColor("_BaseColor", Color.white);
			}
		}

		private void UpdateMovieRenderTarget()
		{
			if (_movieCamera == null)
			{
				return;
			}

			_baseController?.EnsureBackgroundTextureSizeForCurrentScreen();
			_movieCamera.targetTexture = _baseController?.BackgroundTexture;
			float aspect = GetRenderTargetAspect(_movieCamera);
			if (aspect > 0f)
			{
				_movieCamera.aspect = aspect;
				_movieCamera.ResetProjectionMatrix();
			}
			UpdateMovieQuadScale(aspect);
			_lastRenderedScreenWidth = Screen.width;
			_lastRenderedScreenHeight = Screen.height;
		}

		private void UpdateMovieQuadScale(float targetAspect)
		{
			if (_movieQuadObject == null || _movieCamera == null)
			{
				return;
			}

			if (targetAspect <= 0f)
			{
				targetAspect = BASE_ASPECT;
			}
			float videoAspect = GetMovieAspect();
			float viewHeight = _movieCamera.orthographic ? _movieCamera.orthographicSize * 2f : 10f;
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

		private string PrepareMoviePlaybackPath(string moviePath)
		{
#if UNITY_ANDROID
			return CopyMovieToAsciiCachePath(moviePath);
#else
			return moviePath;
#endif
		}

#if UNITY_ANDROID
		private string CopyMovieToAsciiCachePath(string moviePath)
		{
			try
			{
				FileInfo sourceInfo = new FileInfo(moviePath);
				if (!sourceInfo.Exists)
				{
					return null;
				}

				string cacheDirectory = Path.Combine(Application.temporaryCachePath, "OpenSekai2DMV");
				Directory.CreateDirectory(cacheDirectory);
				string cachePath = Path.Combine(cacheDirectory, CreateAsciiMovieCacheFileName(sourceInfo));
				FileInfo cacheInfo = new FileInfo(cachePath);
				if (!cacheInfo.Exists || cacheInfo.Length != sourceInfo.Length || cacheInfo.LastWriteTimeUtc != sourceInfo.LastWriteTimeUtc)
				{
					File.Copy(sourceInfo.FullName, cachePath, true);
					File.SetLastWriteTimeUtc(cachePath, sourceInfo.LastWriteTimeUtc);
				}
				return cachePath;
			}
			catch (System.Exception exception)
			{
				Debug.LogWarningFormat("Failed to prepare 2DMV cache. path:{0} error:{1}", moviePath, exception.Message);
				return null;
			}
		}

		private static string CreateAsciiMovieCacheFileName(FileInfo sourceInfo)
		{
			string extension = NormalizeMovieExtension(sourceInfo.Extension);
			uint hash = 2166136261U;
			AddStringToHash(ref hash, sourceInfo.FullName);
			AddStringToHash(ref hash, sourceInfo.Length.ToString());
			AddStringToHash(ref hash, sourceInfo.LastWriteTimeUtc.Ticks.ToString());
			return "video_" + hash.ToString("x8") + extension;
		}

		private static string NormalizeMovieExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension) || extension.Length > 8)
			{
				return ".mp4";
			}

			extension = extension.ToLowerInvariant();
			for (int i = 0; i < extension.Length; i++)
			{
				char c = extension[i];
				if (i == 0)
				{
					if (c != '.')
					{
						return ".mp4";
					}
				}
				else if ((c < '0' || c > '9') && (c < 'a' || c > 'z'))
				{
					return ".mp4";
				}
			}
			return extension;
		}

		private static void AddStringToHash(ref uint hash, string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			unchecked
			{
				for (int i = 0; i < value.Length; i++)
				{
					hash ^= value[i];
					hash *= 16777619U;
				}
			}
		}
#endif

		private void StopMovie()
		{
			_pendingMoviePlay = false;
			_pendingMovieStartTime = 0f;
			if (_videoPlayer != null)
			{
				_videoPlayer.Stop();
			}
		}

		private float GetRenderTargetAspect()
		{
			return GetRenderTargetAspect(_jacketCamera);
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
			if (_movieModeRoot != null)
			{
				_movieModeRoot.SetActive(false);
			}
			if (_movieQuadObject != null)
			{
				_movieQuadObject.SetActive(false);
			}
			if (_movieRenderer != null)
			{
				_movieRenderer.enabled = false;
				_movieRenderer.gameObject.SetActive(false);
			}
			if (_movieCamera != null)
			{
				_movieCamera.enabled = false;
				_movieCamera.targetTexture = null;
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
			if (bootData == null || string.IsNullOrEmpty(bootData.CustomMusicScorePath))
			{
				return null;
			}

			SongPackage package = SongPackageStorage.LoadEntry(bootData.CustomMusicScorePath);
			string jacketPath = package?.JacketPath;
			if (string.IsNullOrEmpty(jacketPath) || !File.Exists(jacketPath))
			{
				return null;
			}

			ClearExternalJacketAssets();
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (ImageConversion.LoadImage(texture, File.ReadAllBytes(jacketPath)))
			{
				_externalJacketTexture = texture;
				return texture;
			}

			Destroy(texture);
			return null;
		}

		private bool IsExternalJacketTexture(Texture2D texture)
		{
			return texture != null && texture == _externalJacketTexture;
		}

		private void ApplyJacketTexture(Texture2D texture, bool destroyWhenReplaced)
		{
			if (_jackets == null)
			{
				return;
			}

			if (destroyWhenReplaced && _externalJacketSprite != null)
			{
				Destroy(_externalJacketSprite);
				_externalJacketSprite = null;
			}

			Sprite sprite = Sprite.Create(
				texture,
				new Rect(0f, 0f, texture.width, texture.height),
				new Vector2(0.5f, 0.5f),
				100f);
			if (destroyWhenReplaced)
			{
				_externalJacketSprite = sprite;
			}

			foreach (SpriteRenderer jacket in _jackets)
			{
				if (jacket != null)
				{
					jacket.sprite = sprite;
					jacket.gameObject.SetActive(true);
				}
			}
		}

		private void SetJacketsActive(bool active)
		{
			if (_jackets == null)
			{
				return;
			}

			foreach (SpriteRenderer jacket in _jackets)
			{
				if (jacket != null)
				{
					jacket.gameObject.SetActive(active);
				}
			}
		}

		private void OnDestroy()
		{
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
