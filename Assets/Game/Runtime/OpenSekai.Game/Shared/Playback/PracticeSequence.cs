using System;

namespace OpenSekai.Shared.Playback
{
    public enum PracticePhase
    {
        Playing,
        Resting,
        Completed
    }

    /// <summary>
    /// Each play step consumes the same half-open interval of chart time. Only rests use elapsed
    /// real time. The caller applies the initial phase, then reapplies it whenever Advance returns true.
    /// </summary>
    public sealed class PracticeSequence
    {
        private readonly PracticeTemplate _template;
        private readonly bool _repeat;

        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public int StepIndex { get; private set; }
        public PracticePhase Phase { get; private set; }
        public float Speed { get; private set; }
        public double RestRemainingSeconds { get; private set; }

        public PracticeSequence(PracticeTemplate template, double startSeconds, double endSeconds, bool repeat = false)
        {
            _template = template ?? throw new ArgumentNullException(nameof(template));
            if (!(startSeconds >= 0) || double.IsInfinity(startSeconds))
                throw new ArgumentOutOfRangeException(nameof(startSeconds));
            if (!(endSeconds > startSeconds) || double.IsInfinity(endSeconds))
                throw new ArgumentOutOfRangeException(nameof(endSeconds));

            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
            _repeat = repeat;
            EnterStep(0);
        }

        /// <summary>Changes this pass only. The next pass starts with the template's speed.</summary>
        public void SetSpeed(float speed)
        {
            PracticeStep.ValidateSpeed(speed);
            if (Phase == PracticePhase.Playing) Speed = speed;
        }

        /// <summary>
        /// Supply the transport's chart position when playing, and unpaused wall-clock time when
        /// resting. Stop calling while paused. No elapsed time before the BGM stops counts as rest.
        /// </summary>
        public bool Advance(double chartSeconds, double unscaledDeltaSeconds)
        {
            if (Phase == PracticePhase.Completed) return false;
            if (Phase == PracticePhase.Playing)
            {
                if (double.IsNaN(chartSeconds) || double.IsInfinity(chartSeconds))
                    throw new ArgumentOutOfRangeException(nameof(chartSeconds));
                if (chartSeconds < EndSeconds) return false;
                NextStep();
                return true;
            }

            if (!(unscaledDeltaSeconds >= 0) || double.IsInfinity(unscaledDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            bool changed = false;
            while (Phase == PracticePhase.Resting && unscaledDeltaSeconds >= RestRemainingSeconds)
            {
                unscaledDeltaSeconds -= RestRemainingSeconds;
                NextStep();
                changed = true;
            }

            // A delayed frame may span multiple rests. The next audible pass always begins at
            // the selected start, even if it took longer than expected to wake from the rest.
            if (Phase == PracticePhase.Resting) RestRemainingSeconds -= unscaledDeltaSeconds;
            return changed;
        }

        private void NextStep()
        {
            int next = StepIndex + 1;
            if (next == _template.Steps.Count && !_repeat)
            {
                Phase = PracticePhase.Completed;
                Speed = 0;
                RestRemainingSeconds = 0;
                return;
            }

            EnterStep(next % _template.Steps.Count);
        }

        private void EnterStep(int index)
        {
            StepIndex = index;
            PracticeStep step = _template.Steps[index];
            Phase = step.Kind == PracticeStepKind.Play ? PracticePhase.Playing : PracticePhase.Resting;
            Speed = step.Speed;
            RestRemainingSeconds = step.RestSeconds;
        }
    }
}
