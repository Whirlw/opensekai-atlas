using System.Collections.Generic;
using System.Linq;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public static class WordingManager
	{
		private const string REPLACE_CODE_COMMMA = "_x_COMMA_x_";
		private const string WORDING_RESOURCE_PATH = "wording/wording_zh";
		private const string MASTER_WORDING_RESOURCE_PATH = "wording/master_wording_zh";

		private static Dictionary<string, string> _dictionary;

		private static bool _dispKeyFlag;

		private static bool _initFlag;

		public static bool DispKeyFlag
		{
			get
			{
				return _dispKeyFlag;
			}
		}

		public static List<string> ValueList
		{
			get
			{
				Init();
				return _dictionary.Values.ToList();
			}
		}

		public static void Init()
		{
			if (_initFlag)
			{
				ForceInit();
			}
		}

		public static void ForceInit()
		{
			_dictionary.Clear();
			LoadWordingText(WORDING_RESOURCE_PATH, overwrite: false, logMissing: true);
			AddMasterWording();
			_initFlag = false;
		}

		public static void AddMasterWording()
		{
			// Original app reads MasterDataManager.Instance.GetWordings() from CachedMaserDataAll.
			// OpenSekai does not load full master data yet, so this temporary restoration path
			// reads the pre-extracted master wording table from a local Resources txt file.
			LoadWordingText(MASTER_WORDING_RESOURCE_PATH, overwrite: true, logMissing: false);
		}

		private static void LoadWordingText(string resourcePath, bool overwrite, bool logMissing)
		{
			TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
			if (textAsset == null)
			{
				if (logMissing)
				{
					Debug.LogError("wordingファイルの読み込みに失敗しました");
				}
				return;
			}

			string[] lines = textAsset.text.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].TrimEnd('\r');
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}

				AddWordingLine(line, i + 1, overwrite);
			}
		}

		private static void AddWordingLine(string line, int lineNumber, bool overwrite)
		{
			string[] columns = line.Split(',');
			if (columns.Length != 2)
			{
				Debug.LogError(string.Format("{0}行目のデータ\"{1}\"が想定のcsvと異なります", lineNumber, line));
				return;
			}

			string key = columns[0];
			if (string.IsNullOrEmpty(key))
			{
				Debug.LogError(string.Format("{0}行目のデータ\"{1}\"にキーが入力されていません", lineNumber, line));
				return;
			}

			string value = columns[1]
				.Replace(REPLACE_CODE_COMMMA, ",")
				.Replace("\\n", "\n");

			if (_dictionary.ContainsKey(key))
			{
				if (overwrite)
				{
					_dictionary[key] = value;
				}
				else
				{
					Debug.LogError(string.Format("{0}行目のデータ\"{1}\"のキーは既に存在しています", lineNumber, line));
				}
				return;
			}

			_dictionary.Add(key, value);
		}

		public static void DisplayLog(bool separate)
		{
			if (!separate)
			{
				Debug.Log(string.Join(",", _dictionary.Select(pair => pair.Key + "=" + pair.Value)));
				return;
			}

			foreach (var pair in _dictionary)
			{
				Debug.Log(pair.Key + "=" + pair.Value);
			}
		}

		public static string[] GetKeyAll()
		{
			Init();
			return _dictionary.Keys.ToArray();
		}

		public static string Get(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return string.Empty;
			}

			Init();
			if (_dispKeyFlag)
			{
				return key;
			}

			return _dictionary.TryGetValue(key, out var value) ? value : string.Empty;
		}

		public static string GetFormat(string key, params object[] args)
		{
			var format = Get(key);
			return args != null && args.Length > 0 ? string.Format(format, args) : format;
		}

		public static void SetDispKey(bool flag)
		{
			if (_dispKeyFlag == flag)
			{
				return;
			}

			_dispKeyFlag = flag;
			foreach (var customText in Object.FindObjectsByType<CustomText>(FindObjectsSortMode.None))
			{
				customText.UpdateWordingText();
			}
		}

		static WordingManager()
		{
			_dictionary = new Dictionary<string, string>();
			_dispKeyFlag = false;
			_initFlag = true;
		}
	}
}
