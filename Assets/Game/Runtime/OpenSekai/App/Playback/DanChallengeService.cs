using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using OpenSekai.Infrastructure.Sonolus;
using Cysharp.Threading.Tasks;
using OpenSekai.App.Library;
using OpenSekai.Shared.Playback;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.Playback
{
    public enum DanPhase { Idle, Preparing, StartingSong, Playing, BetweenSongs, Finished, Error }

    /// <summary>Main-thread course owner. It survives Core scene swaps. Changed is raised on the main thread.</summary>
    public sealed class DanChallengeService
    {
        /// <summary>Default rest between two songs. <see cref="ContinueNow"/> ends it sooner.</summary>
        public const float REST_SECONDS = DanDefinition.DEFAULT_REST_SECONDS;

        public static DanChallengeService Local { get; private set; } = new();
        public event Action Changed;
        public DanPhase Phase { get; private set; }
        public DanDefinition Definition { get; private set; }
        public DanAttempt Attempt { get; private set; }
        public int SongNumber { get; private set; }
        public int RemainingLife { get; private set; }
        public bool NewlyAwarded { get; private set; }
        public IReadOnlyList<string> ReachedAchievementIds { get; private set; } = Array.Empty<string>();
        public string Error { get; private set; }

        /// <summary>Seconds left of the rest while <see cref="DanPhase.BetweenSongs"/>; counts unscaled time, paused in the background.</summary>
        public float RestRemaining { get => (float)_restRemaining; private set => _restRemaining = value; }

        /// <summary>The pinned package folder of song <paramref name="number"/> (from 1) while a course runs; null otherwise.</summary>
        public string SongDirectory(int number) =>
            _songs != null && number >= 1 && number <= _songs.Length ? _songs[number - 1]?.Directory : null;

        /// <summary>Ends the rest between songs; the next song starts at once.</summary>
        public void ContinueNow() => _skipRest = true;

        private bool _skipRest;
        private bool _applicationPaused;
        private double _restTick;
        private double _restRemaining;

        internal void SetApplicationPaused(bool paused, double now)
        {
            if (paused == _applicationPaused) return;
            if (Phase == DanPhase.BetweenSongs) UpdateRest(now);
            _applicationPaused = paused;
            _restTick = now;
        }

        internal void UpdateRest(double now)
        {
            if (!_applicationPaused)
                _restRemaining = RestAfterFrame(_restRemaining, now - _restTick);
            _restTick = now;
        }

        private readonly PlayerProfileStore _profile;
        private readonly SonolusLevelInstaller _installer;
        private readonly string _cacheRoot;
        private PlayerProfileStore Profile => _profile ?? PlayerProfileStore.Local;

        internal DanChallengeService(PlayerProfileStore profile = null, SonolusLevelInstaller installer = null, string cacheRoot = null)
            => (_profile, _installer, _cacheRoot) = (profile, installer, cacheRoot);

        private DanSongPreparation _preparation;
        private PreparedDanSong[] _songs;
        private CancellationTokenSource _operation;
        private DanLiveSong _live;
        private bool _finishedSong;
        private bool _quit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Local = new DanChallengeService();

        public async UniTask<PlayStartResult> StartAsync(DanDefinition definition, CancellationToken token = default)
        {
            if (_operation != null || PlayService.Instance.IsStarting)
                return new PlayStartResult(PlayStartStatus.Busy, "已有游玩正在准备或进行。");
            if (SceneManager.Instance.CurrentScene != SceneManager.Scene.Library)
                return new PlayStartResult(PlayStartStatus.Failed, "请从段位页开始挑战。");
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Attempt = null;
            SongNumber = 1;
            Error = null;
            ReachedAchievementIds = Array.Empty<string>();
            NewlyAwarded = false;
            _quit = false;
            RemainingLife = definition.InitialLife;
            _operation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Profile.ActiveDanId = definition.Id;
            var preparationToken = _operation.Token;
            try
            {
                _songs = new PreparedDanSong[definition.Songs.Count];
                _preparation = new DanSongPreparation(_installer ?? SonolusLevelInstaller.Shared,
                    _cacheRoot ?? Path.Combine(Application.temporaryCachePath, "DanCourses"));
                for (int i = 0; i < _songs.Length; i++)
                {
                    SongNumber = i + 1;
                    SetPhase(DanPhase.Preparing);
                    _songs[i] = await _preparation.PrepareAsync(definition.Songs[i], preparationToken);
                }
                preparationToken.ThrowIfCancellationRequested();
                BeginAttempt();
                var result = await StartSong(SceneManager.Scene.Library, preparationToken);
                if (!result.Started) EndOperation(result.Message, result.Status == PlayStartStatus.Cancelled);
                else
                {
                    // Page destruction after handing off to Core must not cancel later songs.
                    _operation.Dispose();
                    _operation = new CancellationTokenSource();
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                EndOperation(null, true);
                return new PlayStartResult(PlayStartStatus.Cancelled, "已取消段位准备。");
            }
            catch (Exception e)
            {
                EndOperation(e.Message);
                return new PlayStartResult(PlayStartStatus.Failed, Error);
            }
        }

        private void BeginAttempt()
        {
            ReachedAchievementIds = Array.Empty<string>();
            Attempt = DanAttempt.Begin(Definition);
            Profile.SaveDanAttempt(Definition, Attempt);
            SongNumber = 1;
            RemainingLife = Definition.InitialLife;
            NewlyAwarded = false;
        }

        private async UniTask<PlayStartResult> StartSong(SceneManager.Scene source, CancellationToken token)
        {
            var song = _songs[SongNumber - 1];
            int startLife = RemainingLife;
            _finishedSong = false;
            var live = new DanLiveSong(Definition.InitialLife, startLife, Definition.Damage,
                life => { RemainingLife = life; Notify(); },
                (kind, score, time) =>
                {
                    if (_finishedSong) return;
                    _finishedSong = true;
                    RemainingLife = score.life;
                    var row = new DanSongResult(song.ScoreKey, song.Title, kind, Math.Max(0L, time), startLife, score,
                        DateTimeOffset.UtcNow, song.Ranked, song.PlayLevel);
                    var updated = Attempt.Append(Definition, row);
                    try
                    {
                        NewlyAwarded = Profile.SaveDanAttempt(Definition, updated, out var reached);
                        if (reached.Count > 0)
                            ReachedAchievementIds = Array.AsReadOnly(ReachedAchievementIds.Union(reached).ToArray());
                        Attempt = updated;
                    }
                    catch (Exception e) { Error = e.Message; }
                },
                restart => LeaveSongAsync(restart).Forget(), Definition.AllowPause, Definition.FailOnZero, Definition.SongRates[SongNumber - 1], SongNumber == _songs.Length);
            _live = live;
            SetPhase(DanPhase.StartingSong);
            var result = await PlayService.Instance.StartAsync(new PlayRequest(song.Directory, live, source, song.Ranked), token);
            if (result.Started) SetPhase(DanPhase.Playing);
            return result;
        }

        public void Quit()
        {
            if (_operation == null) return;
            _quit = true;
            _live?.RequestRetire();
            _operation.Cancel();
        }

        private async UniTask LeaveSongAsync(bool restart)
        {
            _live = null;
            try
            {
                if (!_quit && Error == null && _finishedSong)
                {
                    bool next = Attempt.Songs.Count < _songs.Length && Attempt.Songs[Attempt.Songs.Count - 1].Kind == PlayResultKind.Completed;
                    if (restart || next)
                    {
                        var token = _operation.Token;
                        if (restart)
                        {
                            BeginAttempt();
                        }
                        else
                        {
                            SongNumber++;
                            if (Definition.RestEnabled && Definition.RestSeconds > 0) await RestBetweenSongs(token);
                        }
                        var result = await StartSong(SceneManager.Scene.Core, token);
                        if (result.Started) return;
                        if (result.Status != PlayStartStatus.Cancelled) Error = result.Message;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Error = e.Message; }
            EndOperation(Error);
            SceneManager.Instance.RequestScene(SceneManager.Scene.Library);
        }

        internal static double RestAfterFrame(double remaining, double elapsed) =>
            Math.Max(0f, remaining - Math.Max(0f, elapsed));

        private async UniTask RestBetweenSongs(CancellationToken token)
        {
            // Reset before the phase change: a page may skip the rest from its Changed handler.
            _skipRest = false;
            RestRemaining = Definition.RestSeconds;
            _restTick = Time.realtimeSinceStartupAsDouble;
            SetPhase(DanPhase.BetweenSongs);
            while (RestRemaining > 0f && !_skipRest)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                UpdateRest(Time.realtimeSinceStartupAsDouble);
            }
            RestRemaining = 0f;
        }

        private void EndOperation(string error, bool cancelled = false)
        {
            Profile.ActiveDanId = null;
            RestRemaining = 0f;
            Error = cancelled ? null : error;
            _preparation?.Dispose();
            _preparation = null;
            _songs = null;
            _live = null;
            _operation?.Dispose();
            _operation = null;
            SetPhase(Error == null ? DanPhase.Finished : DanPhase.Error);
        }

        private void SetPhase(DanPhase phase)
        {
            Phase = phase;
            Notify();
        }

        private void Notify()
        {
            try { Changed?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
