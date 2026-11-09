using Newtonsoft.Json;
using Sekai.Live;

namespace OpenSekai.Charts
{
    /// <summary>Stored event position only; live event progress belongs to each session.</summary>
    public class ChartPlaybackEvent
    {
        public MusicScoreInfo MusicScoreInfo { get; set; }
        [JsonIgnore] public virtual bool IsSkillOrFever => false;
        public ChartPlaybackEvent Copy() => new ChartPlaybackEvent { MusicScoreInfo = MusicScoreInfo };
    }

    // Legacy editor JSON can contain these types; match its policy of excluding them from custom play.
    internal sealed class NonPlaybackChartEvent : ChartPlaybackEvent
    {
        public override bool IsSkillOrFever => true;
    }
}
