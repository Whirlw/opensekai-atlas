using System.Text;
using Newtonsoft.Json;
using Sekai;

namespace CP
{
	/// <summary>
	/// Byte-oriented serializer used for files that are written once and read back at runtime
	/// (currently only the AssetBundle catalog, <c>AssetBundleInfo.bytes</c>). It used to be backed
	/// by MessagePack with a hand written formatter; the catalog is a few dozen kilobytes, so plain
	/// JSON is fast enough and removes a dependency from the player.
	/// </summary>
	public class BinarySerializer
	{
		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			Formatting = Formatting.None,
			NullValueHandling = NullValueHandling.Ignore,
			// The data types initialise their collections in the constructor; Replace makes the
			// deserializer swap them out instead of appending into the freshly created instances.
			ObjectCreationHandling = ObjectCreationHandling.Replace
		};

		public BinarySerializer()
		{
		}

		public static byte[] Serialize<T>(T data)
		{
			string json = JsonConvert.SerializeObject(data, Settings);
			return Encoding.UTF8.GetBytes(json);
		}

		public static int Serialize<T>(ref byte[] buffer, T data)
		{
			byte[] bytes = Serialize(data);
			if (buffer == null || buffer.Length < bytes.Length)
			{
				buffer = new byte[bytes.Length];
			}

			System.Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
			return bytes.Length;
		}

		public static T Deserialize<T>(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return default;
			}

			string json = Encoding.UTF8.GetString(bytes);
			return JsonConvert.DeserializeObject<T>(json, Settings);
		}
	}
}
