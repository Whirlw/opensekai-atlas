using System;
using System.Collections.Generic;
using Sekai.Live;
using Newtonsoft.Json;
using System.Linq;
namespace OpenSekai.Charts
{
    /// <summary>Legacy chart wire data, without selection, clipboard, preview or validation state.</summary>
    public sealed class ChartDocument
    {
        public const int CURRENT_VERSION = 1;
        public int VersionCode { get; set; } = CURRENT_VERSION;
        public List<ChartEvent> MusicScoreEventDataList { get; set; } = new List<ChartEvent>();
        public List<ChartNote> NoteList { get; set; } = new List<ChartNote>();
        public ChartPlaybackEvent[] EventArray { get; set; } = Array.Empty<ChartPlaybackEvent>();
        public long MusicScoreTicksMax { get; set; }
        public int MusicId { get; set; }
        public string FullComboDataHash { get; set; }
        [JsonIgnore] public bool HasPlayableNotes => NoteList != null && NoteList.Exists(note => note != null);

        public ChartDocument Copy() => new ChartDocument
        {
            VersionCode = VersionCode, MusicScoreTicksMax = MusicScoreTicksMax,
            MusicId = MusicId, FullComboDataHash = FullComboDataHash,
            NoteList = NoteList?.ConvertAll(note => note?.Copy()) ?? new List<ChartNote>(),
            MusicScoreEventDataList = MusicScoreEventDataList?.ConvertAll(e => e?.Copy()) ?? new List<ChartEvent>(),
            EventArray = (EventArray ?? Array.Empty<ChartPlaybackEvent>()).Where(e => e != null).Select(e => e.Copy()).ToArray()
        };

        public void Migrate()
        {
            if (VersionCode != 0 && VersionCode < CURRENT_VERSION)
            {
                NoteList?.Sort((a,b) => a.ticks != b.ticks ? a.ticks.CompareTo(b.ticks) : a.id.CompareTo(b.id));
                MusicScoreEventDataList?.Sort((a,b) => a.ticks.CompareTo(b.ticks));
            }
            VersionCode = CURRENT_VERSION;
            EventArray = (EventArray ?? Array.Empty<ChartPlaybackEvent>()).Where(e => e != null && !e.IsSkillOrFever).ToArray();
        }
    }
}
