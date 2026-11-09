using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Graphics
{
    /// <summary>
    /// Guards the HUD atlas' import settings. The atlas is repainted by hand for every art pass, so a
    /// setting that silently turns a sprite into a cut-out of its old outline throws away the new pixels
    /// without any build error - exactly what happened to the combo digit "1".
    /// </summary>
    public class LiveAtlasSpriteSettingsTests
    {
        private const string AtlasTexturePath = "Assets/Graphics/Textures/sactx-0-2048x2048-ASTC 6x6-LiveAtlas-ff0d1eac.png";
        private const string SpriteFolder = "Assets/Graphics/Sprites";

        [Test]
        public void TheHudAtlasIsDrawnAsFullRectangles()
        {
            var importer = AssetImporter.GetAtPath(AtlasTexturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, "the HUD atlas disappeared from " + AtlasTexturePath);

            var settings = new TextureImporterSettings();
            importer!.ReadTextureSettings(settings);

            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect),
                "a tight mesh is generated from the alpha outline, so repainted art is clipped to the old shape");
        }

        [Test]
        public void EverySpriteStaysInsideItsTexture()
        {
            var problems = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    problems.Add(Path.GetFileNameWithoutExtension(path) + ": not a sprite");
                    continue;
                }

                var texture = sprite.texture;
                if (texture == null)
                {
                    problems.Add(sprite.name + ": no texture");
                    continue;
                }

                // Sprites that a SpriteAtlas packs at build time report the packed texture while their
                // rect still lives in the source layout, so the two do not line up in the editor. Only
                // the atlases this project paints by hand are checked here.
                if (!IsPaintableAtlas(texture.name))
                {
                    continue;
                }

                var rect = sprite.rect;
                var textureWidth = (float)texture.width;
                var textureHeight = (float)texture.height;
                if (rect.width <= 0f || rect.height <= 0f ||
                    rect.xMin < 0f || rect.yMin < 0f ||
                    rect.xMax > textureWidth || rect.yMax > textureHeight)
                {
                    problems.Add(sprite.name + ": rect " + rect + " does not fit " + texture.name +
                                 " (" + texture.width + "x" + texture.height + ")");
                }
            }

            CollectionAssert.IsEmpty(problems);
        }

        /// <summary>The textures <c>Tools/apply-atlas-art.ps1</c> pastes into, by name.</summary>
        private static bool IsPaintableAtlas(string textureName)
        {
            return textureName.Contains("LiveAtlas")
                || textureName.Contains("JudgeAtlas")
                || textureName.Contains("FrameAtlas");
        }
    }
}
