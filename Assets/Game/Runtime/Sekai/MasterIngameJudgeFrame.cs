using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using MessagePack;
using Sekai.Live;

namespace Sekai
{
	public class MasterIngameJudgeFrame : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _ingameNoteType;
		public string ingameNoteType
		{
		    get => _ingameNoteType;
		    set => _ingameNoteType = value;
		}

				private float _perfectBefore;
		public float perfectBefore
		{
		    get => _perfectBefore;
		    set => _perfectBefore = value;
		}

				private float _perfectAfter;
		public float perfectAfter
		{
		    get => _perfectAfter;
		    set => _perfectAfter = value;
		}

				private float _greatBefore;
		public float greatBefore
		{
		    get => _greatBefore;
		    set => _greatBefore = value;
		}

				private float _greatAfter;
		public float greatAfter
		{
		    get => _greatAfter;
		    set => _greatAfter = value;
		}

				private float _goodBefore;
		public float goodBefore
		{
		    get => _goodBefore;
		    set => _goodBefore = value;
		}

				private float _goodAfter;
		public float goodAfter
		{
		    get => _goodAfter;
		    set => _goodAfter = value;
		}

				private float _badBefore;
		public float badBefore
		{
		    get => _badBefore;
		    set => _badBefore = value;
		}

				private float _badAfter;
		public float badAfter
		{
		    get => _badAfter;
		    set => _badAfter = value;
		}

		public JudgeFrameType NoteType
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float PerfectBeforeJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float PerfectAfterJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float GreatBeforeJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float GreatAfterJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float GoodBeforeJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float GoodAfterJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float BadBeforeJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public float BadAfterJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			var titleCaseType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(ingameNoteType ?? string.Empty);
			Enum.TryParse(titleCaseType, true, out JudgeFrameType noteType);
			NoteType = noteType;

			PerfectBeforeJudgeTime = perfectBefore / 60f;
			PerfectAfterJudgeTime = perfectAfter / 60f;
			GreatBeforeJudgeTime = greatBefore / 60f;
			GreatAfterJudgeTime = greatAfter / 60f;
			GoodBeforeJudgeTime = goodBefore / 60f;
			GoodAfterJudgeTime = goodAfter / 60f;
			BadBeforeJudgeTime = badBefore / 60f;
			BadAfterJudgeTime = badAfter / 60f;
		}

		void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
		{
		}

		public MasterIngameJudgeFrame()
		{
		}
	}
}
