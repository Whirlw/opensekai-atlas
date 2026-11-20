using System.Runtime.CompilerServices;
using Sekai.Live;

namespace Sekai.Core.Live
{
	public class BaseSkillEffect : ISkillEffect
	{
		private int _levelKBackingField;

		private float _lifeTimeKBackingField;

		private int _orderKBackingField;

		private bool _isEncoreKBackingField;

		public virtual SkillType Type
		{
			get
			{
				return SkillType.ScoreUp;
			}
		}

		public virtual bool IsInstant
		{
			get
			{
				return false;
			}
		}

		public int Level
		{
			[CompilerGenerated]
			get
			{
				return _levelKBackingField;
			}
			[CompilerGenerated]
			protected set
			{
				_levelKBackingField = value;
			}
		}

		public float LifeTime
		{
			[CompilerGenerated]
			get
			{
				return _lifeTimeKBackingField;
			}
			[CompilerGenerated]
			protected set
			{
				_lifeTimeKBackingField = value;
			}
		}

		public int Order
		{
			[CompilerGenerated]
			get
			{
				return _orderKBackingField;
			}
			[CompilerGenerated]
			protected set
			{
				_orderKBackingField = value;
			}
		}

		public bool IsEncore
		{
			[CompilerGenerated]
			get
			{
				return _isEncoreKBackingField;
			}
			[CompilerGenerated]
			set
			{
				_isEncoreKBackingField = value;
			}
		}

		public virtual void Activate()
		{
		}

		public virtual (NoteResult, NoteResultDescription) CalculateJudgementResult((NoteResult result, NoteResultDescription description) result)
		{
			return result;
		}

		public virtual float CalculateAdditiveScore(INote note, LiveScore score)
		{
			return 0f;
		}

		public virtual float CalculateScoreFactor(INote note, LiveScore score)
		{
			return 0f;
		}

		public virtual float GetScoreFactor(LiveScore score)
		{
			return 0f;
		}

		public BaseSkillEffect()
		{
		}
	}
}
