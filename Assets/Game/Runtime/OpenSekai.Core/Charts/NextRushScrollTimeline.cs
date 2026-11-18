using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSekai.Charts
{
    /// <summary>NextRUSH+ group coordinates and transition runs, in chart seconds.</summary>
    // Reference: sekai/lib/timescale.py at NextRushCompatibility.UpstreamCommit (Kyle Chang, MIT).
    public sealed class NextRushScrollTimeline
    {
        public readonly struct Change
        {
            public readonly double Time, Speed, Skip;
            public readonly int Ease;
            public readonly bool Scroll, Hidden;

            public Change(double time, double speed, double skip, int ease, bool scroll, bool hidden)
            {
                Time = time;
                Speed = speed;
                Skip = skip;
                Ease = ease;
                Scroll = scroll;
                Hidden = hidden;
            }
        }

        public readonly struct Position
        {
            internal readonly int Marker;
            internal readonly double Coordinate, Speed;
            public readonly double Time;
            public readonly bool Hidden;

            internal Position(int marker, double time, double coordinate, double speed, bool hidden)
            {
                Marker = marker;
                Time = time;
                Coordinate = coordinate;
                Speed = speed;
                Hidden = hidden;
            }
        }

        // Across a run, D(left, hit) = Forward + Ratio * D(right, hit).
        private readonly struct Run
        {
            internal readonly double Ratio, Forward, Backward;
            internal static Run Identity => new Run(1, 0, 0);

            internal Run(double ratio, double forward, double backward)
            {
                Ratio = ratio;
                Forward = forward;
                Backward = backward;
            }

            internal Run Then(Run next) => new Run(Ratio * next.Ratio,
                Forward + Ratio * next.Forward, next.Backward + Backward / next.Ratio);
        }

        private readonly Change[] _markers;
        private readonly double[] _coordinate, _scrollSkip, _prefixMaximum;
        private readonly int[] _runOf;
        private readonly Position[] _boundaries;
        private readonly Run[] _runIndex;
        private readonly bool _hasScroll;
        private readonly double _monotoneScrollMinimum;
        private readonly double _forcePreempt;
        private Position _frame;
        private bool _hasFrame;

        public NextRushScrollTimeline(IReadOnlyList<Change> changes, double forceSpeed)
        {
            if (forceSpeed > 12) throw new InvalidDataException("NextRUSH+ forceNoteSpeed 超出有效范围");
            _forcePreempt = forceSpeed > 0 ? 0.35 + 3.65 * Math.Pow((12 - forceSpeed) / 11, 1.31) : 0;
            _markers = new Change[changes.Count];
            _coordinate = new double[changes.Count];
            _scrollSkip = new double[changes.Count];
            _prefixMaximum = new double[changes.Count];
            _runOf = new int[changes.Count];
            var boundaries = new List<Position>();
            double maxBefore = double.NegativeInfinity;
            double minimumScrollSpeed = double.PositiveInfinity;

            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                if (i > 0 && change.Time < changes[i - 1].Time)
                    throw new InvalidDataException("NextRUSH+ 变速事件必须按时间排列");
                if (change.Ease < 0 || change.Ease > 41 || change.Ease >= 30 && change.Ease <= 37)
                    throw new InvalidDataException("NextRUSH+ 变速缓动无效");
                _markers[i] = change;
                var previous = i == 0 ? default : _markers[i - 1];
                double beforeSkip = i == 0 ? change.Time :
                    _coordinate[i - 1] + Integral(i - 1, previous.Time, change.Time);
                _coordinate[i] = beforeSkip + change.Skip;
                _scrollSkip[i] = (i == 0 ? 0 : _scrollSkip[i - 1]) + change.Skip / ScrollSpeed(change.Speed);
                // Bound the entire preceding interval, including easing through zero.
                double upper = i == 0 ? change.Time : _coordinate[i - 1] + (change.Time - previous.Time)
                    * MaximumSpeed(i - 1);
                maxBefore = _prefixMaximum[i] = Math.Max(maxBefore, upper);
                _hasScroll |= change.Scroll;
                minimumScrollSpeed = change.Scroll && change.Skip >= 0 && change.Speed >= 0
                    ? Math.Min(minimumScrollSpeed, ScrollSpeed(change.Speed)) : 0;
                // The time before the first marker belongs to the same run when it uses TIMESCALE.
                if (i == 0 ? change.Scroll : previous.Scroll != change.Scroll)
                    boundaries.Add(new Position(i, change.Time, _coordinate[i], change.Speed, change.Hidden));
                _runOf[i] = boundaries.Count;
            }

            _boundaries = boundaries.ToArray();
            _monotoneScrollMinimum = minimumScrollSpeed;
            int size = 1;
            while (size < _boundaries.Length) size *= 2;
            _runIndex = new Run[2 * size];
            double logRatio = 0, minLogRatio = 0, maxLogRatio = 0;
            for (int i = 0; i < size; i++)
            {
                var piece = _runIndex[size + i] = i + 1 < _boundaries.Length
                    ? Piece(_boundaries[i], _boundaries[i + 1]) : Run.Identity;
                logRatio += Math.Log(Math.Abs(piece.Ratio));
                minLogRatio = Math.Min(minLogRatio, logRatio);
                maxLogRatio = Math.Max(maxLogRatio, logRatio);
            }
            // Bound all query subranges, including those that cross two indexed blocks.
            if (maxLogRatio - minLogRatio > 500)
                throw new InvalidDataException("NextRUSH+ 变速模式组合超出数值范围");
            for (int i = size - 1; i > 0; i--)
            {
                var summary = _runIndex[i] = _runIndex[2 * i].Then(_runIndex[2 * i + 1]);
                if (!Finite(summary.Ratio) || summary.Ratio == 0 || !Finite(summary.Forward) || !Finite(summary.Backward))
                    throw new InvalidDataException("NextRUSH+ 变速模式组合超出数值范围");
            }
        }

        public double Preempt(double playerPreempt) => _forcePreempt > 0 ? _forcePreempt : playerPreempt;

        // Live uses one display clock for all the consumers of a group.
        public Position SampleFrame(double time)
        {
            if (!_hasFrame || _frame.Time != time)
            {
                _frame = Sample(time);
                _hasFrame = true;
            }
            return _frame;
        }

        public Position Sample(double time)
        {
            int low = 0, high = _markers.Length;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (_markers[mid].Time <= time) low = mid + 1; else high = mid;
            }
            int marker = low - 1;
            if (marker < 0) return new Position(-1, time, time, 1, false);
            var change = _markers[marker];
            return new Position(marker, time, _coordinate[marker] + Integral(marker, change.Time, time),
                SpeedAt(marker, time), change.Hidden);
        }

        public double Distance(Position now, Position hit)
        {
            return Math.Max(-1e20, Math.Min(1e20, DistanceUnchecked(now, hit)));
        }

        private double DistanceUnchecked(Position now, Position hit)
        {
            int leftRun = RunOf(now), rightRun = RunOf(hit);
            if (leftRun == rightRun)
                return IsScroll(now) ? ScrollSpeed(now.Speed) * Width(now, hit) : CoordinateDifference(now, hit);

            bool reverse = leftRun > rightRun;
            Position left = reverse ? hit : now, right = reverse ? now : hit;
            int first = Math.Min(leftRun, rightRun), last = Math.Max(leftRun, rightRun);
            var total = Piece(left, _boundaries[first])
                .Then(BetweenBoundaries(first, last - 1))
                .Then(Piece(_boundaries[last - 1], right));
            return reverse ? -total.Backward : total.Forward;
        }

        // Earliest conservative visual activation. Before the first marker the distance has unit slope.
        public double ActivationTime(Position target, double preempt)
        {
            if (target.Marker < 0) return target.Time - preempt;
            double first = _markers[0].Time;
            if (_hasScroll)
            {
                var before = new Position(-1, first, first, 1, false);
                double beforeFirst = first + Distance(before, target) - preempt;
                if (beforeFirst <= first) return beforeFirst;
                return _monotoneScrollMinimum > 0 ? Math.Max(first, target.Time - preempt / _monotoneScrollMinimum) : first;
            }

            double threshold = target.Coordinate - preempt;
            if (threshold <= first) return threshold;
            int low = 1, high = target.Marker + 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (_prefixMaximum[mid] >= threshold) high = mid; else low = mid + 1;
            }
            int marker = low - 1;
            double speed = MaximumSpeed(marker);
            double start = _markers[marker].Time;
            if (threshold <= _coordinate[marker] || speed <= 0) return start;
            return Math.Min(target.Time, start + (threshold - _coordinate[marker]) / speed);
        }

        private Run BetweenBoundaries(int first, int last)
        {
            var left = Run.Identity;
            var right = Run.Identity;
            int size = _runIndex.Length / 2;
            for (first += size, last += size; first < last; first /= 2, last /= 2)
            {
                if ((first & 1) != 0) left = left.Then(_runIndex[first++]);
                if ((last & 1) != 0) right = _runIndex[--last].Then(right);
            }
            return left.Then(right);
        }

        private Run Piece(Position left, Position right)
        {
            if (!IsScroll(left))
            {
                double distance = CoordinateDifference(left, right);
                return new Run(1, distance, distance);
            }
            double a = ScrollSpeed(left.Speed), b = ScrollSpeed(right.Speed);
            double width = Width(left, right);
            return new Run(a / b, a * width, b * width);
        }

        private int RunOf(Position position) => position.Marker < 0 ? 0 : _runOf[position.Marker];
        private bool IsScroll(Position position) => position.Marker >= 0 && _markers[position.Marker].Scroll;
        private double SkipAt(Position position) => position.Marker < 0 ? 0 : _scrollSkip[position.Marker];
        private double Width(Position left, Position right) => right.Time - left.Time + (SkipAt(right) - SkipAt(left));
        private double CoordinateDifference(Position left, Position right) =>
            left.Marker == right.Marker ? Integral(left.Marker, left.Time, right.Time) : right.Coordinate - left.Coordinate;
        private static double ScrollSpeed(double speed) => speed < 0 ? Math.Min(speed, -0.0001) : Math.Max(speed, 0.0001);
        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
        private double MaximumSpeed(int marker)
        {
            var change = _markers[marker];
            double speed = change.Speed;
            if (marker + 1 < _markers.Length && change.Ease != 0 && change.Ease != 38)
                speed = Math.Max(speed, _markers[marker + 1].Speed);
            return Math.Max(0, speed);
        }

        private double Integral(int marker, double left, double right)
        {
            if (marker < 0) return right - left;
            var change = _markers[marker];
            if (marker + 1 == _markers.Length || change.Ease == 0 || change.Ease == 38 || left == right)
                return change.Speed * (right - left);
            var next = _markers[marker + 1];
            double span = next.Time - change.Time;
            if (span == 0) return 0;
            double a = (left - change.Time) / span, b = (right - change.Time) / span;
            return change.Speed * (right - left)
                + (next.Speed - change.Speed) * span * (EaseIntegral(change.Ease, b) - EaseIntegral(change.Ease, a));
        }

        private double SpeedAt(int marker, double time)
        {
            var change = _markers[marker];
            if (marker + 1 == _markers.Length || change.Ease == 0 || change.Ease == 38 || time == change.Time)
                return change.Speed;
            var next = _markers[marker + 1];
            double u = (time - change.Time) / (next.Time - change.Time);
            return change.Speed + (next.Speed - change.Speed) * EaseValue(change.Ease, u);
        }

        internal static double EaseValue(int ease, double u)
        {
            switch (ease)
            {
                case 1: return u;
                case 39: return 1;
                case 40: return u < 0.5 ? 0 : 1;
                case 41: return 0.5;
            }
            int family = (ease - 2) / 4;
            switch ((ease - 2) % 4)
            {
                case 0: return InValue(family, u);
                case 1: return 1 - InValue(family, 1 - u);
                case 2: return u <= 0.5 ? InValue(family, 2 * u) / 2 : 1 - InValue(family, 2 - 2 * u) / 2;
                default: return u <= 0.5 ? (1 - InValue(family, 1 - 2 * u)) / 2 : (1 + InValue(family, 2 * u - 1)) / 2;
            }
        }

        private static double InValue(int family, double u)
        {
            switch (family)
            {
                case 0: return u * u;
                case 1: return 1 - Math.Cos(u * Math.PI / 2);
                case 2: return u * u * u;
                case 3: return Math.Pow(u, 4);
                case 4: return Math.Pow(u, 5);
                case 5: return u == 0 ? 0 : Math.Pow(2, 10 * u - 10);
                default: return 1 - Math.Sqrt(Math.Max(0, 1 - u * u));
            }
        }

        private static double EaseIntegral(int ease, double u)
        {
            u = Math.Max(0, Math.Min(1, u));
            switch (ease)
            {
                case 1: return u * u / 2;
                case 39: return u;
                case 40: return Math.Max(u - 0.5, 0);
                case 41: return u / 2;
            }
            int family = (ease - 2) / 4;
            double total = InIntegral(family, 1);
            switch ((ease - 2) % 4)
            {
                case 0: return InIntegral(family, u);
                case 1: return u - total + InIntegral(family, 1 - u);
                case 2: return u <= 0.5 ? InIntegral(family, 2 * u) / 4 : u - 0.5 + InIntegral(family, 2 - 2 * u) / 4;
                default: return u <= 0.5 ? u / 2 + (InIntegral(family, 1 - 2 * u) - total) / 4
                    : u / 2 + (InIntegral(family, 2 * u - 1) - total) / 4;
            }
        }

        private static double InIntegral(int family, double u)
        {
            switch (family)
            {
                case 0: return u * u * u / 3;
                case 1: return u - Math.Sin(u * Math.PI / 2) * 2 / Math.PI;
                case 2: return Math.Pow(u, 4) / 4;
                case 3: return Math.Pow(u, 5) / 5;
                case 4: return Math.Pow(u, 6) / 6;
                case 5: return (Math.Pow(2, 10 * u - 10) - 1.0 / 1024) / (10 * Math.Log(2));
                default: return u - (u * Math.Sqrt(Math.Max(0, 1 - u * u)) + Math.Asin(u)) / 2;
            }
        }
    }
}
