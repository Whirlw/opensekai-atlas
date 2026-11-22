using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.App.Library
{
    public sealed class HistoryPage
    {
        public int TotalCount { get; }
        public IReadOnlyList<CustomMusicScorePlayHistoryRecord> Records { get; }

        internal HistoryPage(int count, IReadOnlyList<CustomMusicScorePlayHistoryRecord> records)
        {
            TotalCount = count;
            Records = records;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ProfileAppearance
    {
        [JsonProperty("nickname", Required = Required.Always)]
        public string Nickname { get; }
        [JsonProperty("avatarId", Required = Required.Always)]
        public string AvatarId { get; }

        [JsonConstructor]
        public ProfileAppearance(string nickname, string avatarId)
        {
            Nickname = Validate(nickname, nameof(nickname));
            AvatarId = Validate(avatarId, nameof(avatarId));
        }

        private static string Validate(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || Array.Exists(value.ToCharArray(), char.IsControl))
                throw new ArgumentException("Invalid profile field.", field);
            return value.Trim();
        }
    }

    public sealed class PlayerProfileSnapshot
    {
        public ProfileAppearance Appearance { get; }
        public Best30Snapshot Best30 { get; }
        public IReadOnlyList<string> EarnedDanIds { get; }
        public string EquippedDanId { get; }
        public OpenSekai.App.Playback.DanDefinition EquippedDan { get; }

        internal PlayerProfileSnapshot(ProfileAppearance appearance, Best30Snapshot best30, DanArchive archive)
        {
            Appearance = appearance;
            Best30 = best30;
            EarnedDanIds = new List<string>(archive.Earned()).AsReadOnly();
            EquippedDanId = archive.Equipped;
            if (EquippedDanId != null) EquippedDan = archive.DecodeDefinitions()[EquippedDanId];
        }
    }

    /// <summary>Local player data. Call from the Unity main thread, including confirm and cancel.</summary>
    public sealed partial class PlayerProfileStore
    {
        private const string PROFILE_FILE = "profile.json";
        private static PlayerProfileStore _local;
        public static PlayerProfileStore Local => _local ??= new PlayerProfileStore(SongPackageStorage.RootDirectory);

        private readonly string _root;
        private string _directory;
        internal RankingHistory History { get; private set; }
        internal PlayHistorySummaryCache Summaries { get; private set; }
        private string HistoryPath => Path.Combine(_directory, SongPackageStorage.PLAY_HISTORY_FILE_NAME);
        private string ProfilePath => Path.Combine(_directory, PROFILE_FILE);
        private string Archives => Path.Combine(_root, "PlayerArchives");
        private static readonly ProfileAppearance _default = new("Player", "default");

        internal PlayerProfileStore(string root)
        {
            _root = root;
            _directory = SongPackageStorage.ActivePlayerDirectory(root);
            Summaries = new PlayHistorySummaryCache();
            History = new RankingHistory(HistoryPath, Summaries);
        }

        public PlayerProfileSnapshot Read() => new(ReadAppearance(), History.Read(), ReadDanArchive());
        public HistoryPage ReadHistory(int offset, int limit) => History.ReadPage(offset, limit);

        public void SetAppearance(string nickname, string avatarId)
        {
            RequireIdle();
            var appearance = new ProfileAppearance(nickname, avatarId);
            AtomicFile.WriteAllText(ProfilePath, JsonConvert.SerializeObject(appearance));
            CancelPendingPreview();
        }

        private ProfileAppearance ReadAppearance() => File.Exists(ProfilePath)
            ? DecodeAppearance(File.ReadAllText(ProfilePath)) : _default;

        private static ProfileAppearance DecodeAppearance(string json) =>
            JsonConvert.DeserializeObject<ProfileAppearance>(json) ?? throw new InvalidDataException("Missing profile.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _local = null;
    }
}
