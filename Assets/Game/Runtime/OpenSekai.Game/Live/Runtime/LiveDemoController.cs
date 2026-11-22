using System.Collections;
using OpenSekai.Shared.Live.Simulation;
using OpenSekai.Shared.Settings;
using Sekai.Live;
using UnityEngine;

namespace Sekai.Core.Live
{
	/// <summary>
	/// The resource pack page's demo: the live's own views and logic playing a short chart on
	/// autoplay, drawn into a texture instead of the screen. After a round the five result effects
	/// play one after another, then the round starts again.
	///
	/// It shows what a live shows (the judgment text and combo as a PERFECT hand would get them, no
	/// AUTO label) but leaves the rest of a live out: no music, no input, no pause, no history, and
	/// none of the screen-wide changes (resolution, frame rate, sleep, garbage collector) a live makes.
	/// The views lay themselves out for the screen, so the target has the screen's aspect. The root
	/// sits far from the page cameras, whose culling masks take every layer.
	/// </summary>
	public sealed class LiveDemoController : BaseLiveController
	{
		/// <summary>The order the result effects play in after each round.</summary>
		private static readonly LiveResultAnimationType[] _results =
		{
			LiveResultAnimationType.AllPerfect,
			LiveResultAnimationType.FullCombo,
			LiveResultAnimationType.Clear,
			LiveResultAnimationType.LifeZero,
			LiveResultAnimationType.Failure,
		};

		/// <summary>The round starts this long before the first note, with notes already on the lanes.</summary>
		private const float LEAD_IN_SECONDS = 0.5f;

		/// <summary>The pause between the last note and the first result effect.</summary>
		private const float AFTER_NOTES_SECONDS = 0.9f;

		/// <summary>How long each result effect stays before the next.</summary>
		private const float RESULT_SECONDS = 3.2f;

		/// <summary>The current demo, if any: its sounds go through <see cref="SoundManager"/> while it runs.</summary>
		internal static LiveDemoController Current { get; private set; }

		private LiveViewBase[] _views;
		private LiveLogic _logic;
		private RenderTexture _target;
		private Coroutine _results;
		private bool _roundOver;
		private bool _audible;
		private double _clockRemainderMs;

		/// <summary>
		/// Builds the views under a new object at <paramref name="position"/> and starts the first
		/// round. The demo owns <paramref name="boot"/> and its skins from here on, and releases them
		/// when destroyed; <paramref name="target"/> stays the caller's.
		/// </summary>
		public static LiveDemoController Create(Transform parent, Vector3 position, FreeLiveBootData boot, RenderTexture target)
		{
			var root = new GameObject("LiveDemo");
			root.transform.SetParent(parent, false);
			root.transform.position = position;
			LiveDemoController demo = root.AddComponent<LiveDemoController>();
			try
			{
				demo.Begin(boot, target);
			}
			catch
			{
				// Destroying it runs OnExit, which releases what it took over.
				Destroy(root);
				throw;
			}

			return demo;
		}

		/// <summary>Whether the demo's hit sounds and result voices are heard. Off at first.</summary>
		public bool Audible
		{
			get => _audible;
			set
			{
				if (_audible == value) return;
				_audible = value;
				if (!value) Silence();
			}
		}

		/// <summary>The pack sounds of this demo, played instead of the built-in cue when bound.</summary>
		internal LiveMaterialAudio MaterialAudio => (BootData as FreeLiveBootData)?.MaterialAudio;

		protected override void OnAwake()
		{
			// Begin does the work: the boot data arrives right after AddComponent.
		}

		private void Begin(FreeLiveBootData boot, RenderTexture target)
		{
			BootData = boot;
			Settings = boot.LiveSettingData;
			_target = target;
			Current = this;
			liveBundleBuildData = ResourceCatalog.LiveBundleConfig.Load();
			LiveConfig.LoadMasterConfig();
			LiveConfig.LoadOptionData();
			LiveConfig.CacheSpeedTime = 0f;
			ApplyBuiltinSets();
			timingCompensation = new TimingCompensation(0f, 0f, 0f);
			LiveConfig.ViewLeadSeconds = 0f;
			LoadSounds();

			EnsureBackgroundTextureSizeForCurrentScreen();
			_views = LiveViewFactory.Create(boot, transform);
			LiveViewExt.Setup(_views, this);
			LiveViewExt.OnLoad(_views);
			RouteCameras();
			StartRound();
		}

		private void LoadSounds()
		{
			SoundManager.Instance.LoadSoundBundle("live/sound/live_se/default", true);
			int set = Mathf.Clamp(Settings?.NoteSeIndex ?? 0, 0, 1);
			LiveConfig.SetNoteSeName(set);
			SoundManager.Instance.LoadSoundBundle("live/tap_se/" + LiveConfig.NoteSeName, true);
		}

		/// <summary>
		/// The front camera draws the whole live (the effect camera is only stacked on it, and the
		/// background cameras render once into their own texture). Point it at the target, and keep
		/// the prefab's audio listener off: the page already has one.
		/// </summary>
		private void RouteCameras()
		{
			if (BaseCamera != null)
			{
				BaseCamera.targetTexture = _target;
				BaseCamera.forceIntoRenderTexture = true;
			}

			foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
			{
				listener.enabled = false;
			}

			SoundManager.NotifySceneAudioListenersChanged();
		}

		private void StartRound()
		{
			if (_results != null)
			{
				StopCoroutine(_results);
				_results = null;
			}

			if (_logic != null)
			{
				_logic.ConnectorAudio.Reset();
				LiveViewExt.Result(_views, (int)LiveResultAnimationType.None);
				LiveViewExt.Retry(_views);
			}

			_logic = new LiveLogic(liveBundleBuildData);
			_logic.Setup(BootData, _views);
			_logic.SetSkillLogic(new SkillLogic());
			_logic.SetScoreLogic(new ScoreLogic(liveBundleBuildData));
			_roundOver = false;
			_clockRemainderMs = 0d;
			currentMusicTimeMs = (long)(-LEAD_IN_SECONDS * 1000f);
			float viewTime = CurrentViewTimeSeconds;
			LiveViewExt.MusicStart(_views, viewTime);
			LiveViewExt.MusicPlaybackStart(_views, viewTime);
			LiveViewExt.RhythmGameStart(_views);
			_logic.RefreshInput();
			state = LiveControllerState.Playing;
		}

		protected override void OnUpdate()
		{
			currentGameTime = Time.realtimeSinceStartupAsDouble;
			if (state != LiveControllerState.Playing || _logic == null)
			{
				return;
			}

			// The clock runs on its own, as a live's does before its music starts.
			double stepMs = Mathf.Min(Time.unscaledDeltaTime, 0.1f) * 1000d + _clockRemainderMs;
			long wholeMs = (long)stepMs;
			currentMusicTimeMs += wholeMs;
			_clockRemainderMs = stepMs - wholeMs;

			_logic.OnUpdate(CurrentLogicChartTime, currentGameTime);
			_logic.OnAutoInput();
			if (!_roundOver && _logic.IsNotesAllFinished)
			{
				_roundOver = true;
				_results = StartCoroutine(PlayResults());
			}

			_logic.OnView();
			LiveViewExt.OnUpdate(_views, CurrentViewTimeSeconds);
		}

		private IEnumerator PlayResults()
		{
			_logic.ConnectorAudio.Silence();
			yield return new WaitForSecondsRealtime(AFTER_NOTES_SECONDS);
			foreach (LiveResultAnimationType result in _results)
			{
				LiveViewExt.Result(_views, (int)result);
				yield return new WaitForSecondsRealtime(RESULT_SECONDS);
			}

			_results = null;
			StartRound();
		}

		/// <summary>Stops what is sounding now: the hold loops and the pack clips.</summary>
		private void Silence()
		{
			_logic?.ConnectorAudio.Silence();
			LiveViewExt.Pause(_views);
			LiveViewExt.Resume(_views, CurrentViewTimeSeconds);
			MaterialAudio?.Stop();
		}

		// The page has no pause, retry or back key for the demo.
		protected override void OnPause()
		{
		}

		protected override void OnBackKey()
		{
		}

		protected override void OnExit()
		{
			if (_results != null)
			{
				StopCoroutine(_results);
				_results = null;
			}

			state = LiveControllerState.None;
			_logic?.ConnectorAudio.Reset();
			MaterialAudio?.Stop();
			if (_views != null)
			{
				LiveViewExt.OnUnload(_views);
			}

			if (BaseCamera != null)
			{
				BaseCamera.targetTexture = null;
			}

			if (Current == this)
			{
				Current = null;
			}

			LiveConfig.ViewLeadSeconds = 0f;
			(BootData as FreeLiveBootData)?.ReleaseMaterialAudio();
			if (BackgroundTexture != null)
			{
				BackgroundTexture.Release();
				Destroy(BackgroundTexture);
				BackgroundTexture = null;
			}
		}
	}
}
