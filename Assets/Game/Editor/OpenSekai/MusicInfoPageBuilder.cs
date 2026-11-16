using System.Collections.Generic;
using System.IO;
using Sekai;
using Sekai.Live;
using Sekai.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace OpenSekai.EditorTools
{
	/// <summary>
	/// Rebuilds the song info page (the <c>MusicInfo</c> canvas in <c>FrontUIView.prefab</c>) from
	/// code, in the library page's look (LibraryTheme): the dark ground with a glow in the jacket's
	/// color, a rounded jacket with a soft shadow and a hairline, a difficulty chip with the level,
	/// the title, an accent rule and captioned credit lines. It draws the shape sprites itself (the
	/// same math as LibraryShapes, at twice the canvas size), makes a condensed TMP font for the chip
	/// and level, replaces the canvas's children with the layout and wires <see cref="MusicInfoView"/>.
	/// Layout is in canvas units (reference 1920 wide, height 872-1080 depending on the screen).
	/// </summary>
	public static class MusicInfoPageBuilder
	{
		private const string PrefabPath = "Assets/Graphics/Resources/live/view/FrontUIView.prefab";
		private const string SpriteDir = "Assets/Graphics/Textures/MusicInfo";
		private const string BoldFontPath = "Assets/Graphics/Resources/font/SourceHanSansSC-Bold SDF_Base.asset";
		private const string MediumFontPath = "Assets/Graphics/Resources/font/SourceHanSansSC-Medium SDF_Base.asset";
		private const string CondensedSourcePath = "Assets/Game/Resources/Fonts/BarlowCondensed-SemiBold.ttf";
		private const string CondensedFontPath = "Assets/Graphics/Resources/font/BarlowCondensed-SemiBold SDF.asset";
		private const string CondensedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .-+/:·";

		// Shape sprites are drawn at twice the canvas size so corners stay sharp on a phone screen.
		private const int Scale = 2;
		private const float SpritePixelsPerUnit = 100f * Scale;

		private const float JacketSize = 460f;
		private const float JacketRadius = 28f;
		private const float JacketX = -470f;
		private const float ColumnX = -180f;
		private const float ColumnWidth = 1000f;
		private const float CaptionWidth = 76f;
		private const float ChipHeight = 42f;

		// LibraryTheme.Colors.
		private static readonly Color Ground = new Color32(12, 15, 20, 255);
		private static readonly Color CoverPlaceholder = new Color32(37, 44, 56, 255);
		private static readonly Color Hairline = new Color32(255, 255, 255, 20);
		private static readonly Color TextPrimary = new Color32(242, 244, 247, 255);
		private static readonly Color TextSecondary = new Color32(197, 206, 218, 255);
		private static readonly Color TextMuted = new Color32(154, 166, 181, 255);
		private static readonly Color Accent = new Color32(157, 184, 255, 255);
		private static readonly Color Glow = new Color32(37, 52, 92, 255);

		private static TMP_FontAsset _bold;
		private static TMP_FontAsset _medium;
		private static TMP_FontAsset _condensed;

		[MenuItem("OpenSekai/Art/Rebuild Music Info Page")]
		public static void Rebuild()
		{
			WriteSprites();
			ConfigureSprites();
			_bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
			_medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumFontPath);
			_condensed = LoadOrCreateCondensedFont() ?? _bold;

			GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
			try
			{
				MusicInfoView view = prefab.GetComponentInChildren<MusicInfoView>(true);
				if (view == null)
				{
					throw new System.InvalidOperationException("No MusicInfoView in " + PrefabPath);
				}
				Build(view);
				PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefab);
			}
			Debug.Log("[MusicInfoPageBuilder] rebuilt " + PrefabPath);
		}

		/// <summary>Batch-mode entry: <c>-executeMethod OpenSekai.EditorTools.MusicInfoPageBuilder.RebuildFromCommandLine</c>.</summary>
		public static void RebuildFromCommandLine()
		{
			try
			{
				Rebuild();
				EditorApplication.Exit(0);
			}
			catch (System.Exception e)
			{
				Debug.LogException(e);
				EditorApplication.Exit(1);
			}
		}

		// ------------------------------------------------------------------ sprites

		/// <summary>9-slice border of each generated sprite, in pixels.</summary>
		private static readonly Dictionary<string, int> Borders = new Dictionary<string, int>();

		private static void WriteSprites()
		{
			Directory.CreateDirectory(SpriteDir);
			Borders.Clear();
			WriteRounded("ml_round28", JacketRadius, 0f);
			WriteRounded("ml_ring28", JacketRadius, 1f);
			WriteRounded("ml_round8", 8f, 0f);
			WriteRounded("ml_ring8", 8f, 1.5f);
			WriteRounded("ml_round2", 2f, 0f);
			WriteShadow("ml_shadow28", JacketRadius, 40f);
			WriteGlow("ml_glow", 256);
		}

		private static void WriteRounded(string name, float radius, float stroke)
		{
			int r = Mathf.Max(1, Mathf.RoundToInt(radius * Scale));
			float s = stroke * Scale;
			int size = r * 2 + 2;
			var pixels = new Color32[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float px = x + 0.5f;
					float py = y + 0.5f;
					float cx = Mathf.Clamp(px, r, size - r);
					float cy = Mathf.Clamp(py, r, size - r);
					float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
					float alpha = Mathf.Clamp01(r - distance + 0.5f);
					if (s > 0f)
					{
						alpha *= Mathf.Clamp01(distance - (r - s) + 0.5f);
					}
					pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
				}
			}
			WritePng(name, size, size, pixels);
			Borders[name] = r;
		}

		private static void WriteShadow(string name, float radius, float blur)
		{
			int corner = Mathf.RoundToInt(radius * Scale);
			int spread = Mathf.RoundToInt(blur * Scale);
			int border = corner + spread;
			int size = border * 2 + 2;
			var pixels = new Color32[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = x + 0.5f - Mathf.Clamp(x + 0.5f, border, size - border);
					float dy = y + 0.5f - Mathf.Clamp(y + 0.5f, border, size - border);
					float distance = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dy * dy) - corner);
					float fade = 1f - Mathf.Clamp01(distance / spread);
					pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * fade * fade));
				}
			}
			WritePng(name, size, size, pixels);
			Borders[name] = border;
		}

		private static void WriteGlow(string name, int size)
		{
			var pixels = new Color32[size * size];
			float center = size * 0.5f;
			// A fixed seed keeps the dither identical between rebuilds.
			var random = new System.Random(20261116);
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = (x + 0.5f - center) / center;
					float dy = (y + 0.5f - center) / center;
					float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
					float alpha = falloff * falloff * (3f - 2f * falloff) * 255f;
					// Dither the 8-bit ramp: a dark gradient stretched over the screen bands without it.
					alpha += (float)random.NextDouble() - 0.5f;
					pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255));
				}
			}
			WritePng(name, size, size, pixels);
			Borders[name] = 0;
		}

		private static void WritePng(string name, int width, int height, Color32[] pixels)
		{
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
			try
			{
				texture.SetPixels32(pixels);
				texture.Apply(false);
				File.WriteAllBytes(SpriteDir + "/" + name + ".png", texture.EncodeToPNG());
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}
		}

		private static void ConfigureSprites()
		{
			AssetDatabase.Refresh();
			foreach (string file in Directory.GetFiles(SpriteDir, "ml_*.png"))
			{
				string path = file.Replace('\\', '/');
				string name = Path.GetFileNameWithoutExtension(path);
				TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
				importer.textureType = TextureImporterType.Sprite;
				importer.spriteImportMode = SpriteImportMode.Single;
				importer.spritePixelsPerUnit = name == "ml_glow" ? 100f : SpritePixelsPerUnit;
				importer.mipmapEnabled = false;
				importer.alphaIsTransparency = true;
				importer.wrapMode = TextureWrapMode.Clamp;
				importer.filterMode = FilterMode.Bilinear;
				// Soft alpha ramps band under block compression; these are small, keep them exact.
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				int border = Borders.TryGetValue(name, out int value) ? value : 0;
				importer.spriteBorder = new Vector4(border, border, border, border);
				importer.SaveAndReimport();
			}
		}

		private static Sprite LoadSprite(string name)
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");
			if (sprite == null)
			{
				throw new FileNotFoundException("Missing sprite " + name);
			}
			return sprite;
		}

		/// <summary>
		/// The library's condensed numeral face (Barlow Condensed SemiBold), as a static TMP asset
		/// holding Latin letters and digits, falling back to the bold CJK face.
		/// </summary>
		private static TMP_FontAsset LoadOrCreateCondensedFont()
		{
			TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CondensedFontPath);
			if (existing != null)
			{
				return existing;
			}

			Font source = AssetDatabase.LoadAssetAtPath<Font>(CondensedSourcePath);
			if (source == null)
			{
				Debug.LogWarning("[MusicInfoPageBuilder] " + CondensedSourcePath + " is missing; the chip uses the bold face.");
				return null;
			}

			TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
				source, 90, 9, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, false);
			asset.name = Path.GetFileNameWithoutExtension(CondensedFontPath);
			asset.TryAddCharacters(CondensedCharacters, out string _);
			if (_bold != null)
			{
				asset.fallbackFontAssetTable = new List<TMP_FontAsset> { _bold };
			}

			AssetDatabase.CreateAsset(asset, CondensedFontPath);
			Texture2D atlas = asset.atlasTextures[0];
			atlas.name = asset.name + " Atlas";
			AssetDatabase.AddObjectToAsset(atlas, asset);
			asset.material.name = asset.name + " Material";
			AssetDatabase.AddObjectToAsset(asset.material, asset);
			// Only these glyphs are needed, so freeze the atlas: nothing changes at play time.
			asset.atlasPopulationMode = AtlasPopulationMode.Static;
			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
			return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CondensedFontPath);
		}

		// ------------------------------------------------------------------ layout

		private static void Build(MusicInfoView view)
		{
			Transform root = view.transform;
			for (int i = root.childCount - 1; i >= 0; i--)
			{
				Object.DestroyImmediate(root.GetChild(i).gameObject);
			}

			// Whole page, faded as one.
			RectTransform viewRoot = Node("ViewRoot", root);
			Stretch(viewRoot);
			CanvasGroup viewGroup = viewRoot.gameObject.AddComponent<CanvasGroup>();
			viewGroup.alpha = 0f;
			viewGroup.blocksRaycasts = false;
			viewGroup.interactable = false;

			// The library's ground opens from the middle over the field, with two glows in the
			// jacket's colors: a strong one behind the jacket and a faint one under the text.
			RectTransform ground = Node("Ground", viewRoot);
			Stretch(ground);
			AddImage(ground, null, Ground);
			Image glowMain = AddImage(Node("GlowMain", ground), LoadSprite("ml_glow"), WithAlpha(Glow, 0.62f));
			Place(glowMain.rectTransform, new Vector2(JacketX + 40f, 40f), new Vector2(1700f, 1700f));
			Image glowSecondary = AddImage(Node("GlowSecondary", ground), LoadSprite("ml_glow"), WithAlpha(Accent, 0.08f));
			Place(glowSecondary.rectTransform, new Vector2(420f, -360f), new Vector2(1200f, 1200f));

			// Jacket: shadow, rounded mask, hairline.
			RectTransform jacketGroupRect = Node("JacketGroup", viewRoot);
			Place(jacketGroupRect, new Vector2(JacketX, 0f), new Vector2(JacketSize, JacketSize));
			CanvasGroup jacketGroup = jacketGroupRect.gameObject.AddComponent<CanvasGroup>();
			jacketGroup.blocksRaycasts = false;

			Image shadow = AddImage(Node("Shadow", jacketGroupRect), LoadSprite("ml_shadow28"), new Color(0f, 0f, 0f, 0.42f));
			shadow.type = Image.Type.Sliced;
			Place(shadow.rectTransform, new Vector2(0f, -8f), new Vector2(JacketSize + 80f, JacketSize + 80f));

			Image maskImage = AddImage(Node("JacketMask", jacketGroupRect), LoadSprite("ml_round28"), CoverPlaceholder);
			maskImage.type = Image.Type.Sliced;
			Place(maskImage.rectTransform, Vector2.zero, new Vector2(JacketSize, JacketSize));
			maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = true;

			RectTransform jacketRect = Node("Jacket", maskImage.rectTransform);
			Stretch(jacketRect);
			CustomRawImage jacket = jacketRect.gameObject.AddComponent<CustomRawImage>();
			jacket.raycastTarget = false;

			Image outline = AddImage(Node("Outline", jacketGroupRect), LoadSprite("ml_ring28"), Hairline);
			outline.type = Image.Type.Sliced;
			Place(outline.rectTransform, Vector2.zero, new Vector2(JacketSize, JacketSize));

			// Text column, left-aligned at ColumnX; rows are positioned from its origin.
			RectTransform column = Node("TextColumn", viewRoot);
			Place(column, new Vector2(ColumnX, 18f), new Vector2(ColumnWidth, 0f));
			column.pivot = new Vector2(0f, 0.5f);

			// Row 0: difficulty chip, level and the collaboration label.
			RectTransform tagRow = Row("TagRow", column, 160f, ChipHeight);
			RectTransform diffRect = Node("Difficulty", tagRow);
			LeftAt(diffRect, 0f, new Vector2(148f, ChipHeight));
			UIPartsMusicDifficulty difficulty = diffRect.gameObject.AddComponent<UIPartsMusicDifficulty>();
			Image chipFill = AddImage(Node("Fill", diffRect), LoadSprite("ml_round8"), WithAlpha(TextSecondary, 0.14f));
			chipFill.type = Image.Type.Sliced;
			Stretch(chipFill.rectTransform);
			Image chipRing = AddImage(Node("Ring", diffRect), LoadSprite("ml_ring8"), WithAlpha(TextSecondary, 0.5f));
			chipRing.type = Image.Type.Sliced;
			Stretch(chipRing.rectTransform);
			CustomTextMesh diffText = AddText(Node("Label", diffRect), _condensed, 28f, TextSecondary, TextAlignmentOptions.Center, 6f);
			Stretch(diffText.rectTransform);
			diffText.fontSizeMin = 18f;
			diffText.margin = new Vector4(12f, 0f, 12f, 0f);

			CustomTextMesh level = AddText(Node("Level", tagRow), _condensed, 30f, TextSecondary, TextAlignmentOptions.Left, 2f);
			LeftAt(level.rectTransform, 166f, new Vector2(120f, ChipHeight));
			level.enableAutoSizing = false;

			RectTransform ribbonRect = Node("CollaborationRibbon", tagRow);
			LeftAt(ribbonRect, 300f, new Vector2(400f, ChipHeight));
			UIPartsMusicCollaborationRibbon ribbon = ribbonRect.gameObject.AddComponent<UIPartsMusicCollaborationRibbon>();
			CustomTextMesh ribbonText = AddText(Node("Text", ribbonRect), _medium, 22f, TextMuted, TextAlignmentOptions.Left, 0f);
			Stretch(ribbonText.rectTransform);
			ribbonText.enableAutoSizing = false;
			SetField(ribbon, "_collaborationText", ribbonText);
			SetField(ribbon, "_collaborationTextPadding", 4f);
			SetField(ribbon, "_widthMax", ColumnWidth - 300f);

			// Row 1: title, with the accent rule under it.
			RectTransform titleRow = Row("TitleRow", column, 82f, 92f);
			CustomTextMesh title = AddText(Node("Title", titleRow), _bold, 64f, TextPrimary, TextAlignmentOptions.Left, 0f);
			Stretch(title.rectTransform);
			title.fontSizeMin = 34f;

			Image rule = AddImage(Node("Rule", titleRow), LoadSprite("ml_round2"), Accent);
			rule.type = Image.Type.Sliced;
			RectTransform ruleRect = rule.rectTransform;
			ruleRect.anchorMin = ruleRect.anchorMax = new Vector2(0f, 0.5f);
			ruleRect.pivot = new Vector2(0f, 0.5f);
			ruleRect.anchoredPosition = new Vector2(2f, -60f);
			ruleRect.sizeDelta = new Vector2(88f, 5f);

			// Row 2: credits. Row 3: chart credits. Captions in the muted color, values beside them.
			RectTransform musicInfo = Row("MusicInfo", column, -34f, 88f);
			Caption(musicInfo, "创作", 22f);
			CustomTextMesh creator = AddText(Node("Creator", musicInfo), _medium, 26f, TextSecondary, TextAlignmentOptions.Left, 0f);
			ValueLine(creator.rectTransform, 22f, 40f);
			Caption(musicInfo, "演唱", -22f);
			CustomTextMesh singer = AddText(Node("Singer", musicInfo), _medium, 26f, TextSecondary, TextAlignmentOptions.Left, 0f);
			ValueLine(singer.rectTransform, -22f, 40f);

			RectTransform scoreInfo = Row("ScoreInfo", column, -112f, 44f);
			GameObject scoreCaption = Caption(scoreInfo, "谱面", 0f);
			CustomTextMesh scoreCreator = AddText(Node("ScoreCreator", scoreInfo), _medium, 26f, TextSecondary, TextAlignmentOptions.Left, 0f);
			ValueLine(scoreCreator.rectTransform, 0f, 40f);

			SetField(view, "_viewGroup", viewGroup);
			SetField(view, "_band", ground);
			SetArray(view, "_bandEdges", new Object[0]);
			SetField(view, "_jacketGroup", jacketGroup);
			SetField(view, "_shine", null);
			SetField(view, "_rule", ruleRect);
			SetArray(view, "_sparkles", new Object[0]);
			SetField(view, "_textColumn", column);
			SetArray(view, "_infoGroup", new Object[]
			{
				tagRow.GetComponent<CanvasGroup>(), titleRow.GetComponent<CanvasGroup>(),
				musicInfo.GetComponent<CanvasGroup>(), scoreInfo.GetComponent<CanvasGroup>(),
			});
			SetField(view, "_jacket", jacket);
			SetField(view, "_difficultyJacket", difficulty);
			SetField(view, "_difficultyJacketText", diffText);
			SetField(view, "_titleLabel", title);
			SetField(view, "_creatorLabel", creator);
			SetField(view, "_singerLabel", singer);
			SetField(view, "_collaborationRibbon", ribbon);
			SetField(view, "_musicInfoRoot", musicInfo.gameObject);
			SetField(view, "_scoreInfoRoot", scoreInfo.gameObject);
			// The chart's own title repeats the song title nearly always; the page shows who charted it.
			SetField(view, "_scoreTitleLabel", null);
			SetField(view, "_scoreCreatorLabel", scoreCreator);
			SetField(view, "_scoreIcon", scoreCaption);
			SetField(view, "_levelLabel", level);
			SetArray(view, "_difficultyGraphics", new Object[] { chipFill, chipRing, diffText });
			SetArray(view, "_accentGraphics", new Object[] { rule, glowSecondary });
			SetArray(view, "_glowGraphics", new Object[] { glowMain });
			SetField(view, "_JacketFadeOutDelaySec", 2.35f);
		}

		// ------------------------------------------------------------------ nodes

		private static Color WithAlpha(Color color, float alpha)
		{
			color.a = alpha;
			return color;
		}

		private static RectTransform Node(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.layer = parent.gameObject.layer;
			var rect = (RectTransform)go.transform;
			rect.SetParent(parent, false);
			return rect;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
		}

		private static void Place(RectTransform rect, Vector2 position, Vector2 size)
		{
			rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
		}

		private static void LeftAt(RectTransform rect, float x, Vector2 size)
		{
			rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
			rect.pivot = new Vector2(0f, 0.5f);
			rect.anchoredPosition = new Vector2(x, 0f);
			rect.sizeDelta = size;
		}

		/// <summary>A full-width row of the text column with its own CanvasGroup (it fades and slides on its own).</summary>
		private static RectTransform Row(string name, RectTransform column, float y, float height)
		{
			RectTransform row = Node(name, column);
			row.anchorMin = new Vector2(0f, 0.5f);
			row.anchorMax = new Vector2(1f, 0.5f);
			row.pivot = new Vector2(0f, 0.5f);
			row.anchoredPosition = new Vector2(0f, y);
			row.sizeDelta = new Vector2(0f, height);
			CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
			group.blocksRaycasts = false;
			return row;
		}

		/// <summary>A muted caption at the start of a credit line, centred on <paramref name="y"/>.</summary>
		private static GameObject Caption(RectTransform row, string text, float y)
		{
			CustomTextMesh caption = AddText(Node("Caption", row), _medium, 22f, TextMuted, TextAlignmentOptions.Left, 0f);
			caption.enableAutoSizing = false;
			caption.text = text;
			LeftAt(caption.rectTransform, 0f, new Vector2(CaptionWidth, 40f));
			caption.rectTransform.anchoredPosition = new Vector2(0f, y);
			return caption.gameObject;
		}

		/// <summary>A credit value after its caption, centred on <paramref name="y"/>.</summary>
		private static void ValueLine(RectTransform rect, float y, float height)
		{
			rect.anchorMin = new Vector2(0f, 0.5f);
			rect.anchorMax = new Vector2(1f, 0.5f);
			rect.pivot = new Vector2(0f, 0.5f);
			rect.anchoredPosition = new Vector2(CaptionWidth, y);
			rect.sizeDelta = new Vector2(-CaptionWidth, height);
		}

		private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
		{
			Image image = rect.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.color = color;
			image.raycastTarget = false;
			return image;
		}

		private static CustomTextMesh AddText(RectTransform rect, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment, float spacing)
		{
			CustomTextMesh text = rect.gameObject.AddComponent<CustomTextMesh>();
			text.font = font;
			text.fontSize = size;
			text.enableAutoSizing = true;
			text.fontSizeMax = size;
			text.fontSizeMin = Mathf.Round(size * 0.6f);
			text.color = color;
			text.alignment = alignment;
			text.characterSpacing = spacing;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Ellipsis;
			text.raycastTarget = false;
			text.text = string.Empty;
			return text;
		}

		// ------------------------------------------------------------------ wiring

		private static void SetField(Object target, string field, Object value)
		{
			var so = new SerializedObject(target);
			SerializedProperty property = so.FindProperty(field) ?? throw new MissingFieldException(target.GetType().Name, field);
			property.objectReferenceValue = value;
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetField(Object target, string field, float value)
		{
			var so = new SerializedObject(target);
			SerializedProperty property = so.FindProperty(field) ?? throw new MissingFieldException(target.GetType().Name, field);
			property.floatValue = value;
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetArray(Object target, string field, Object[] values)
		{
			var so = new SerializedObject(target);
			SerializedProperty property = so.FindProperty(field) ?? throw new MissingFieldException(target.GetType().Name, field);
			property.arraySize = values.Length;
			for (int i = 0; i < values.Length; i++)
			{
				property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
			}
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		private sealed class MissingFieldException : System.Exception
		{
			public MissingFieldException(string type, string field) : base(type + "." + field + " not found") { }
		}
	}
}
