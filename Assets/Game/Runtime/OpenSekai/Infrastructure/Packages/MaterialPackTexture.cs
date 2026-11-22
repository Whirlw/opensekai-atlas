using System.IO;
using OpenSekai.MaterialPacks;
using UnityEngine;

namespace OpenSekai.Infrastructure.Packages
{
    internal static class MaterialPackTexture
    {
        internal static (int width, int height) Dimensions(byte[] bytes, string name)
        {
            if (bytes.Length < 45 || Read(0) != unchecked((int)0x89504E47) || Read(4) != 0x0D0A1A0A
                || Read(8) != 13 || Read(12) != 0x49484452 || Read(bytes.Length - 12) != 0
                || Read(bytes.Length - 8) != 0x49454E44 || Read(bytes.Length - 4) != unchecked((int)0xAE426082))
                throw new InvalidDataException("PNG 文件不完整：" + name);
            int width = Read(16), height = Read(20);
            if (width is <= 0 or > MaterialSpriteSlot.MAX_DIMENSION || height is <= 0 or > MaterialSpriteSlot.MAX_DIMENSION)
                throw new InvalidDataException("PNG 尺寸越界：" + name);
            // Budget for a decoded RGBA32 texture, including indexed images with transparency.
            bool supported = bytes[25] switch
            {
                0 or 3 => bytes[24] is 1 or 2 or 4 or 8,
                2 or 4 or 6 => bytes[24] == 8,
                _ => false
            };
            if (!supported) throw new InvalidDataException("需要每通道最多 8 位的 PNG：" + name);
            return (width, height);

            int Read(int i) => bytes[i] << 24 | bytes[i + 1] << 16 | bytes[i + 2] << 8 | bytes[i + 3];
        }

        internal static Texture2D Decode(byte[] bytes, string name)
        {
            var size = Dimensions(bytes, name);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            try
            {
                if (!texture.LoadImage(bytes, true) || texture.width != size.width || texture.height != size.height)
                    throw new InvalidDataException("无法解码 PNG：" + name);
                return texture;
            }
            catch
            {
                Release(texture);
                throw;
            }
        }

        internal static void Release(Texture2D texture)
        {
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
        }
    }
}
