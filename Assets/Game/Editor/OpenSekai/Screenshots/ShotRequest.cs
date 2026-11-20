using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    /// <summary>
    /// One screenshot run: an output folder, a resolution and the shots to take, in order. It is read
    /// from a JSON file so the same request works from the menu, from <c>-executeMethod</c> and from
    /// an editor that is already open (Tools/capture-screens.ps1 writes it).
    /// </summary>
    [Serializable]
    public sealed class ShotRequest
    {
        /// <summary>Folder the PNGs and report.json go to, relative to the project or absolute.</summary>
        public string outDir;

        public int width = 2800;
        public int height = 1272;

        /// <summary>Quit the editor when done (used when the script launched the editor itself).</summary>
        public bool quitWhenDone;

        public List<Shot> shots = new List<Shot>();

        public static ShotRequest Load(string path)
        {
            ShotRequest request = JsonConvert.DeserializeObject<ShotRequest>(File.ReadAllText(path));
            if (request == null)
            {
                throw new InvalidDataException("Empty screenshot request: " + path);
            }
            request.Normalize();
            return request;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(outDir))
            {
                outDir = "TestResults/screenshots/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            }
            if (!Path.IsPathRooted(outDir))
            {
                outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outDir));
            }
            width = Mathf.Clamp(width, 64, 8192);
            height = Mathf.Clamp(height, 64, 8192);
            shots = shots ?? new List<Shot>();
        }
    }

    [Serializable]
    public sealed class Shot
    {
        /// <summary>live | info | finish | library | settings | settings-trace | result | actions | loading | return | home | intro | handoff | about</summary>
        public string kind;

        /// <summary>File name stem; defaults to the kind (plus the time for live/info).</summary>
        public string name;

        /// <summary>
        /// live/info: the package to play. A package id, its folder name (or part of it), the song
        /// title, or a folder path. Installed packages are searched first, then Tools/fixtures.
        /// </summary>
        public string package;

        /// <summary>live/info: easy | normal | hard | expert | master | append. Empty keeps the package's.</summary>
        public string difficulty;

        /// <summary>
        /// live: seconds on the music clock (the audio file's own time, filler included).
        /// info: seconds since the info page was asked to start (it fades out at about 2.9 s).
        /// finish: seconds since the last note was done (the result animation starts 1 s later).
        /// intro: seconds into the splash (it hands over to the settled home page at about 4.4 s).
        /// </summary>
        public List<float> times = new List<float>();

        /// <summary>live: seconds before each time that are played at the capture frame rate (judgement effects need them).</summary>
        public float lead = 1.5f;

        /// <summary>
        /// live: show autoplay hits as PERFECT with a combo (the game's "auto fake perfect" option), so
        /// the judgement text and combo art are in the frame. false shows plain autoplay (AUTO, no combo).
        /// Only the in-memory settings change; the settings file is not written.
        /// </summary>
        public bool fakePerfect = true;

        /// <summary>finish: the end-of-live animation autoplay shows: allperfect | fullcombo | clear | finish | none.</summary>
        public string finishAnimation = "allperfect";

        /// <summary>live/info/finish: the simulated frame rate the frames are stepped at.</summary>
        public int fps = 60;

        /// <summary>result: the sample shown on the result panel.</summary>
        public ResultSample result;
    }

    /// <summary>Sample data for the library result panel. Every field has a plausible default.</summary>
    [Serializable]
    public sealed class ResultSample
    {
        /// <summary>Completed | Failed | Retired | LoadError</summary>
        public string kind = "Completed";
        public bool auto;
        public string title = "DEINOS PHAINEIN";
        public int playLevel = 35;
        public int score = 1084217;
        /// <summary>S | A | B | C | D</summary>
        public string rank = "S";
        public int maxCombo = 812;
        public int totalCombo = 1250;
        public int perfect = 1183;
        public int great = 51;
        public int good = 9;
        public int bad = 4;
        public int miss = 3;
        public int life = 820;
        public string message = "找不到音频文件。";

        /// <summary>Fills the counts for a named preset: fc, ap, auto, failed, retired, loaderror.</summary>
        public static ResultSample Preset(string preset)
        {
            var sample = new ResultSample();
            switch ((preset ?? string.Empty).ToLowerInvariant())
            {
                case "ap":
                    sample.score = 1135420; sample.maxCombo = 1250; sample.perfect = 1250;
                    sample.great = sample.good = sample.bad = sample.miss = 0;
                    break;
                case "fc":
                    sample.score = 1112806; sample.maxCombo = 1250; sample.perfect = 1206;
                    sample.great = 44; sample.good = sample.bad = sample.miss = 0;
                    break;
                case "auto":
                    sample.auto = true; sample.score = 1135420; sample.maxCombo = 1250;
                    sample.perfect = sample.great = sample.good = sample.bad = sample.miss = 0;
                    break;
                case "failed":
                    sample.kind = "Failed"; sample.score = 412803; sample.rank = "C"; sample.maxCombo = 96;
                    sample.totalCombo = 1250; sample.perfect = 402; sample.great = 87; sample.good = 31;
                    sample.bad = 22; sample.miss = 58; sample.life = 0;
                    break;
                case "retired":
                    sample.kind = "Retired"; sample.score = 233118; sample.rank = "D"; sample.maxCombo = 207;
                    sample.perfect = 220; sample.great = 12; sample.good = 1; sample.bad = 0; sample.miss = 1;
                    break;
                case "loaderror":
                    sample.kind = "LoadError";
                    break;
            }
            return sample;
        }
    }
}
