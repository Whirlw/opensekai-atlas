using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MasterGameCharacter : IMessagePackSerializationCallbackReceiver
	{
		public const string GENDER_TYPE_MALE = "male";

		public const string GENDER_TYPE_FEMALE = "female";

		public const string GENDER_TYPE_SECRET = "secret";

		public const string FIGURE_TYPE_BOYS = "boys";

		public const string FIGURE_TYPE_MENS = "mens";

		public const string FIGURE_TYPE_GIRLS = "girls";

		public const string FIGURE_TYPE_LADIES = "ladies";

		public const string CHARACTER_UNIT_THEME_PARK = "theme_park";

		public const string CHARACTER_UNIT_IDOL = "idol";

		public const string CHARACTER_UNIT_STREET = "street";

		public const string CHARACTER_UNIT_LIGHT_SOUND = "light_sound";

		public const string CHARACTER_UNIT_SCHOOL_REFUSAL = "school_refusal";

		public const string CHARACTER_UNIT_PIAPRO = "piapro";

		public const string CHARACTER_UNIT_NONE = "none";

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _resourceId;
		public int resourceId
		{
		    get => _resourceId;
		    set => _resourceId = value;
		}

				private string _firstName;
		public string firstName
		{
		    get => _firstName;
		    set => _firstName = value;
		}

				private string _givenName;
		public string givenName
		{
		    get => _givenName;
		    set => _givenName = value;
		}

				private string _firstNameEnglish;
		public string firstNameEnglish
		{
		    get => _firstNameEnglish;
		    set => _firstNameEnglish = value;
		}

				private string _givenNameEnglish;
		public string givenNameEnglish
		{
		    get => _givenNameEnglish;
		    set => _givenNameEnglish = value;
		}

				private string _ruby;
		public string ruby
		{
		    get => _ruby;
		    set => _ruby = value;
		}

		public string gender;

				private float _height;
		public float height
		{
		    get => _height;
		    set => _height = value;
		}

				private float _live2dHeightAdjustment;
		public float live2dHeightAdjustment
		{
		    get => _live2dHeightAdjustment;
		    set => _live2dHeightAdjustment = value;
		}

		public string figure;

				private string _breastSize;
		public string breastSize
		{
		    get => _breastSize;
		    set => _breastSize = value;
		}

				private string _unit;
		public string unit
		{
		    get => _unit;
		    set => _unit = value;
		}

		public string supportUnitType;

		public string FullName
		{
			get
			{
				throw null;
			}
		}

		public string FullNameEnglish
		{
			get
			{
				throw null;
			}
		}

		public UnitType UnitType
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

		public SupportUnitType SupportUnitType
		{
			get
			{
				throw null;
			}
		}

		public bool IsFigureMan
		{
			get
			{
				throw null;
			}
		}

		public GenderType Gender
		{
			get
			{
				throw null;
			}
		}

		public CostumeType CostumeType
		{
			get
			{
				throw null;
			}
		}

		public FigureType Figure
		{
			get
			{
				throw null;
			}
		}

		public void ConvertUnitType()
		{
			throw null;
		}

		void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
		{
			throw null;
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			throw null;
		}

		public MasterGameCharacter()
		{
		}
	}
}
