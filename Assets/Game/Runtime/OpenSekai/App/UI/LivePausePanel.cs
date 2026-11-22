using System;
using OpenSekai.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The pause card of a live started from the library, in the library's look: the jacket and
    /// title in the header with the difficulty, level and charter under it, two action rows for
    /// 重新开始 and 返回曲库, and a wide accent 继续 button. Both rows ask for a second tap — the
    /// row turns to its warning color and says so — so a stray tap cannot throw the run away.
    /// The scrim is inert: the live only goes on through 继续.
    /// </summary>
    internal sealed class LivePausePanel : MonoBehaviour, ILivePauseMenu
    {
        private const float CARD_WIDTH = 780f;
        private const float CARD_HEIGHT = 492f;
        private const float COVER_SIZE = 64f;
        private const float RESUME_HEIGHT = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT;
        private const float STATUS_WIDTH = 140f;

        private LibraryModal _modal;
        private RawImage _cover;
        private Image _coverPlaceholder;
        private Image _resumeBackground;
        private LibraryActionRow _retry;
        private LibraryActionRow _quit;

        private Action _onResume;
        private Action _onRetry;
        private Action _onQuit;

        public bool IsVisible => _modal != null && _modal.IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            LivePauseMenus.Factory = Create;
        }

        internal static ILivePauseMenu Create(Transform parent)
        {
            GameObject go = new GameObject("LivePausePanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LivePausePanel panel = go.AddComponent<LivePausePanel>();
            panel.Build();
            return panel;
        }

        public void Show(LivePauseInfo info, Action resume, Action retry, Action quit)
        {
            info ??= new LivePauseInfo();
            _onResume = resume;
            _onRetry = retry;
            _onQuit = quit;

            _modal.Title.text = string.IsNullOrWhiteSpace(info.Title) ? "未命名曲目" : info.Title;
            _modal.Subtitle.text = Subtitle(info);
            ApplyCourse();
            _cover.texture = info.Jacket;
            _cover.enabled = info.Jacket != null;
            _coverPlaceholder.enabled = info.Jacket == null;

            Color32 accent = info.Accent;
            accent.a = 255;
            _resumeBackground.color = accent;

            _retry.SetArmed(false, false);
            _quit.SetArmed(false, false);
            _modal.Open();
        }

        public void Hide()
        {
            _onResume = null;
            _onRetry = null;
            _onQuit = null;
            _retry?.SetArmed(false, false);
            _quit?.SetArmed(false, false);
            _modal?.Close();
        }

        /// <summary>In a 段位 course retry restarts the whole course and quitting gives it up; the rows say so.</summary>
        private void ApplyCourse()
        {
            OpenSekai.App.Playback.DanChallengeService course = OpenSekai.App.Playback.DanChallengeService.Local;
            bool inCourse = course.Phase == OpenSekai.App.Playback.DanPhase.Playing && course.Definition != null;
            if (!inCourse)
            {
                _retry.SetText("重新开始", "从头再打一次", "再点一次重新开始", "这一局的进度会清空");
                _quit.SetText("返回曲库", "结束这一局，回到曲库", "再点一次返回曲库", "这一局会按中途退出结算");
                return;
            }

            _retry.SetText("从第一首重来", "整场段位重新开始，生命回满", "再点一次从第一首重来", "这一场会记为未完成");
            _quit.SetText("放弃挑战", "结束这场段位，查看结算", "再点一次放弃挑战", "这一场会记为未完成，不授予段位牌");
            string line = "段位「" + course.Definition.Label + "」第 " + course.SongNumber + " / " + course.Definition.Songs.Count + " 首";
            _modal.Subtitle.text = string.IsNullOrEmpty(_modal.Subtitle.text) ? line : line + "  ·  " + _modal.Subtitle.text;
        }

        private void OnDestroy()
        {
            _retry?.Dispose();
            _quit?.Dispose();
            _modal?.Dispose();
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            gameObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)transform;
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), string.Empty, null, withSubtitle: true);

            // Header: the jacket in front of the title, as on the package action card.
            float padding = LibraryTheme.Layout.MODAL_PADDING;
            float header = LibraryTheme.Layout.MODAL_HEADER_HEIGHT;
            RectTransform coverFrame = LibraryUi.CreateRect("Cover", _modal.Card);
            LibraryWidgets.Place(coverFrame, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(padding, -header * 0.5f - 2f), new Vector2(COVER_SIZE, COVER_SIZE));
            Image frame = LibraryWidgets.Panel("Frame", coverFrame, LibraryTheme.Radius.COVER_SMALL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _coverPlaceholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 28f);
            GameObject jacket = new GameObject("Jacket", typeof(RectTransform), typeof(RawImage));
            jacket.transform.SetParent(frame.transform, false);
            _cover = jacket.GetComponent<RawImage>();
            _cover.raycastTarget = false;
            LibraryUi.Stretch(_cover.rectTransform);

            float textLeft = padding + COVER_SIZE + 20f;
            float textRight = padding + STATUS_WIDTH + 12f;
            SetHorizontalInsets(_modal.Title.rectTransform, textLeft, textRight);
            SetHorizontalInsets(_modal.Subtitle.rectTransform, textLeft, textRight);
            _modal.Title.fontSize = 30f;
            _modal.Subtitle.richText = true;

            TextMeshProUGUI status = LibraryWidgets.Label(
                "Status", _modal.Card, "PAUSED", 22f, LibraryTheme.Colors.TextMuted, FontStyles.Bold, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(status);
            status.characterSpacing = 6f;
            LibraryWidgets.Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-padding, -header * 0.5f), new Vector2(STATUS_WIDTH, 32f));

            RectTransform body = _modal.Body;
            body.offsetMax = new Vector2(body.offsetMax.x, -header - 8f);

            _retry = new LibraryActionRow(body, 0, "Retry", LibraryIcon.Refresh,
                "重新开始", "从头再打一次",
                "再点一次重新开始", "这一局的进度会清空",
                LibraryTheme.Colors.Warning, LibraryTheme.Colors.TextPrimary, OnRetryClicked);
            _quit = new LibraryActionRow(body, 1, "Quit", LibraryIcon.Back,
                "返回曲库", "结束这一局，回到曲库",
                "再点一次返回曲库", "这一局会按中途退出结算",
                LibraryTheme.Colors.Danger, LibraryTheme.Colors.TextPrimary, OnQuitClicked);

            Button resume = LibraryWidgets.PillButton(
                "Resume", body, "继续", OnResumeClicked, 0f, RESUME_HEIGHT,
                LibraryTheme.Colors.DefaultAccent, LibraryTheme.Colors.TextOnAccent, 28f, FontStyles.Bold, UiCue.Forward);
            LibraryWidgets.BottomStrip((RectTransform)resume.transform, 0f, 0f, 0f, RESUME_HEIGHT);
            _resumeBackground = (Image)resume.targetGraphic;
            TextMeshProUGUI resumeLabel = resume.GetComponentInChildren<TextMeshProUGUI>();
            LibraryWidgets.Inset(resumeLabel.rectTransform, 40f, 0f, 0f, 0f);
            Image resumeIcon = LibraryWidgets.Icon(resume.transform, LibraryIcon.Play, LibraryTheme.Colors.TextOnAccent, 26f);
            resumeIcon.rectTransform.anchoredPosition = new Vector2(-40f, 0f);

            _modal.Close();
        }

        private void OnResumeClicked()
        {
            Action action = _onResume;
            Hide();
            action?.Invoke();
        }

        private void OnRetryClicked()
        {
            if (!_retry.Armed)
            {
                UiSound.Play(UiCue.Warning);
                _quit.SetArmed(false, false);
                _retry.SetArmed(true, true);
                return;
            }

            UiSound.Play(UiCue.Retry);
            Action action = _onRetry;
            Hide();
            action?.Invoke();
        }

        private void OnQuitClicked()
        {
            if (!_quit.Armed)
            {
                UiSound.Play(UiCue.Warning);
                _retry.SetArmed(false, false);
                _quit.SetArmed(true, true);
                return;
            }

            UiSound.Play(UiCue.Back);
            Action action = _onQuit;
            Hide();
            action?.Invoke();
        }

        /// <summary>"MASTER  LV 32  ·  谱面 X  ·  自动", the difficulty in its own color; parts the song lacks are left out.</summary>
        private static string Subtitle(LivePauseInfo info)
        {
            string text = string.Empty;
            if (!string.IsNullOrWhiteSpace(info.Difficulty))
            {
                string color = ColorUtility.ToHtmlStringRGB(LibraryTheme.Colors.Difficulty(info.Difficulty));
                text = "<color=#" + color + ">" + info.Difficulty.ToUpperInvariant() + "</color>";
            }
            if (info.Level > 0)
            {
                text += (text.Length > 0 ? "  " : string.Empty) + "LV " + info.Level;
            }
            if (!string.IsNullOrWhiteSpace(info.Charter))
            {
                text += (text.Length > 0 ? "  ·  " : string.Empty) + "谱面 " + info.Charter.Trim();
            }
            if (info.IsAuto)
            {
                text += (text.Length > 0 ? "  ·  " : string.Empty) + "自动";
            }
            return text;
        }

        private static void SetHorizontalInsets(RectTransform rect, float left, float right)
        {
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
        }
    }
}
