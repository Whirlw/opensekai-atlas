using System;
using UnityEngine;

namespace OpenSekai.Shared.Playback
{
    /// <summary>A reusable sequence applied to one interval, in chart seconds (before the BGM offset).</summary>
    public sealed class PracticePlan
    {
        public PracticePlan(double startSeconds, double? endSeconds, PracticeTemplate template,
            Action<Transform, IPracticeControls> createControls, bool speedFollows = true, bool judgmentFollows = true, bool recordResult = false)
        {
            if (double.IsInfinity(startSeconds) || double.IsNaN(startSeconds))
                throw new ArgumentOutOfRangeException(nameof(startSeconds));
            if (endSeconds.HasValue && (double.IsInfinity(endSeconds.Value) || !(endSeconds > startSeconds)))
                throw new ArgumentOutOfRangeException(nameof(endSeconds));
            RecordResult = recordResult;
            SpeedFollows = speedFollows;
            JudgmentFollows = judgmentFollows;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            CreateControls = createControls ?? throw new ArgumentNullException(nameof(createControls));
        }

        public bool RecordResult { get; }
        public bool SpeedFollows { get; }
        public bool JudgmentFollows { get; }
        public double StartSeconds { get; }
        public double? EndSeconds { get; }
        public PracticeTemplate Template { get; }
        public Action<Transform, IPracticeControls> CreateControls { get; }
    }

    public interface IPracticeControls
    {
        double PositionSeconds { get; }
        double MinimumSeconds { get; }
        double MaximumSeconds { get; }
        double Rate { get; }
        bool Paused { get; }
        bool Resting { get; }
        bool Completed { get; }
        void SetRate(double speed);
        void Seek(double chartSeconds);
        void TogglePause();
        void Restart();
        void Quit();
    }
}
