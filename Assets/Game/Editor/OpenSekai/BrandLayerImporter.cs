using System;
using System.IO;
using OpenSekai.App.UI;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    [ScriptedImporter(2, "brandlayers")]
    internal sealed class BrandLayerImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            context.DependsOnSourceAsset("Assets/Game/Runtime/OpenSekai/App/UI/Home/BrandArt.cs");
            int size = int.Parse(File.ReadAllText(context.assetPath).Trim());
            foreach (BrandArt.Part part in Enum.GetValues(typeof(BrandArt.Part)))
            {
                string name = part.ToString();
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = name + "Texture",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear
                };
                texture.SetPixelData(BrandArt.Render(part, size), 0);
                texture.Apply(true, true);
                context.AddObjectToAsset(texture.name, texture);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                context.AddObjectToAsset(name, sprite);
                // Unity gives the main object the file name. Keep all seven named sprites as sub-assets.
                if (part == BrandArt.Part.Rails) context.SetMainObject(texture);
            }
        }
    }
}
