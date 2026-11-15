using System.Collections.Generic;
using System.IO;
using Sekai;
using Sekai.Live;
using Sekai.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.EditorTools
{
	/// <summary>
	/// Rebuilds the song info page (the <c>MusicInfo</c> canvas in <c>FrontUIView.prefab</c>) from
	/// code: sets the import settings of the sprites drawn by <c>ArtSource/Tools/render_musicinfo.ps1</c>,
	/// replaces the canvas's children with the generated layout and wires <see cref="MusicInfoView"/>.
	/// Layout is in canvas units (reference 1920 wide, height 872-1080 depending on the screen).
	/// </summary>
	public static class MusicInfoPageBuilder
	{
		private const string PrefabPath = "Assets/Graphics/Resources/live/view/FrontUIView.prefab";
		private const string SpriteDir = "Assets/Graphics/Textures/MusicInfo";
		private const string BoldFontPath = "Assets/Graphics/Resources/font/SourceHanSansSC-Bold SDF_Base.asset";
		private const string MediumFontPath = "Assets/Graphics/Resources/font/SourceHanSansSC-Medium SDF_Base.asset";

		private const float BandHeight = 560f;
		private const float JacketSize = 440f;
		private const float FrameSize = 520f;
		private const float JacketX = -500f;
		private const float ColumnX = -210f;
		private const float ColumnWidth = 1020f;

		private static readonly Color Lavender = new Color(0.89f, 0.87f, 1f, 1f);
		private static readonly Color SoftLavender = new Color(0.76f, 0.74f, 0.95f, 1f);

		private static TMP_FontAsset _bold;
		private static TMP_FontAsset _medium;

		[MenuItem("OpenSekai/Art/Rebuild Music Info Page")]
		public static void Rebuild()
		{
			ConfigureSprites();
			_bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
			_medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumFontPath);

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

		private static void ConfigureSprites()
		{
			AssetDatabase.Refresh();
			foreach (string file in Directory.GetFiles(SpriteDir, "*.png"))
			{
				string path = file.Replace('\\', '/');
				TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
				importer.textureType = TextureImporterType.Sprite;
				importer.spriteImportMode = SpriteImportMode.Single;
				importer.spritePixelsPerUnit = 100f;
				importer.mipmapEnabled = false;
				importer.alphaIsTransparency = true;
				importer.wrapMode = TextureWrapMode.Clamp;
				importer.filterMode = FilterMode.Bilinear;
				// Long dark gradients band under ASTC; these are small, keep them exact.
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				string name = Path.GetFileNameWithoutExtension(path);
				importer.spriteBorder = name.StartsWith("mi_tag") ? new Vector4(22f, 22f, 22f, 22f) : Vector4.zero;
				importer.SaveAndReimport();
			}
		}

		private static Sprite LoadSprite(string name)
		{
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");
			if (sprite == null)
			{
				throw new FileNotFoundException("Missing sprite " + name + " (run ArtSource/Tools/render_musicinfo.ps1)");
			}
			return sprite;
		}

		private static void Build(MusicInfoView view)
		{
			Transform root = view.transform;
			for (int i = root.childCount - 1; i >= 0; i--)
			{
				Object.DestroyImmediate(root.GetChild(i).gameObject);
			}

			// Whole page: a light veil over the field plus the band, faded as one.
			RectTransform viewRoot = Node("ViewRoot", root);
			Stretch(viewRoot);
			CanvasGroup viewGroup = viewRoot.gameObject.AddComponent<CanvasGroup>();
			viewGroup.alpha = 0f;
			viewGroup.blocksRaycasts = false;
			viewGroup.interactable = false;

			Image veil = AddImage(Node("Veil", viewRoot), null, new Color(0.03f, 0.03f, 0.1f, 0.5f));
			Stretch(veil.rectTransform);

			RectTransform band = Node("Band", viewRoot);
			band.anchorMin = new Vector2(0f, 0.5f);
			band.anchorMax = new Vector2(1f, 0.5f);
			band.sizeDelta = new Vector2(0f, BandHeight);
			Stretch(AddImage(Node("Fill", band), LoadSprite("mi_band"), Color.white).rectTransform);

			RectTransform edgeTop = EdgeRule("EdgeTop", band, BandHeight * 0.5f - 6f, 0f);
			RectTransform edgeBottom = EdgeRule("EdgeBottom", band, -BandHeight * 0.5f + 6f, 1f);

			// Jacket: glow, bevel-masked art with a light sweep, gradient frame, sparkles.
			RectTransform jacketGroupRect = Node("JacketGroup", viewRoot);
			Place(jacketGroupRect, new Vector2(JacketX, 0f), new Vector2(JacketSize, JacketSize));
			CanvasGroup jacketGroup = jacketGroupRect.gameObject.AddComponent<CanvasGroup>();
			jacketGroup.blocksRaycasts = false;

			Image glow = AddImage(Node("Glow", jacketGroupRect), LoadSprite("mi_glow"), new Color(1f, 1f, 1f, 0.7f));
			Place(glow.rectTransform, Vector2.zero, new Vector2(860f, 860f));

			Image maskImage = AddImage(Node("JacketMask", jacketGroupRect), LoadSprite("mi_jacket_mask"), new Color(0.07f, 0.06f, 0.16f, 1f));
			Place(maskImage.rectTransform, Vector2.zero, new Vector2(JacketSize, JacketSize));
			maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = true;

			RectTransform jacketRect = Node("Jacket", maskImage.rectTransform);
			Stretch(jacketRect);
			CustomRawImage jacket = jacketRect.gameObject.AddComponent<CustomRawImage>();
			jacket.raycastTarget = false;

			Image shine = AddImage(Node("Shine", maskImage.rectTransform), LoadSprite("mi_shine"), new Color(1f, 1f, 1f, 0.75f));
			Place(shine.rectTransform, new Vector2(-430f, 0f), new Vector2(256f, 640f));

			Image frame = AddImage(Node("Frame", jacketGroupRect), LoadSprite("mi_frame"), Color.white);
			Place(frame.rectTransform, Vector2.zero, new Vector2(FrameSize, FrameSize));

			var sparkles = new List<RectTransform>
			{
				Sparkle(jacketGroupRect, new Vector2(-172f, 296f), 64f, new Color(1f, 0.86f, 0.95f, 1f)),
				Sparkle(jacketGroupRect, new Vector2(172f, -296f), 52f, new Color(0.82f, 0.95f, 1f, 1f)),
				Sparkle(jacketGroupRect, new Vector2(226f, 266f), 30f, new Color(0.85f, 1f, 0.95f, 1f)),
				Sparkle(jacketGroupRect, new Vector2(-284f, -150f), 24f, new Color(0.9f, 0.85f, 1f, 1f)),
			};

			// Text column, left-aligned at ColumnX; rows are positioned from its origin.
			RectTransform column = Node("TextColumn", viewRoot);
			Place(column, new Vector2(ColumnX, 0f), new Vector2(ColumnWidth, 0f));
			column.pivot = new Vector2(0f, 0.5f);

			// Row 0: difficulty plate and the collaboration ribbon.
			RectTransform tagRow = Row("TagRow", column, 156f, 52f);
			RectTransform diffRect = Node("Difficulty", tagRow);
			LeftAt(diffRect, 0f, new Vector2(224f, 52f));
			UIPartsMusicDifficulty difficulty = diffRect.gameObject.AddComponent<UIPartsMusicDifficulty>();
			CustomImage defaultPlate = AddCustomImage(Node("Default", diffRect), LoadSprite("mi_tag"), Color.white);
			defaultPlate.type = Image.Type.Sliced;
			Stretch(defaultPlate.rectTransform);
			CustomImage appendPlate = AddCustomImage(Node("Append", diffRect), LoadSprite("mi_tag_append"), Color.white);
			appendPlate.type = Image.Type.Sliced;
			Stretch(appendPlate.rectTransform);
			appendPlate.gameObject.SetActive(false);
			CustomTextMesh diffText = AddText(Node("Label", diffRect), _bold, 30f, Color.white, TextAlignmentOptions.Center, 6f);
			Stretch(diffText.rectTransform);
			diffText.fontSizeMin = 18f;
			diffText.margin = new Vector4(10f, 0f, 10f, 0f);
			SetField(difficulty, "_defaultImage", defaultPlate);
			SetField(difficulty, "_appendObject", appendPlate.gameObject);

			RectTransform ribbonRect = Node("CollaborationRibbon", tagRow);
			LeftAt(ribbonRect, 248f, new Vector2(400f, 44f));
			UIPartsMusicCollaborationRibbon ribbon = ribbonRect.gameObject.AddComponent<UIPartsMusicCollaborationRibbon>();
			CustomTextMesh ribbonText = AddText(Node("Text", ribbonRect), _medium, 28f, Lavender, TextAlignmentOptions.Left, 2f);
			Stretch(ribbonText.rectTransform);
			ribbonText.enableAutoSizing = false;
			SetField(ribbon, "_collaborationText", ribbonText);
			SetField(ribbon, "_collaborationTextPadding", 4f);
			SetField(ribbon, "_widthMax", ColumnWidth - 248f);

			// Row 1: title. Row 2: credits. Row 3: chart credits.
			RectTransform titleRow = Row("TitleRow", column, 76f, 92f);
			CustomTextMesh title = AddText(Node("Title", titleRow), _bold, 68f, Color.white, TextAlignmentOptions.Left, 4f);
			Stretch(title.rectTransform);
			title.fontSizeMin = 34f;
			title.textWrappingMode = TextWrappingModes.NoWrap;
			title.overflowMode = TextOverflowModes.Ellipsis;

			Image rule = AddImage(Node("Rule", titleRow), LoadSprite("mi_rule"), Color.white);
			RectTransform ruleRect = rule.rectTransform;
			ruleRect.anchorMin = ruleRect.anchorMax = new Vector2(0f, 0.5f);
			ruleRect.pivot = new Vector2(0f, 0.5f);
			ruleRect.anchoredPosition = new Vector2(-12f, -56f);
			ruleRect.sizeDelta = new Vector2(ColumnWidth + 24f, 24f);

			RectTransform musicInfo = Row("MusicInfo", column, -38f, 92f);
			Icon(musicInfo, "mi_icon_song", 22f);
			CustomTextMesh creator = AddText(Node("Creator", musicInfo), _medium, 30f, Lavender, TextAlignmentOptions.Left, 1f);
			TextLine(creator.rectTransform, 22f, 40f);
			CustomTextMesh singer = AddText(Node("Singer", musicInfo), _medium, 28f, SoftLavender, TextAlignmentOptions.Left, 1f);
			TextLine(singer.rectTransform, -22f, 38f);

			RectTransform scoreInfo = Row("ScoreInfo", column, -144f, 84f);
			GameObject scoreIcon = Icon(scoreInfo, "mi_icon_chart", 20f);
			CustomTextMesh scoreTitle = AddText(Node("ScoreTitle", scoreInfo), _bold, 30f, Color.white, TextAlignmentOptions.Left, 2f);
			TextLine(scoreTitle.rectTransform, 20f, 40f);
			CustomTextMesh scoreCreator = AddText(Node("ScoreCreator", scoreInfo), _medium, 26f, SoftLavender, TextAlignmentOptions.Left, 1f);
			TextLine(scoreCreator.rectTransform, -20f, 34f);

			SetField(view, "_viewGroup", viewGroup);
			SetField(view, "_band", band);
			SetArray(view, "_bandEdges", new Object[] { edgeTop, edgeBottom });
			SetField(view, "_jacketGroup", jacketGroup);
			SetField(view, "_shine", shine.rectTransform);
			SetField(view, "_rule", ruleRect);
			SetArray(view, "_sparkles", sparkles.ToArray());
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
			SetField(view, "_scoreTitleLabel", scoreTitle);
			SetField(view, "_scoreCreatorLabel", scoreCreator);
			SetField(view, "_scoreIcon", scoreIcon);
			SetField(view, "_JacketFadeOutDelaySec", 2.35f);
		}

		// ------------------------------------------------------------------ nodes

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

		/// <summary>A text line indented past the row's icon, centred on <paramref name="y"/>.</summary>
		private static void TextLine(RectTransform rect, float y, float height)
		{
			rect.anchorMin = new Vector2(0f, 0.5f);
			rect.anchorMax = new Vector2(1f, 0.5f);
			rect.pivot = new Vector2(0f, 0.5f);
			rect.anchoredPosition = new Vector2(52f, y);
			rect.sizeDelta = new Vector2(-52f, height);
		}

		private static GameObject Icon(RectTransform row, string sprite, float y)
		{
			Image icon = AddImage(Node("Icon", row), LoadSprite(sprite), Lavender);
			LeftAt(icon.rectTransform, 2f, new Vector2(36f, 36f));
			icon.rectTransform.anchoredPosition = new Vector2(2f, y);
			return icon.gameObject;
		}

		private static RectTransform EdgeRule(string name, RectTransform band, float y, float pivotX)
		{
			Image rule = AddImage(Node(name, band), LoadSprite("mi_rule"), new Color(1f, 1f, 1f, 0.85f));
			RectTransform rect = rule.rectTransform;
			rect.anchorMin = new Vector2(0f, 0.5f);
			rect.anchorMax = new Vector2(1f, 0.5f);
			rect.pivot = new Vector2(pivotX, 0.5f);
			rect.anchoredPosition = new Vector2(0f, y);
			rect.sizeDelta = new Vector2(0f, 24f);
			return rect;
		}

		private static RectTransform Sparkle(RectTransform parent, Vector2 position, float size, Color color)
		{
			// Half the tint's saturation, like the rest of the page's pastel hues (MusicInfoArt.cs).
			Color.RGBToHSV(color, out float h, out float sat, out float v);
			color = Color.HSVToRGB(h, sat * 0.5f, v);
			Image image = AddImage(Node("Sparkle", parent), LoadSprite("mi_sparkle"), color);
			Place(image.rectTransform, position, new Vector2(size, size));
			return image.rectTransform;
		}

		private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
		{
			Image image = rect.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.color = color;
			image.raycastTarget = false;
			return image;
		}

		private static CustomImage AddCustomImage(RectTransform rect, Sprite sprite, Color color)
		{
			CustomImage image = rect.gameObject.AddComponent<CustomImage>();
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
