using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;

namespace OpenSekai.App.Library
{
    public enum DanOutcome { Interrupted, Failed, Passed }

    [JsonObject(ItemRequired = Required.Always)]
    public sealed class DanSongResult
    {
        public string ScoreKey { get; }
        public string Title { get; }
        public PlayResultKind Kind { get; }
        public long PositionMs { get; }
        public int StartingLife { get; }
        public LiveScore Score { get; }

        [JsonConstructor]
        internal DanSongResult(string scoreKey, string title, PlayResultKind kind, long positionMs, int startingLife, LiveScore score)
        {
            ScoreKey = scoreKey;
            Title = title;
            Kind = kind;
            PositionMs = positionMs;
            StartingLife = startingLife;
            Score = score;
        }
    }

    [JsonObject(ItemRequired = Required.Always)]
    public sealed class DanAttempt
    {
        public string Id { get; }
        public string DefinitionId { get; }
        public DateTimeOffset StartedUtc { get; }
        public DanOutcome Outcome { get; }
        public IReadOnlyList<DanSongResult> Songs { get; }

        [JsonConstructor]
        internal DanAttempt(string id, string definitionId, DateTimeOffset startedUtc, DanOutcome outcome, IList<DanSongResult> songs)
        {
            Id = id;
            DefinitionId = definitionId;
            StartedUtc = startedUtc;
            Outcome = outcome;
            Songs = Array.AsReadOnly(songs?.ToArray() ?? throw new InvalidDataException("Missing challenge songs."));
        }

        internal static DanAttempt Begin(DanDefinition definition) =>
            new(Guid.NewGuid().ToString("N"), definition.Id, DateTimeOffset.UtcNow, DanOutcome.Interrupted, Array.Empty<DanSongResult>());

        internal DanAttempt Append(DanDefinition definition, DanSongResult song)
        {
            var songs = Songs.ToList();
            songs.Add(song);
            DanOutcome outcome = song.Kind == PlayResultKind.Failed ? DanOutcome.Failed :
                song.Kind == PlayResultKind.Completed && songs.Count == definition.Songs.Count ? DanOutcome.Passed : DanOutcome.Interrupted;
            return new DanAttempt(Id, DefinitionId, StartedUtc, outcome, songs);
        }

        // A prefix of completed songs is the durable checkpoint if the process dies during the next song.
        public int SongNumber(DanDefinition definition) =>
            Outcome == DanOutcome.Interrupted && (Songs.Count == 0 || Songs[Songs.Count - 1].Kind == PlayResultKind.Completed)
                ? Math.Min(Songs.Count + 1, definition.Songs.Count) : Songs.Count;

        public int RemainingLife(DanDefinition definition) => Songs.Count == 0 ? definition.InitialLife : Songs[Songs.Count - 1].Score.life;

        internal void Validate(DanDefinition definition)
        {
            if (!Guid.TryParseExact(Id, "N", out _) || StartedUtc == default || DefinitionId != definition.Id ||
                Songs.Count > definition.Songs.Count) throw new InvalidDataException("Invalid challenge attempt.");
            int life = definition.InitialLife;
            var expected = DanOutcome.Interrupted;
            for (int i = 0; i < Songs.Count; i++)
            {
                var song = Songs[i];
                if (song == null || string.IsNullOrWhiteSpace(song.ScoreKey) || string.IsNullOrWhiteSpace(song.Title) ||
                    song.StartingLife != life || song.Score.life < 0 || song.Score.life > life ||
                    song.PositionMs < 0 || song.Score.perfectCount < 0 || song.Score.greatCount < 0 ||
                    song.Score.goodCount < 0 || song.Score.badCount < 0 || song.Score.missCount < 0 ||
                    song.Kind != PlayResultKind.Completed && i + 1 != Songs.Count)
                    throw new InvalidDataException("Invalid challenge song.");
                life = song.Score.life;
                expected = song.Kind switch
                {
                    PlayResultKind.Failed when life == 0 => DanOutcome.Failed,
                    PlayResultKind.Retired => DanOutcome.Interrupted,
                    PlayResultKind.Completed when life > 0 => i + 1 == definition.Songs.Count ? DanOutcome.Passed : DanOutcome.Interrupted,
                    _ => throw new InvalidDataException("Invalid challenge end reason.")
                };
            }
            if (Outcome != expected) throw new InvalidDataException("Invalid challenge outcome.");
        }
    }
}
