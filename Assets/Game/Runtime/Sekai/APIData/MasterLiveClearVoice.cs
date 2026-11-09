
namespace Sekai.ApiData
{
	public class MasterLiveClearVoice
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _gameCharacterUnitId;
		public int gameCharacterUnitId
		{
		    get => _gameCharacterUnitId;
		    set => _gameCharacterUnitId = value;
		}

				private bool _isNextGrade;
		public bool isNextGrade
		{
		    get => _isNextGrade;
		    set => _isNextGrade = value;
		}

				private string _liveClearVoiceType;
		public string liveClearVoiceType
		{
		    get => _liveClearVoiceType;
		    set => _liveClearVoiceType = value;
		}

				private string _voiceFileName;
		public string voiceFileName
		{
		    get => _voiceFileName;
		    set => _voiceFileName = value;
		}

		public MasterLiveClearVoice()
		{
		}
	}
}
