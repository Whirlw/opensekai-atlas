using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A note object is reused across runs. Every transient field has to come back to the value a brand
	/// new instance would have, otherwise a leftover timestamp or flag shows up as a bug that only
	/// reproduces on the second play. This walks every value typed field instead of trusting a hand
	/// written list in <c>ResetNote</c>.
	/// </summary>
	[TestFixture]
	public class PoolHygieneTests
	{
		private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		/// <summary>
		/// Fields that belong to the note's identity: they are set by the constructor or by the chart
		/// setup that runs again on every spawn, so <c>ResetNote</c> is not supposed to touch them.
		/// Anything not listed here is per run state and must come back.
		/// </summary>
		private static readonly HashSet<string> Identity = new HashSet<string>
		{
			"DefaultLeftLane", "DefaultRightLane",
			"LaneStart", "LaneEnd", "LaneStartF", "LaneEndF",
			"JudgeLaneStart", "JudgeLaneEnd",
			"Category", "Type", "Direction", "LineType",
			"IsSkip", "IsDecoration", "Id",
			"speedRatio", "IsNegativeSpeed",
			"LaneOffset", "_flickDistance",
		};

		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static IEnumerable<TestCaseData> NoteTypes()
		{
			yield return new TestCaseData(typeof(NormalNote), (Func<NoteBase>)(() => new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f))).SetName("NormalNote");
			yield return new TestCaseData(typeof(FrictionNote), (Func<NoteBase>)(() => new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f))).SetName("FrictionNote");
			yield return new TestCaseData(typeof(FlickNote), (Func<NoteBase>)(() => new FlickNote(Info(1f), 3, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f))).SetName("FlickNote");
			yield return new TestCaseData(typeof(FrictionFlickNote), (Func<NoteBase>)(() => new FrictionFlickNote(Info(1f), 4, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f))).SetName("FrictionFlickNote");
			yield return new TestCaseData(typeof(LongNote), (Func<NoteBase>)(() => new LongNote(Info(1f), 5, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f))).SetName("LongNote");
			yield return new TestCaseData(typeof(GuideNote), (Func<NoteBase>)(() => new GuideNote(Info(1f), 6, 4, 5, NoteCategory.Long, NoteType.Default, null, 1f))).SetName("GuideNote");
		}

		[TestCaseSource(nameof(NoteTypes))]
		public void ResetRestoresEveryValueField(Type type, Func<NoteBase> create)
		{
			NoteBase fresh = create();
			NoteBase reused = create();

			Dictionary<string, object> expected = Snapshot(fresh);
			Dirty(reused);
			reused.ResetNote();

			var wrong = new List<string>();
			foreach (KeyValuePair<string, object> pair in Snapshot(reused))
			{
				if (!expected.TryGetValue(pair.Key, out object want))
				{
					continue;
				}

				if (!Equals(want, pair.Value))
				{
					wrong.Add(pair.Key + " expected " + want + " but was " + pair.Value);
				}
			}

			Assert.That(wrong, Is.Empty, type.Name + " keeps a transient field across ResetNote:\n  " + string.Join("\n  ", wrong));
		}

		private static Dictionary<string, object> Snapshot(NoteBase note)
		{
			var map = new Dictionary<string, object>();
			for (Type t = note.GetType(); t != null && t != typeof(object); t = t.BaseType)
			{
				foreach (FieldInfo field in t.GetFields(Fields))
				{
					if (!field.FieldType.IsValueType || field.IsInitOnly)
					{
						continue;
					}

					if (Identity.Contains(SimpleName(field.Name)))
					{
						continue;
					}

					map[t.Name + "." + field.Name] = field.GetValue(note);
				}
			}

			return map;
		}

		private static void Dirty(NoteBase note)
		{
			for (Type t = note.GetType(); t != null && t != typeof(object); t = t.BaseType)
			{
				foreach (FieldInfo field in t.GetFields(Fields))
				{
					if (!field.FieldType.IsValueType || field.IsInitOnly || Identity.Contains(SimpleName(field.Name)))
					{
						continue;
					}

					field.SetValue(note, Different(field.FieldType, field.GetValue(note)));
				}
			}
		}

		private static object Different(Type type, object current)
		{
			if (type == typeof(bool))
			{
				return !(bool)current;
			}

			if (type == typeof(int))
			{
				return (int)current + 3;
			}

			if (type == typeof(float))
			{
				return (float)current + 0.75f;
			}

			if (type == typeof(Vector2))
			{
				return (Vector2)current + new Vector2(0.75f, 0.75f);
			}

			if (type == typeof(Vector3))
			{
				return (Vector3)current + new Vector3(0.75f, 0.75f, 0.75f);
			}

			if (type.IsEnum)
			{
				Array values = Enum.GetValues(type);
				object other = null;
				foreach (object value in values)
				{
					if (!Equals(value, current))
					{
						other = value;
						break;
					}
				}

				return other ?? current;
			}

			return current;
		}

		/// <summary>
		/// Auto properties show up as <c>&lt;Name&gt;k__BackingField</c>; the identity list is written in
		/// plain property names so it stays readable.
		/// </summary>
		private static string SimpleName(string fieldName)
		{
			if (fieldName.Length > 1 && fieldName[0] == '<')
			{
				int end = fieldName.IndexOf('>');
				if (end > 1)
				{
					return fieldName.Substring(1, end - 1);
				}
			}

			return fieldName;
		}
	}
}
