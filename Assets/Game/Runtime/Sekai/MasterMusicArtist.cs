using System.Collections.Generic;
using System.Globalization;

namespace Sekai
{
	public class MasterMusicArtist
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _pronunciation;
		public string pronunciation
		{
		    get => _pronunciation;
		    set => _pronunciation = value;
		}

		public bool ContainsWord(string word, CompareOptions options)
		{
			throw null;
		}

		public IEnumerable<string> GetFilteredElementsByWord(string word, CompareOptions options)
		{
			throw null;
		}

		public MasterMusicArtist()
		{
		}
	}
}
