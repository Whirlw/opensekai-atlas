namespace Sekai
{
	public class UserAccountManager
	{
		public class UserAccountData
		{
						private string _Credential = string.Empty;
			public string Credential
			{
			    get => _Credential;
			    set => _Credential = value;
			}
		}

		private static readonly UserAccountManager _instance = new UserAccountManager();

		public static UserAccountManager Instance => _instance;

		public bool IsLogin { get; set; }
		public string DeviceId { get; set; } = string.Empty;
		public UserAccountData Data { get; set; } = new UserAccountData();
	}
}
