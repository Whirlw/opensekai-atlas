using System;
using OpenSekai.Shared.Playback;

namespace Sekai.Core.Live
{
    /// <summary>The live audio clock drives playing steps. Only rests advance on wall time.</summary>
    internal sealed class PracticePlayback : IPracticeControls
    {
        private readonly PracticePlan _plan;
        private readonly Func<double> _position;
        private readonly Action<double, bool> _jump;
        private readonly Action<double> _setRate;
        private readonly Action<double> _setBoundary;
        private readonly Action _hold;
        private readonly Action _resume;
        private readonly Action _quit;
        private PracticeSequence _sequence;
        private bool _free;
        private bool _closed;
        private double? _nextPassStart;
        private double _rate = 1;

        public PracticePlayback(PracticePlan plan, double minimum, double maximum, Func<double> position,
            Action<double, bool> jump, Action<double> setRate, Action<double> setBoundary,
            Action hold, Action resume, Action quit)
        {
            _plan = plan;
            MinimumSeconds = minimum;
            MaximumSeconds = maximum;
            _position = position;
            _jump = jump;
            _setRate = setRate;
            _setBoundary = setBoundary;
            _hold = hold;
            _resume = resume;
            _quit = quit;
        }

        public double MinimumSeconds { get; }
        public double MaximumSeconds { get; }
        public double PositionSeconds => Math.Clamp(_position(), MinimumSeconds, MaximumSeconds);
        public bool Paused { get; private set; }
        public bool Completed { get; private set; }
        public bool Resting => !_free && _sequence?.Phase == PracticePhase.Resting;
        public bool IsPlaying => !_closed && !Paused && !Resting && !Completed;
        internal bool AtSegmentEnd => IsPlaying && _position() >= (_free ? MaximumSeconds : _sequence.EndSeconds);
        public double Rate => _rate;

        public void Restart()
        {
            if (_closed) return;
            Paused = false;
            _free = false;
            _nextPassStart = null;
            double start = Math.Max(_plan.StartSeconds, MinimumSeconds);
            double end = Math.Min(_plan.EndSeconds ?? MaximumSeconds, MaximumSeconds);
            Completed = end <= start;
            if (Completed)
            {
                _hold();
                return;
            }

            _sequence = new PracticeSequence(_plan.Template, start, end);
            _setBoundary(end);
            if (Resting) _jump(start, false);
            ApplyStep();
        }

        private void ApplyStep()
        {
            Completed = _sequence.Phase == PracticePhase.Completed;
            if (Completed || Resting)
            {
                _hold();
                return;
            }

            SetRate(_sequence.Speed);
            _jump(_nextPassStart ?? _sequence.StartSeconds, true);
            _nextPassStart = null;
        }

        public void SetRate(double speed)
        {
            if (_closed || !double.IsFinite(speed) || speed < PracticeStep.MIN_SPEED || speed > PracticeStep.MAX_SPEED) return;
            _rate = (float)speed;
            if (!_free && _sequence?.Phase == PracticePhase.Playing) _sequence.SetSpeed((float)speed);
            _setRate(_rate);
        }

        public void TogglePause()
        {
            if (_closed || Completed) return;
            Paused = !Paused;
            if (Resting) return;
            if (Paused) _hold();
            else _resume();
        }

        public void Seek(double chartSeconds)
        {
            if (_closed || (!Paused && !Completed) || !double.IsFinite(chartSeconds)) return;
            double target = Math.Clamp(chartSeconds, MinimumSeconds, MaximumSeconds);
            if (_free || Completed || target < _sequence.StartSeconds || target >= _sequence.EndSeconds)
            {
                _free = true;
                _setBoundary(MaximumSeconds);
                _nextPassStart = null;
            }
            else if (Resting)
            {
                // Keep the remaining silence. Only the next audible pass starts at the cursor.
                _nextPassStart = target;
            }
            Completed = false;
            Paused = true;
            _jump(target, false);
        }

        /// <returns>True when changing steps, so the caller does not judge the old frame.</returns>
        public bool Tick(double deltaSeconds)
        {
            if (_closed || Paused || Completed) return false;
            if (_free)
            {
                if (_position() < MaximumSeconds) return false;
                Completed = true;
                _hold();
                return true;
            }

            if (!_sequence.Advance(_position(), deltaSeconds)) return false;
            ApplyStep();
            return true;
        }

        public void Quit()
        {
            if (_closed) return;
            _closed = true;
            _hold();
            _quit();
        }
    }
}
