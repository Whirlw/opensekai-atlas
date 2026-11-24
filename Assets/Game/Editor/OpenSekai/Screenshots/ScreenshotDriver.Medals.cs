using OpenSekai.App.Library;
using OpenSekai.App.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        private static readonly string[] _medalColors = { "#526DC8", "#6ED6C0", "#B692FF", "#FFC670", "#FF8CBA" };
        private static readonly string[] _medalGrades = { null, "III", "VIII", "α", "ω" };

        /// <summary>
        /// Every medal tier in a row: the 段位 plate, another kind's, the 段位 plate not earned yet, the
        /// lists' size and the home card's chips; under them, every grade, and the chips empty, with long texts in full and fitted.
        /// </summary>
        private static GameObject BuildMedalGallery()
        {
            var host = new GameObject("[Medal gallery]", typeof(RectTransform));
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)host.transform;
            Image ground = LibraryUi.CreateImage("Ground", root, HomeTheme.Colors.Card);
            LibraryUi.Stretch(ground.rectTransform);

            for (int tier = MedalTiers.LOWEST; tier <= MedalTiers.TOP; tier++)
            {
                Color32 color = DanStyles.ParseColor(_medalColors[tier]);
                string grade = _medalGrades[tier];
                var dan = new MedalLook("三曲耐久", "star", null, color, tier, grade, true);
                var other = new MedalLook("万键之路", "trophy", "10K", color, tier, grade, false);
                float top = 34f + tier * 112f;
                TextMeshProUGUI name = LibraryWidgets.Label("Name" + tier, root, MedalCopy.Tier(tier), 22f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold);
                LibraryWidgets.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, -(top + 38f)), new Vector2(80f, 34f));
                AddMedal(root, dan, 150f, top, new Vector2(488f, 76f), false);
                AddMedal(root, other, 700f, top, new Vector2(400f, 76f), false);
                AddMedal(root, dan, 1160f, top, new Vector2(330f, 76f), true);
                AddMedal(root, dan, 1510f, top + 8f, new Vector2(258f, 60f), false);
                AddChip(root, dan, 1800f, top + 20f);
                AddChip(root, other, 1974f, top + 20f);
            }

            for (int i = 0; i < MedalGrades.All.Count; i++)
            {
                var look = new MedalLook("等级", "diamond", null, DanStyles.ParseColor(_medalColors[i % _medalColors.Length]),
                    MedalTiers.LOWEST + 1, MedalGrades.All[i], true);
                AddMedal(root, look, 40f + i % 12 * 172f, 612f + i / 12 * 86f, new Vector2(160f, 60f), false);
            }

            Color32 tint = DanStyles.ParseColor(_medalColors[2]);
            var graded = new MedalLook("Long Endurance X", "star", null, tint, MedalTiers.TOP, "VIII", true);
            var wide = new MedalLook("十六个字的段位名称可以写到这么长了", "star", null, tint, MedalTiers.LOWEST, null, true);
            AddChip(root, null, 40f, 900f);
            AddChip(root, graded, 220f, 900f);
            AddChip(root, wide, 560f, 900f);
            AddChip(root, graded, 960f, 900f, 200f);
            AddChip(root, wide, 1184f, 900f, 200f);
            return host;
        }

        private static void AddChip(RectTransform root, MedalLook look, float left, float top, float width = 0f)
        {
            var chip = new MedalChip(root, "Chip");
            if (look != null) chip.SetLook(look);
            else chip.SetEmpty("未佩戴奖牌");
            if (width > 0f) chip.SetWidth(width);
            LibraryWidgets.Place(chip.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top), chip.Root.sizeDelta);
        }

        private static void AddMedal(RectTransform root, MedalLook look, float left, float top, Vector2 size, bool locked)
        {
            var plate = new MedalPlate(root, "Medal", size, shadow: false);
            LibraryWidgets.Place(plate.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top), size);
            plate.SetLook(look);
            plate.SetLocked(locked);
        }
    }
}
