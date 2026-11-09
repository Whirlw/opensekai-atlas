using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Lightweight end-of-session panel: one snapshot, two actions. It deliberately replaces the
    /// old result演出-only flow and never talks to the chart editor.
    /// </summary>
    public sealed class LibraryResultPanel : MonoBehaviour
    {
        private const float PANEL_WIDTH = 920f;
        private const float PANEL_HEIGHT = 600f;

        private static readonly Color32 OverlayColor = new Color32(0, 0, 0, 175);
        private static readonly Color32 PanelColor = new Color32(26, 32, 40, 255);
        private static readonly Color32 AccentColor = new Color32(120, 200, 255, 255);

        private Action<PlayResult> _onRetry;
        private PlayResult _current;

        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private TextMeshProUGUI _scoreText;
        private TextMeshProUGUI _rankText;
        private TextMeshProUGUI _statsText;
        private TextMeshProUGUI _judgementText;

        public static LibraryResultPanel Create(Transform parent, Action<PlayResult> _onRetry)
        {
            RectTransform root = LibraryScreen.CreateRect("ResultPanel", parent);
            LibraryScreen.Stretch(root);
            LibraryResultPanel panel = root.gameObject.AddComponent<LibraryResultPanel>();
            panel._onRetry = _onRetry;
            panel.Build();
            panel.Hide();
            return panel;
        }

        public void Show(PlayResult result)
        {
            if (result == null)
            {
                Hide();
                return;
            }

            _current = result;
            Refresh(result);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Build()
        {
            Image overlay = LibraryScreen.CreateImage("Overlay", transform, OverlayColor);
            LibraryScreen.Stretch(overlay.rectTransform);

            RectTransform panel = LibraryScreen.CreateRect("Panel", transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            _titleText = LibraryScreen.CreateText("Song", panel, string.Empty, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleText.rectTransform.offsetMin = new Vector2(40f, -96f);
            _titleText.rectTransform.offsetMax = new Vector2(-40f, -36f);
            _titleText.textWrappingMode = TextWrappingModes.NoWrap;
            _titleText.overflowMode = TextOverflowModes.Ellipsis;

            _subtitleText = LibraryScreen.CreateText("Subtitle", panel, string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            _subtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _subtitleText.rectTransform.offsetMin = new Vector2(40f, -132f);
            _subtitleText.rectTransform.offsetMax = new Vector2(-40f, -96f);
            _subtitleText.color = new Color32(168, 182, 198, 255);

            _scoreText = LibraryScreen.CreateText("Score", panel, "0", 92f, FontStyles.Bold, TextAlignmentOptions.Center);
            _scoreText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _scoreText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _scoreText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _scoreText.rectTransform.offsetMin = new Vector2(40f, -260f);
            _scoreText.rectTransform.offsetMax = new Vector2(-40f, -140f);

            _rankText = LibraryScreen.CreateText("Rank", panel, string.Empty, 44f, FontStyles.Bold, TextAlignmentOptions.Center);
            _rankText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _rankText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _rankText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _rankText.rectTransform.offsetMin = new Vector2(40f, -324f);
            _rankText.rectTransform.offsetMax = new Vector2(-40f, -268f);
            _rankText.color = AccentColor;

            _statsText = LibraryScreen.CreateText("Stats", panel, string.Empty, 26f, FontStyles.Normal, TextAlignmentOptions.Center);
            _statsText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _statsText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _statsText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _statsText.rectTransform.offsetMin = new Vector2(40f, -372f);
            _statsText.rectTransform.offsetMax = new Vector2(-40f, -328f);

            _judgementText = LibraryScreen.CreateText("Judgements", panel, string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            _judgementText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _judgementText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _judgementText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _judgementText.rectTransform.offsetMin = new Vector2(40f, -416f);
            _judgementText.rectTransform.offsetMax = new Vector2(-40f, -376f);
            _judgementText.color = new Color32(168, 182, 198, 255);

            Button retry = LibraryScreen.CreateButton("Retry", panel, "再玩一次", Retry, 240f, 64f);
            RectTransform retryRect = (RectTransform)retry.transform;
            retryRect.anchorMin = new Vector2(0.5f, 0f);
            retryRect.anchorMax = new Vector2(0.5f, 0f);
            retryRect.pivot = new Vector2(1f, 0f);
            retryRect.anchoredPosition = new Vector2(-16f, 40f);

            Button close = LibraryScreen.CreateButton("Back", panel, "返回曲库", Close, 240f, 64f);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0f, 0f);
            closeRect.anchoredPosition = new Vector2(16f, 40f);
        }

        private void Retry()
        {
            PlayResult result = _current;
            Hide();
            _onRetry?.Invoke(result);
        }

        private void Close()
        {
            Hide();
        }

        private void Refresh(PlayResult result)
        {
            _titleText.text = result.Title;
            _subtitleText.text = (result.IsAuto ? "自动" : "手动")
                + (result.PlayLevel > 0 ? "  ·  Lv." + result.PlayLevel : string.Empty)
                + "  ·  " + result.KindText;

            bool hasScore = result.HasScore;
            _scoreText.gameObject.SetActive(hasScore);
            _rankText.gameObject.SetActive(hasScore);
            _judgementText.gameObject.SetActive(hasScore);

            if (!hasScore)
            {
                _statsText.text = result.Message;
                return;
            }

            _scoreText.text = result.Score.ToString("N0");
            _rankText.text = result.IsAllPerfectClear ? "AP" : result.Rank.ToString();

            if (result.IsAuto)
            {
                _statsText.text = "总音符  " + result.TotalCombo + "  ·  自动判定  " + result.AutoCount;
                _judgementText.text = "自动模式不计判定等级";
            }
            else
            {
                // The game only knows five judgement grades on screen: a just-perfect hit is still a
                // PERFECT. Showing a separate "JP" column invented a grade the original never had.
                _statsText.text = "最大连击  " + result.MaxCombo + " / " + result.TotalCombo;
                _judgementText.text = "PERFECT " + result.Perfect
                    + "    GREAT " + result.Great
                    + "    GOOD " + result.Good
                    + "    BAD " + result.Bad
                    + "    MISS " + result.Miss;
            }
        }
    }
}
