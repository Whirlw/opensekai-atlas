namespace Sekai.ApiData
{
	public class UserCustomMusicScoreDraft
	{
				private object _userCustomMusicScoreInfo;
		public object userCustomMusicScoreInfo
		{
		    get => _userCustomMusicScoreInfo;
		    set => _userCustomMusicScoreInfo = value;
		}
				private int _slotNo;
		public int slotNo
		{
		    get => _slotNo;
		    set => _slotNo = value;
		}
				private string _memo;
		public string memo
		{
		    get => _memo;
		    set => _memo = value;
		}
				private int _baseMusicDifficultyId;
		public int baseMusicDifficultyId
		{
		    get => _baseMusicDifficultyId;
		    set => _baseMusicDifficultyId = value;
		}
				private long _lastSavedAt;
		public long lastSavedAt
		{
		    get => _lastSavedAt;
		    set => _lastSavedAt = value;
		}
	}
}
