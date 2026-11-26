using System;
using System.Collections.Generic;
using System.IO;

namespace ArtV2
{
    /// <summary>One file to draw.</summary>
    public sealed class Asset
    {
        public readonly string Name;
        public readonly int Width, Height, Scale;
        public readonly bool Dither;
        public readonly Action<Canvas> Draw;

        /// <summary>The file this one is copied from, when it is not drawn.</summary>
        public string Source;

        /// <param name="scale">Samples per pixel along each axis.</param>
        /// <param name="dither">For long dark gradients, which would band otherwise.</param>
        public Asset(string name, int width, int height, int scale, bool dither, Action<Canvas> draw)
        {
            Name = name;
            Width = width;
            Height = height;
            Scale = scale;
            Dither = dither;
            Draw = draw;
        }

        /// <summary>A functional mask the engine needs exactly as it is.</summary>
        public static Asset Copy(string name, int width, int height, string source)
        {
            return new Asset(name, width, height, 1, false, null) { Source = source };
        }
    }

    public static class Render
    {
        /// <summary>Draws the chosen files (all of them when <paramref name="only"/> is empty) and reports how many.</summary>
        public static int Run(string root, string fonts, string[] groups, string[] only)
        {
            string data = Path.Combine(root, "Tools", "data");
            Type.Load(fonts);
            int count = 0;
            foreach (string group in groups)
            {
                string folder = Path.Combine(root, group);
                foreach (Asset asset in Group(group, data))
                {
                    if (only != null && only.Length > 0 && !Array.Exists(only, n => Matches(asset.Name, n))) continue;
                    string file = Path.Combine(folder, asset.Name);
                    if (asset.Source != null)
                    {
                        Directory.CreateDirectory(folder);
                        File.Copy(asset.Source, file, true);
                    }
                    else
                    {
                        var canvas = new Canvas(asset.Width, asset.Height, asset.Scale);
                        asset.Draw(canvas);
                        canvas.Save(file, asset.Dither);
                    }

                    Console.WriteLine(group + "/" + asset.Name + " " + asset.Width + "x" + asset.Height);
                    count++;
                }
            }

            return count;
        }

        /// <summary>Every file a group draws, for checking the list without drawing it.</summary>
        public static List<string> Names(string root, string group)
        {
            var names = new List<string>();
            foreach (Asset asset in Group(group, Path.Combine(root, "Tools", "data"))) names.Add(asset.Name);
            return names;
        }

        public static IEnumerable<Asset> Group(string group, string data)
        {
            switch (group)
            {
                case "Notes": return Notes.All(data, inset: true);
                case "NotesV2": return Notes.All(data, inset: false);
                case "HUD": return Hud.All(data);
                case "Effects": return Effects.All(data);
                case "Backgrounds": return Backgrounds.All(data);
                default: throw new ArgumentException("Unknown group: " + group);
            }
        }

        private static bool Matches(string name, string pattern)
        {
            if (pattern.IndexOf('*') < 0) return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
            string regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
