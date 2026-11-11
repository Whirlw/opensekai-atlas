using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sekai.Live;
using OpenSekai.Charts;

namespace OpenSekai.Charts.Serialization
{
    /// <summary>Reads the existing JSON wire format, including legacy $type/$id/$ref metadata.</summary>
    public static class ChartJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            SerializationBinder = new LegacyChartTypes()
        };

        public static ChartDocument Read(string json, int? musicId = null)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var document = JsonConvert.DeserializeObject<ChartDocument>(json, Settings);
            if (document == null) return null;
            document.Migrate();
            if (musicId.HasValue) document.MusicId = musicId.Value;
            return document;
        }

        public static string Write(ChartDocument document) => JsonConvert.SerializeObject(document, Settings);

        private sealed class LegacyChartTypes : ISerializationBinder
        {
            private const string OLD_MODELS = "Sekai.MusicScoreMaker.Ingame.Models.";
            private static readonly Dictionary<string, Type> Allowed = new Dictionary<string, Type>
            {
                { OLD_MODELS + "MusicScoreMakerData", typeof(ChartDocument) },
                { OLD_MODELS + "MusicScoreNoteBase", typeof(ChartNote) },
                { OLD_MODELS + "MusicScoreEventData", typeof(ChartEvent) },
                { typeof(ChartDocument).FullName, typeof(ChartDocument) },
                { typeof(ChartNote).FullName, typeof(ChartNote) },
                { typeof(ChartEvent).FullName, typeof(ChartEvent) },
                { "Sekai.Live.EventBase", typeof(ChartPlaybackEvent) },
                { "Sekai.Live.SkillEvent", typeof(NonPlaybackChartEvent) },
                { "Sekai.Live.FeverStartEvent", typeof(NonPlaybackChartEvent) },
                { "Sekai.Live.FeverBeginEvent", typeof(NonPlaybackChartEvent) },
                { "Sekai.Live.FeverEndEvent", typeof(NonPlaybackChartEvent) },
                { "Sekai.Live.EventBase[]", typeof(ChartPlaybackEvent[]) },
                { typeof(ChartPlaybackEvent).FullName, typeof(ChartPlaybackEvent) },
                { typeof(MusicScoreInfo).FullName, typeof(MusicScoreInfo) }
            };

            public Type BindToType(string assemblyName, string typeName)
            {
                if (Allowed.TryGetValue(typeName, out var type)) return type;
                if (typeName.StartsWith("System.Collections.Generic.List`1[[", StringComparison.Ordinal))
                {
                    if (typeName.Contains(OLD_MODELS + "MusicScoreNoteBase,")) return typeof(List<ChartNote>);
                    if (typeName.Contains(OLD_MODELS + "MusicScoreEventData,")) return typeof(List<ChartEvent>);
                }
                throw new JsonSerializationException("Unsupported chart type: " + typeName);
            }

            public void BindToName(Type type, out string assemblyName, out string typeName)
            {
                // Keep the assembly names used in existing chart metadata across the Core move.
                if (type == typeof(MusicScoreInfo))
                    assemblyName = "OpenSekai.Game";
                else if (type.Namespace == typeof(ChartDocument).Namespace)
                    assemblyName = "OpenSekai.App";
                else
                    assemblyName = type.Assembly.GetName().Name;
                typeName = type == typeof(ChartDocument) ? OLD_MODELS + "MusicScoreMakerData"
                    : type == typeof(ChartNote) ? OLD_MODELS + "MusicScoreNoteBase"
                    : type == typeof(ChartEvent) ? OLD_MODELS + "MusicScoreEventData"
                    : type == typeof(ChartPlaybackEvent) ? "Sekai.Live.EventBase" : type.FullName;
            }
        }
    }
}
