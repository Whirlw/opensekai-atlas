using System;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 成就奖励: the medal and avatar one goal gives, drawn large side by side with what each is, and
    /// what it takes to get them or when they were received.
    /// </summary>
    internal sealed class AchievementRewardPanel : IDisposable
    {
        private const float WIDTH = 1080f;
        private const float HEIGHT = 640f;
        private const float PAD = 40f;
        private const float FOOTER = 84f;
        private const float AVATAR = 220f;
        private const float COLUMN_GAP = 48f;

        private static readonly Vector2 _medalSize = new Vector2(640f, 136f);

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly Item _medalItem;
        private readonly Item _avatarItem;
        private readonly MedalPlate _medal;
        private readonly ProfileAvatarView _avatar;
        private readonly Image _footer;
        private readonly Image _footerIcon;
        private readonly TextMeshProUGUI _state;

        internal AchievementRewardPanel(RectTransform layer)
        {
            _root = LibraryUi.CreateRect("AchievementReward", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(WIDTH, HEIGHT), "成就奖励", Close, withSubtitle: true);
            RectTransform stage = LibraryUi.CreateRect("Stage", _modal.Body);
            LibraryWidgets.Inset(stage, 0f, FOOTER + 16f, 0f, 0f);
            _medalItem = new Item(stage, "MedalItem");
            _medal = new MedalPlate(_medalItem.Picture, "RewardMedal", _medalSize, glow: true);
            _avatarItem = new Item(stage, "AvatarItem");
            _avatar = new ProfileAvatarView(_avatarItem.Picture, "RewardAvatar", AVATAR);

            _footer = LibraryWidgets.Panel("Footer", _modal.Body, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryWidgets.BottomStrip(_footer.rectTransform, 0f, 0f, 0f, FOOTER);
            _footerIcon = LibraryWidgets.Icon(_footer.transform, LibraryIcon.Lock, LibraryTheme.Colors.TextMuted, 28f);
            LibraryWidgets.Place(_footerIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(28f, 28f));
            _state = LibraryWidgets.Label("RewardState", _footer.transform, string.Empty, 21f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.Inset(_state.rectTransform, 72f, 0f, 24f, 0f);
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(AchievementProgress progress)
        {
            AchievementLook look = AchievementCopy.For(progress);
            bool medal = progress.Goal.GivesMedal;
            bool avatar = progress.Goal.AvatarId != null;
            AchievementState state = progress.State;
            bool unlocked = state == AchievementState.Unlocked;
            _modal.Subtitle.text = "「" + look.Title + "」  ·  " + look.Goal;

            // Side by side, centered together; alone, the medal takes its full size.
            float body = WIDTH - PAD * 2f;
            float medalWidth = avatar ? body - AVATAR - COLUMN_GAP : _medalSize.x;
            float left = avatar && medal ? -(medalWidth + COLUMN_GAP + AVATAR) * 0.5f : 0f;
            _medalItem.Root.gameObject.SetActive(medal);
            if (medal)
            {
                MedalLook plate = MedalLook.Of(look);
                _medal.SetLook(plate);
                _medal.SetLocked(false);
                _medalItem.Place(avatar ? left + medalWidth * 0.5f : 0f, medalWidth, Mathf.Min(1f, (medalWidth - 60f) / _medalSize.x));
                _medalItem.Bind("奖牌「" + (progress.Goal.MedalDisplayName ?? look.Title) + "」", MedalCopy.Tier(plate.Tier) + "奖牌，可以佩戴在个人信息页和首页");
                if (unlocked) _medal.Shine(0.25f);
            }

            _avatarItem.Root.gameObject.SetActive(avatar);
            if (avatar)
            {
                _avatar.SetAvatar(progress.Goal.AvatarId);
                _avatarItem.Place(medal ? -left - AVATAR * 0.5f : 0f, AVATAR + 40f, 1f);
                _avatarItem.Bind(progress.Goal.AvatarName ?? "专属头像", "在编辑资料里选用");
            }

            _footer.color = unlocked || state == AchievementState.Claimable ? HomeTheme.Colors.WithAlpha(look.Tint, 30) : LibraryTheme.Colors.Control;
            _footerIcon.sprite = LibraryShapes.Icon(unlocked ? LibraryIcon.Check : state == AchievementState.Claimable ? LibraryIcon.Star : LibraryIcon.Lock);
            _footerIcon.color = state == AchievementState.InProgress ? LibraryTheme.Colors.TextMuted : look.Tint;
            _state.text = unlocked ? "已领取  ·  " + AchievementCopy.When(progress.UnlockedAtUtc.Value)
                : state == AchievementState.Claimable ? "目标已达成，点击这项成就解锁后领取"
                : AchievementCopy.IsOneShot(progress) ? "尚未达成，解锁成就后领取"
                : "尚未达成（" + AchievementCopy.Count(progress) + "），解锁成就后领取";
            _modal.Open();
        }

        internal void Close() => _modal.Close();

        public void Dispose()
        {
            _medal.Dispose();
            _modal.Dispose();
        }

        /// <summary>One reward: its picture over its name and what it is for.</summary>
        private sealed class Item
        {
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _note;

            internal Item(RectTransform parent, string name)
            {
                Root = LibraryUi.CreateRect(name, parent);
                Root.anchorMin = new Vector2(0.5f, 0f);
                Root.anchorMax = new Vector2(0.5f, 1f);
                Picture = LibraryUi.CreateRect("Picture", Root);
                Vector2 center = new Vector2(0.5f, 0.5f);
                LibraryWidgets.Place(Picture, center, center, new Vector2(0f, 38f), Vector2.zero);
                _name = LibraryWidgets.Label("Name", Root, string.Empty, 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold,
                    TextAlignmentOptions.Center);
                LibraryWidgets.BottomStrip(_name.rectTransform, 0f, 0f, 32f, 36f);
                _note = LibraryWidgets.Label("Note", Root, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                    TextAlignmentOptions.Center);
                LibraryWidgets.BottomStrip(_note.rectTransform, 0f, 0f, 2f, 28f);
            }

            internal RectTransform Root { get; }

            internal RectTransform Picture { get; }

            internal void Place(float x, float width, float scale)
            {
                Root.sizeDelta = new Vector2(width, 0f);
                Root.anchoredPosition = new Vector2(x, 0f);
                Picture.localScale = new Vector3(scale, scale, 1f);
            }

            internal void Bind(string name, string note)
            {
                _name.text = name;
                _note.text = note;
            }
        }
    }
}
