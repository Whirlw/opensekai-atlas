using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace OpenSekai.Tests.EditMode.Graphics
{
    /// <summary>
    /// Guards the sprites that moved off the baked <c>sactx-*</c> atlases. Each one is its own PNG under
    /// <c>Assets/Graphics/AtlasSprites/&lt;atlas&gt;/</c>, packed by a Sprite Atlas of the same name, so new
    /// art of any size goes in without a hand-picked rect. These rules keep that true.
    /// </summary>
    public class AtlasSpritesFolderTests
    {
        private const string ROOT = "Assets/Graphics/AtlasSprites";

        private static IEnumerable<string> AtlasFolders()
        {
            return Directory.Exists(ROOT) ? Directory.GetDirectories(ROOT) : new string[0];
        }

        [Test]
        public void EverySpriteIsASingleFullRectSprite()
        {
            var problems = new List<string>();
            foreach (var folder in AtlasFolders())
            {
                foreach (var file in Directory.GetFiles(folder, "*.png"))
                {
                    var path = file.Replace('\\', '/');
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        problems.Add(path + ": no texture importer");
                        continue;
                    }
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        problems.Add(path + ": not a single sprite, so prefab references (fileID 21300000) break");
                    }
                    if (settings.spriteMeshType != SpriteMeshType.FullRect)
                    {
                        problems.Add(path + ": tight mesh clips repainted art to the old outline");
                    }
                }
            }
            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void EveryFolderIsPackedByItsSpriteAtlas()
        {
            var problems = new List<string>();
            foreach (var folder in AtlasFolders())
            {
                var name = Path.GetFileName(folder);
                var atlasPath = OpenSekai.EditorTools.AtlasSpriteMigration.AtlasPathFor(name);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null)
                {
                    problems.Add(name + ": no " + atlasPath);
                    continue;
                }
                var importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
                var included = importer != null ? importer.includeInBuild : atlas.IsIncludeInBuild();
                if (!included)
                {
                    problems.Add(name + ": atlas is not included in the build");
                }
                foreach (var file in Directory.GetFiles(folder, "*.png"))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
                    if (sprite != null && !atlas.CanBindTo(sprite))
                    {
                        problems.Add(name + ": " + sprite.name + " is not packed by its atlas");
                    }
                }
            }
            CollectionAssert.IsEmpty(problems);
        }
    }
}
