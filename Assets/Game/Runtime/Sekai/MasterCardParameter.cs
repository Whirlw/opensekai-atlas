using MessagePack;

namespace Sekai
{
	public class MasterCardParameter : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _cardId;
		public int cardId
		{
		    get => _cardId;
		    set => _cardId = value;
		}

				private int _cardLevel;
		public int cardLevel
		{
		    get => _cardLevel;
		    set => _cardLevel = value;
		}

		public string cardParameterType;

				private int _power;
		public int power
		{
		    get => _power;
		    set => _power = value;
		}

		public CardParameterType CardParameterType
		{
			get
			{
				throw null;
			}
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public MasterCardParameter()
		{
		}
	}
}
