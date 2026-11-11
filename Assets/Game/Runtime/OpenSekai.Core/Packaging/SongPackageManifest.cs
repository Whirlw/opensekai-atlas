using System.Security.Cryptography;
using Newtonsoft.Json;

namespace OpenSekai.Shared.Packages
{
	public class SongPackageManifest
	{
				private int _formatVersion = 1;
		public int formatVersion
		{
		    get => _formatVersion;
		    set => _formatVersion = value;
		}

				private string _id;
		public string id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _title;
		public string title
		{
		    get => _title;
		    set => _title = value;
		}

				private string _scoreTitle;
		public string scoreTitle
		{
		    get => _scoreTitle;
		    set => _scoreTitle = value;
		}

				private string _composer;
		public string composer
		{
		    get => _composer;
		    set => _composer = value;
		}

				private string _lyricist;
		public string lyricist
		{
		    get => _lyricist;
		    set => _lyricist = value;
		}

				private string _arranger;
		public string arranger
		{
		    get => _arranger;
		    set => _arranger = value;
		}

				private string _singer;
		public string singer
		{
		    get => _singer;
		    set => _singer = value;
		}

				private string _collaborationLabel;
		public string collaborationLabel
		{
		    get => _collaborationLabel;
		    set => _collaborationLabel = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private string _audioFileName;
		public string audioFileName
		{
		    get => _audioFileName;
		    set => _audioFileName = value;
		}

				private string _jacketFileName;
		public string jacketFileName
		{
		    get => _jacketFileName;
		    set => _jacketFileName = value;
		}

				private string _scoreFileName;
		public string scoreFileName
		{
		    get => _scoreFileName;
		    set => _scoreFileName = value;
		}

				private string _videoFileName;
		public string videoFileName
		{
		    get => _videoFileName;
		    set => _videoFileName = value;
		}

				private float _fillerSec;
		public float fillerSec
		{
		    get => _fillerSec;
		    set => _fillerSec = value;
		}

				private int _secForMusicScoreMaker;
		public int secForMusicScoreMaker
		{
		    get => _secForMusicScoreMaker;
		    set => _secForMusicScoreMaker = value;
		}

				private float _previewStartTimeSec;
		public float previewStartTimeSec
		{
		    get => _previewStartTimeSec;
		    set => _previewStartTimeSec = value;
		}

				private string _userName;
		public string userName
		{
		    get => _userName;
		    set => _userName = value;
		}

				private string _musicDifficultyType;
		public string musicDifficultyType
		{
		    get => _musicDifficultyType;
		    set => _musicDifficultyType = value;
		}

				private int _playLevel;
		public int playLevel
		{
		    get => _playLevel;
		    set => _playLevel = value;
		}

		[JsonIgnore]
		public bool HasScoreFileName => !string.IsNullOrEmpty(scoreFileName);

		[JsonIgnore]
		public bool HasAudioFileName => !string.IsNullOrEmpty(audioFileName);

		private const string SHORT_ID_CHARS = "0123456789abcdefghijklmnopqrstuvwxyz";

		internal static string GenerateShortId()
		{
			byte[] bytes = new byte[12];
			using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(bytes);
			}

			char[] chars = new char[12];
			for (int i = 0; i < chars.Length; i++)
			{
				chars[i] = SHORT_ID_CHARS[bytes[i] % SHORT_ID_CHARS.Length];
			}
			return new string(chars);
		}

		public void Normalize()
		{
			if (formatVersion <= 0)
			{
				formatVersion = 1;
			}
			if (string.IsNullOrWhiteSpace(id))
			{
				id = GenerateShortId();
			}
			if (string.IsNullOrWhiteSpace(title))
			{
				title = "Untitled";
			}
			if (string.IsNullOrWhiteSpace(scoreTitle))
			{
				scoreTitle = title;
			}
			composer ??= string.Empty;
			lyricist ??= string.Empty;
			arranger ??= string.Empty;
			singer ??= string.Empty;
			collaborationLabel ??= string.Empty;
			description ??= string.Empty;
			audioFileName ??= "audio.ogg";
			jacketFileName ??= "jacket.png";
			scoreFileName ??= "score.json";
			videoFileName ??= string.Empty;
			if (fillerSec < 0f)
			{
				fillerSec = 0f;
			}
			if (secForMusicScoreMaker < 0)
			{
				secForMusicScoreMaker = 0;
			}
			if (previewStartTimeSec < 0f)
			{
				previewStartTimeSec = 0f;
			}
			userName ??= string.Empty;
			if (string.IsNullOrWhiteSpace(musicDifficultyType))
			{
				musicDifficultyType = "master";
			}
			if (playLevel < 0)
			{
				playLevel = 0;
			}
		}
	}
}
