using System.Runtime.CompilerServices;
using Sekai;

namespace Sekai.Live
{
	public struct JudgeTimeData
	{
		public readonly JudgeFrameType NoteType
		{
			[CompilerGenerated]
			get;
		}

		public float PerfectBeforeJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float PerfectAfterJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float GreatBeforeJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float GreatAfterJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float GoodBeforeJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float GoodAfterJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float BadBeforeJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public float BadAfterJudgeTime
		{
			[CompilerGenerated]
			readonly get;
			[CompilerGenerated]
			private set;
		}

		public JudgeTimeData(JudgeFrameType noteType, float perfectBeforeJudgeTime, float perfectAfterJudgeTime, float greatBeforeJudgeTime, float greatAfterJudgeTime, float goodBeforeJudgeTime, float goodAfterJudgeTime, float badBeforeJudgeTime, float badAfterJudgeTime)
		{
			NoteType = noteType;
			PerfectBeforeJudgeTime = perfectBeforeJudgeTime;
			PerfectAfterJudgeTime = perfectAfterJudgeTime;
			GreatBeforeJudgeTime = greatBeforeJudgeTime;
			GreatAfterJudgeTime = greatAfterJudgeTime;
			GoodBeforeJudgeTime = goodBeforeJudgeTime;
			GoodAfterJudgeTime = goodAfterJudgeTime;
			BadBeforeJudgeTime = badBeforeJudgeTime;
			BadAfterJudgeTime = badAfterJudgeTime;
		}
	}
}
