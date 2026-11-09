using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MasterSkillEnhance : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

		public string skillEnhanceType;

		public string activateEffectValueType;

				private int _activateEffectValue;
		public int activateEffectValue
		{
		    get => _activateEffectValue;
		    set => _activateEffectValue = value;
		}

				private MasterSkillEnhanceCondition _skillEnhanceCondition;
		public MasterSkillEnhanceCondition skillEnhanceCondition
		{
		    get => _skillEnhanceCondition;
		    set => _skillEnhanceCondition = value;
		}

		public ActivateEffectValueType ActivateEffectValueType
		{
			get
			{
				throw null;
			}
		}

		public SkillEnhanceType SkillEnhanceType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			throw null;
		}

		void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
		{
			throw null;
		}

		public MasterSkillEnhance()
		{
		}
	}
}
