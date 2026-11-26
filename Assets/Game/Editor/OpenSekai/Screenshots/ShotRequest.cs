using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OpenSekai.Tests.EditMode")]

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
        /// <summary>clip | live | info | finish | library | settings | settings-trace | result | actions | loading | return | home | intro | handoff | about | server | packs | profile | dan | achievements | medals | ranking | sample | library-states</summary>
        public string kind;

        /// <summary>For clip, the animation name. Otherwise the file name stem; defaults to the kind.</summary>
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
        /// result: seconds into the result page's opening, one frame each; none shows where it settles.
        /// </summary>
        public List<float> times = new List<float>();

        /// <summary>Start and exclusive end of an independent sequence in seconds. With to set, times is ignored.</summary>
        public double? from;
        public double? to;
        public bool alpha;
        public bool clean;

        internal void PrepareSequence()
        {
            if (kind == "clip")
            {
                (double start, double end) = name switch
                {
                    "tier-upgrade" => (-0.5, 1.5),
                    "dan-award" or "special-medals" => (0.0, 4.0),
                    "achievement-unlock" => (0.0, 2.6),
                    "achievement-card" => (0.0, 4.6),
                    _ => throw new ArgumentException("Unknown clip: " + name)
                };
                from ??= to.HasValue ? 0d : start;
                to ??= end;
            }
            if (!to.HasValue)
            {
                if (from.HasValue) throw new ArgumentException("from requires to.");
                return;
            }
            if (kind != "clip" && kind != "intro" && kind != "result" && kind != "info" && kind != "live" && kind != "finish")
                throw new ArgumentException("Unsupported sequence kind: " + kind);
            from ??= 0d;
            if (from.Value < 0 && !(kind == "clip" && name == "tier-upgrade"))
                throw new ArgumentException("Only tier-upgrade accepts negative time.");
            if (fps < 1 || fps > 240) throw new ArgumentException("Sequence fps must be 1..240.");
            if (FrameCount < 1) throw new ArgumentException("The requested range must contain at least one frame.");
            if (alpha && !(kind == "clip" && (name == "tier-upgrade" || name == "special-medals" || name == "achievement-card")))
                throw new ArgumentException("alpha requires a tier-upgrade, special-medals or achievement-card clip.");
            if (clean && !(kind == "clip" && name == "dan-award")) throw new ArgumentException("clean requires dan-award.");
        }

        internal int FrameCount => checked((int)Math.Round((to.Value - from.Value) * fps, MidpointRounding.AwayFromZero));
        internal double FrameTime(int index) => from.Value + (double)index / fps;
        internal string RangeStem(string stem) => FormattableString.Invariant($"{stem}_{from:0.000}-{to:0.000}");
        internal static string FrameName(string stem, int index) => FormattableString.Invariant($"{stem}_{index:D5}.png");

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

        /// <summary>result: the sample shown on the result page.</summary>
        public ResultSample result;
    }

    /// <summary>
    /// Sample data for the result page. With <see cref="settlement"/> set, the run and its ranking
    /// are the backend's matching SettlementSamples case and the counts below are ignored; without
    /// it the counts are shown with no ranking ("本局未记录成绩"). Every field has a plausible default.
    /// </summary>
    [Serializable]
    public sealed class ResultSample
    {
        /// <summary>A SettlementExample name: FirstClear | SameChartFullCombo | NewChartAllPerfectTierUp | OutsideBest30 | Auto | Failed | Retired | SaveFailed.</summary>
        public string settlement;

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

        /// <summary>
        /// A named preset: default (first clear) | fc | ap (new chart, tier up) | outside | auto |
        /// failed | retired | savefailed | loaderror.
        /// </summary>
        public static ResultSample Preset(string preset)
        {
            var sample = new ResultSample();
            switch ((preset ?? string.Empty).ToLowerInvariant())
            {
                case "":
                case "default": sample.settlement = "FirstClear"; break;
                case "fc": sample.settlement = "SameChartFullCombo"; break;
                case "ap": sample.settlement = "NewChartAllPerfectTierUp"; break;
                case "outside": sample.settlement = "OutsideBest30"; break;
                case "auto": sample.settlement = "Auto"; break;
                case "failed": sample.settlement = "Failed"; break;
                case "retired": sample.settlement = "Retired"; break;
                case "savefailed": sample.settlement = "SaveFailed"; break;
                case "loaderror": sample.kind = "LoadError"; break;
                default: throw new ArgumentException("Unknown result preset: " + preset, nameof(preset));
            }
            return sample;
        }
    }
}
