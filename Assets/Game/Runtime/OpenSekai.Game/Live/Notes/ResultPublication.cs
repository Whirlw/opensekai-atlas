using System;
using OpenSekai.Shared.Diagnostics;

namespace Sekai.Live
{
	/// <summary>Which callbacks a verdict reaches, and in what order.</summary>
	internal enum PublishShape
	{
		/// <summary>A key or a tail: judgement, combo, score, damage.</summary>
		Key,

		/// <summary>An interior point of a bar: score, combo, damage, judgement.</summary>
		InteriorPoint,

		/// <summary>A head that was answered: damage, combo, judgement, score.</summary>
		Head,

		/// <summary>A head whose window closed unanswered: damage, combo, judgement. No score callback,
		/// so it consumes no base score.</summary>
		ExpiredHead,
	}

	/// <summary>The four callbacks a verdict can reach, as one value.</summary>
	internal readonly struct ResultCallbacks
	{
		internal readonly Action<NoteBase> Judgment;
		internal readonly Action<NoteBase> Combo;
		internal readonly Action<NoteBase> Score;
		internal readonly Action<NoteBase> Damage;

		internal ResultCallbacks(Action<NoteBase> judgment, Action<NoteBase> combo, Action<NoteBase> score, Action<NoteBase> damage)
		{
			Judgment = judgment;
			Combo = combo;
			Score = score;
			Damage = damage;
		}
	}

	/// <summary>
	/// The one door a verdict leaves through. Every judge point publishes here, keyed by its own object
	/// for the take: a key, a tail or an interior point by its note instance, a head by its
	/// <see cref="LongNoteHead.Identity"/>. A chart id is not the key - generated interior points share
	/// one.
	///
	/// The point is marked published before any callback runs, so a callback that settles the same point
	/// again (directly or through a state change) publishes nothing. <see cref="NoteBase.ResetNote"/>
	/// clears the mark, which is what a retry and the pool do.
	/// </summary>
	internal static class ResultPublication
	{
		/// <summary>
		/// Publishes <paramref name="point"/>'s verdict once.
		/// </summary>
		/// <param name="point">The judge point: what combo, score and damage are counted for.</param>
		/// <param name="callbacks">The callbacks of the note the point belongs to.</param>
		/// <param name="visual">The object the judgement effect is shown on. An ordinary head borrows its
		/// bar here; everything else shows itself.</param>
		/// <param name="shape">Which callbacks run, in which order.</param>
		/// <returns>False when the point had already published in this take.</returns>
		internal static bool Publish(NoteBase point, in ResultCallbacks callbacks, NoteBase visual, PublishShape shape)
		{
			if (point == null)
			{
				return false;
			}

			// The one gate for "this note is not judged": hidden points, guide bars, carriers and
			// decorative notes all close their slot here and reach no callback.
			if (!point.HasJudgment)
			{
				point.ClaimPublication();
				return false;
			}

			if (!point.ClaimPublication())
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Judge))
				{
					LiveTrace.Write(LiveTraceCategory.Judge, string.Concat(
						"republishRefused id=", point.Id, " type=", point.GetType().Name, " shape=", shape,
						" res=", point.Result, " t=", point.MusicScoreInfo.time.ToString("F3")));
				}

				return false;
			}

			visual ??= point;
			switch (shape)
			{
				case PublishShape.InteriorPoint:
					callbacks.Score?.Invoke(point);
					callbacks.Combo?.Invoke(point);
					callbacks.Damage?.Invoke(point);
					callbacks.Judgment?.Invoke(visual);
					break;
				case PublishShape.Head:
				case PublishShape.ExpiredHead:
					callbacks.Damage?.Invoke(point);
					callbacks.Combo?.Invoke(point);
					callbacks.Judgment?.Invoke(visual);
					if (shape == PublishShape.Head)
					{
						callbacks.Score?.Invoke(point);
					}

					break;
				default:
					callbacks.Judgment?.Invoke(visual);
					callbacks.Combo?.Invoke(point);
					callbacks.Score?.Invoke(point);
					callbacks.Damage?.Invoke(point);
					break;
			}

			return true;
		}
	}
}
