using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.Shared.Playback
{
    public sealed class PracticeClip
    {
        public string Id;
        public string ChartKey;
        public string Song;
        public double From;
        public double? To;
        public PracticeTemplate Template;
    }

    public sealed class PracticeClipStore
    {
        private static readonly object _fileLock = new object();
        private readonly string _path;
        public PracticeClipStore(string path) => _path = path;
        public static PracticeClipStore Shared => new PracticeClipStore(Path.Combine(Application.persistentDataPath, "practice-clips.json"));

        public List<PracticeClip> Load() { lock (_fileLock) return ReadClips(); }

        private List<PracticeClip> ReadClips()
        {
            if (!File.Exists(_path)) return new List<PracticeClip>();
            var clips = JsonConvert.DeserializeObject<List<PracticeClip>>(File.ReadAllText(_path));
            if (clips == null || clips.Exists(c => c?.Template == null || string.IsNullOrEmpty(c.Id) || string.IsNullOrEmpty(c.ChartKey)))
                throw new InvalidDataException("无法读取已保存的剪辑。");
            return clips;
        }

        public void Save(PracticeClip clip)
        {
            lock (_fileLock)
            {
                var clips = ReadClips();
                int index = clips.FindIndex(c => c.Id == clip.Id);
                if (index < 0) clips.Add(clip);
                else clips[index] = clip;
                AtomicFile.WriteAllText(_path, JsonConvert.SerializeObject(clips, Formatting.Indented));
            }
        }
    }
}
