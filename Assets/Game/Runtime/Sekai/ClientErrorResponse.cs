using System.Runtime.CompilerServices;

namespace Sekai
{
	public class ClientErrorResponse
	{
		public const string ERROR_CODE_APPLICATION_UPDATE_REQUIRE = "application";

		public const string ERROR_CODE_DATA_UPDATE_REQUIRE = "data";

		public const string ERROR_CODE_MASTER_UPDATE_REQUIRE = "master";

		public uint HttpStatus
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public string ErrorCode
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public string ErrorMessage
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public ClientErrorResponse()
		{
		}
	}
}
