using System.Collections.Generic;

namespace Sekai.Core.Live
{
	/// <summary>Converts recent physical-device time intervals to song time across live speed changes.</summary>
	internal sealed class PracticeRateHistory
	{
		// Calibration uses at most 700ms, and a sampled touch may predate the frame by 250ms.
		private const double HISTORY_SECONDS = 1;
		private readonly List<(double time, double speed)> _changes = new();
		private double _initial;

		internal PracticeRateHistory(double rate) => _initial = rate;

		internal void Reset(double rate)
		{
			_changes.Clear();
			_initial = rate;
		}

		internal void Change(double now, double rate)
		{
			int expired = 0;
			while (expired < _changes.Count && _changes[expired].time <= now - HISTORY_SECONDS)
				_initial = _changes[expired++].speed;
			if (expired > 0) _changes.RemoveRange(0, expired);
			if (_changes.Count > 0 && _changes[^1].time == now) _changes.RemoveAt(_changes.Count - 1);
			double previous = _changes.Count == 0 ? _initial : _changes[^1].speed;
			if (previous != rate) _changes.Add((now, rate));
		}

		internal double Distance(double from, double to)
		{
			if (to < from) return -Distance(to, from);
			double speed = _initial;
			double distance = 0;
			foreach (var change in _changes)
			{
				if (change.time >= to) break;
				if (change.time > from)
				{
					distance += (change.time - from) * speed;
					from = change.time;
				}
				speed = change.speed;
			}
			return distance + (to - from) * speed;
		}
	}
}
