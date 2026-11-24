using System;
using System.Collections.Generic;
using System.Text;
using LitMotion;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 编辑资料: the nickname in a text field and the built-in avatars in a grid, with the choice
    /// previewed large on the left. Nothing is saved until 保存; an empty nickname cannot be saved.
    /// Avatars that achievements give stay dark with a lock until they are unlocked.
    /// An avatar id from a newer version or a backup stays as it is unless another one is picked.
    /// </summary>
    internal sealed class ProfileEditPanel : IDisposable
    {
        private const float TILE = 112f;
        private const int COLUMNS = 6;
        private const float LEFT_WIDTH = 300f;
        private const float BUTTON_WIDTH = 200f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly ProfileAvatarView _preview;
        private readonly TMP_InputField _nickname;
        private readonly TextMeshProUGUI _hint;
        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly TextMeshProUGUI _lockNote;
        private readonly Button _save;
        private readonly Func<string, string, bool> _onSave;
        private string _avatarId;

        /// <param name="onSave">Saves nickname and avatar id; true closes the card.</param>
        internal ProfileEditPanel(RectTransform layer, Func<string, string, bool> onSave)
        {
            _onSave = onSave;
            _root = LibraryUi.CreateRect("ProfileEdit", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(1240f, 740f), "编辑资料", Close, withSubtitle: true);
            _modal.Subtitle.text = "头像与昵称保存在本机，导出备份时一并带走";
            RectTransform body = _modal.Body;
            Vector2 topLeft = new Vector2(0f, 1f);

            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = new Vector2(0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 24f);
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _preview = new ProfileAvatarView(left, "Preview", 188f);
            LibraryWidgets.Place(_preview.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(188f, 188f));
            TextMeshProUGUI nameCaption = LibraryWidgets.Label("NameCaption", left, "昵称", 18f, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            LibraryWidgets.TopStrip(nameCaption.rectTransform, 6f, 0f, 228f, 26f);
            _nickname = LibraryWidgets.TextField("Nickname", left, 64f, "输入昵称", LibraryIcon.User, 22f);
            LibraryWidgets.TopStrip((RectTransform)_nickname.transform, 0f, 0f, 260f, 64f);
            _nickname.characterLimit = ProfileCopy.NICKNAME_LIMIT;
            _nickname.textComponent.richText = false;
            _nickname.onValidateInput = (text, index, character) => char.IsControl(character) ? '\0' : character;
            _nickname.onValueChanged.AddListener(_ => Validate());
            _nickname.onSubmit.AddListener(_ => Save());
            _hint = LibraryWidgets.Label("Hint", left, string.Empty, 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(_hint.rectTransform, 8f, 0f, 334f, 24f);

            RectTransform right = LibraryUi.CreateRect("Right", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + 48f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 24f, 0f, 0f);
            TextMeshProUGUI gridCaption = LibraryWidgets.Label("GridCaption", right, "选择头像", 18f, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            LibraryWidgets.TopStrip(gridCaption.rectTransform, 4f, 0f, 0f, 26f);
            RectTransform grid = LibraryUi.CreateRect("Grid", right);
            LibraryWidgets.Inset(grid, 0f, 0f, 0f, 40f);
            IReadOnlyList<ProfileAvatars.Avatar> avatars = ProfileAvatars.All;
            for (int i = 0; i < avatars.Count; i++)
            {
                string id = avatars[i].Id;
                int column = i % COLUMNS;
                int row = i / COLUMNS;
                RectTransform tile = LibraryUi.CreateRect("Tile" + id, grid);
                tile.anchorMin = new Vector2(column / (float)COLUMNS, 1f);
                tile.anchorMax = new Vector2((column + 1) / (float)COLUMNS, 1f);
                tile.pivot = new Vector2(0.5f, 1f);
                tile.sizeDelta = new Vector2(0f, TILE + 12f);
                tile.anchoredPosition = new Vector2(0f, -row * (TILE + 28f));
                Image ring = LibraryWidgets.Shape("Selected", tile, LibraryShapes.Ring((TILE + 12f) * 0.5f, 3f), Color.white);
                LibraryWidgets.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(TILE + 12f, TILE + 12f));
                var view = new ProfileAvatarView(tile, "Avatar", TILE, glow: false);
                LibraryWidgets.Place(view.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TILE, TILE));
                view.SetAvatar(id);
                Image hit = LibraryUi.CreateImage("Hit", tile, Color.clear);
                hit.raycastTarget = true;
                LibraryWidgets.Place(hit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(TILE + 12f, TILE + 12f));
                Button button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                var entry = new Tile(id, ring, view.Root);
                button.onClick.AddListener(() => Tap(entry));
                LibraryPressFeedback.Attach(hit.gameObject, view.Root);
                _tiles.Add(entry);
            }

            _lockNote = LibraryWidgets.Label("LockNote", body, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(_lockNote.rectTransform, 4f, (BUTTON_WIDTH + 16f) * 2f + 16f, 0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT);

            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _save = ServerWidgets.LabelledButton(body, "Save", "保存", LibraryIcon.Check, Save, BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_save.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero,
                new Vector2(BUTTON_WIDTH, height));
            Button cancel = LibraryWidgets.PillButton("Cancel", body, "取消", Close, BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)cancel.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-(BUTTON_WIDTH + 16f), 0f), new Vector2(BUTTON_WIDTH, height));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <param name="goals">Decide which of the avatars achievements give are unlocked.</param>
        internal void Open(ProfileAppearance current, IReadOnlyList<AchievementProgress> goals, Func<string, bool> ownsAvatar = null)
        {
            _nickname.SetTextWithoutNotify(current?.Nickname ?? string.Empty);
            _lockNote.text = string.Empty;
            foreach (Tile tile in _tiles) tile.Bind(goals, ownsAvatar);
            Choose(current?.AvatarId ?? ProfileAvatars.DEFAULT_ID, false);
            Validate();
            _modal.Open();
        }

        internal void Close()
        {
            _nickname.DeactivateInputField();
            _modal.Close();
        }

        public void Dispose() => _modal.Dispose();

        private void Choose(string id, bool sound)
        {
            _avatarId = id;
            _preview.SetAvatar(id);
            ProfileAvatars.Avatar avatar = ProfileAvatars.Find(id);
            foreach (Tile tile in _tiles)
            {
                bool chosen = string.Equals(tile.Id, id, StringComparison.Ordinal);
                tile.Ring.color = chosen ? (Color)avatar.Color : Color.clear;
            }

            if (sound)
            {
                UiSound.Play(UiCue.Select);
            }
        }

        private void Tap(Tile tile)
        {
            if (tile.Giver != null)
            {
                UiSound.Play(UiCue.Blocked);
                _lockNote.text = "解锁成就「" + AchievementCopy.For(tile.Giver).Title + "」后可用";
                return;
            }

            _lockNote.text = string.Empty;
            Choose(tile.Id, true);
        }

        private bool Validate()
        {
            string name = (_nickname.text ?? string.Empty).Trim();
            bool valid = name.Length > 0;
            _hint.text = valid ? name.Length + " / " + ProfileCopy.NICKNAME_LIMIT : "昵称不能为空";
            _hint.color = valid ? LibraryTheme.Colors.TextMuted : LibraryTheme.Colors.Danger;
            PackWidgets.SetEnabled(_save, valid);
            return valid;
        }

        private void Save()
        {
            if (!Validate())
            {
                UiSound.Play(UiCue.Blocked);
                return;
            }

            if (_onSave((_nickname.text ?? string.Empty).Trim(), _avatarId))
            {
                Close();
            }
        }

        /// <summary>One avatar in the grid.</summary>
        private sealed class Tile
        {
            private readonly CanvasGroup _group;
            private readonly Image _lock;

            internal Tile(string id, Image ring, RectTransform avatar)
            {
                Id = id;
                Ring = ring;
                _group = avatar.gameObject.AddComponent<CanvasGroup>();
                _group.blocksRaycasts = false;
                _lock = LibraryWidgets.Panel("Lock", avatar.parent, 16f, LibraryTheme.Colors.SurfaceSelected);
                Vector2 center = new Vector2(0.5f, 0.5f);
                LibraryWidgets.Place(_lock.rectTransform, center, center, new Vector2(TILE * 0.34f, -TILE * 0.34f), new Vector2(32f, 32f));
                LibraryWidgets.Icon(_lock.transform, LibraryIcon.Lock, LibraryTheme.Colors.TextSecondary, 19f);
            }

            internal string Id { get; }

            internal Image Ring { get; }

            /// <summary>The achievement that gives it, while that is not unlocked; null for an avatar to choose freely.</summary>
            internal AchievementProgress Giver { get; private set; }

            internal void Bind(IReadOnlyList<AchievementProgress> goals, Func<string, bool> ownsAvatar)
            {
                Giver = null;
                foreach (AchievementProgress goal in goals)
                {
                    if (goal.Goal.AvatarId == Id && goal.State != AchievementState.Unlocked && ownsAvatar?.Invoke(Id) != true) Giver = goal;
                }

                _group.alpha = Giver != null ? 0.3f : 1f;
                _lock.gameObject.SetActive(Giver != null);
            }
        }
    }

    /// <summary>A backup on its way out or in: what is happening, a breathing status line and 取消.</summary>
    internal sealed class ProfileBusyPanel : IDisposable
    {
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly TextMeshProUGUI _status;
        private readonly CanvasGroup _pulse;
        private readonly Button _cancel;
        private MotionHandle _breathe;
        private Action _onCancel;

        internal ProfileBusyPanel(RectTransform layer)
        {
            _root = LibraryUi.CreateRect("ProfileBusy", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(780f, 380f), string.Empty, null, withSubtitle: true);
            _status = LibraryWidgets.Label("Status", _modal.Body, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(_status.rectTransform, 0f, 0f, 24f, 30f);
            _pulse = _status.gameObject.AddComponent<CanvasGroup>();
            _cancel = ServerWidgets.LabelledButton(_modal.Body, "Cancel", "取消", LibraryIcon.Close, () => _onCancel?.Invoke(),
                220f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Cancel);
            LibraryWidgets.Place((RectTransform)_cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero,
                new Vector2(220f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Show(string title, string subtitle, string status, Action cancel)
        {
            _modal.Title.text = title;
            _modal.Subtitle.text = subtitle;
            _status.text = status;
            _onCancel = cancel;
            _cancel.gameObject.SetActive(cancel != null);
            _modal.Open(UiCue.None);
            _breathe.TryCancel();
            _breathe = LMotion.Create(0f, Mathf.PI * 2f, 1.6f)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(phase => _pulse.alpha = 0.5f + 0.5f * (0.5f + 0.5f * Mathf.Cos(phase)));
        }

        internal void Hide()
        {
            _breathe.TryCancel();
            _breathe = default;
            _onCancel = null;
            _modal.Close();
        }

        public void Dispose()
        {
            _breathe.TryCancel();
            _modal.Dispose();
        }
    }

    /// <summary>
    /// What a backup would change, before anything changes: how many runs are new, already here
    /// or in conflict (conflicts keep the local run), lines without a play id (skipped), whether
    /// the backup's avatar and nickname come along, and the Ranking Score before and after. 确认合并
    /// commits exactly this preview; a backup that adds nothing only offers 知道了.
    /// </summary>
    internal sealed class ProfileImportPanel : IDisposable
    {
        private const float TILE_HEIGHT = 128f;
        private const float ROW_HEIGHT = 64f;
        private const float BUTTON_WIDTH = 220f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly TextMeshProUGUI[] _values = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _captions = new TextMeshProUGUI[3];
        private readonly ProfileAvatarView _avatar;
        private readonly TextMeshProUGUI _appearance;
        private readonly RankingBadge _badge;
        private readonly TextMeshProUGUI _ranking;
        private readonly TextMeshProUGUI _notes;
        private readonly Button _confirm;
        private readonly Button _cancel;
        private Action _onConfirm;
        private Action _onCancel;

        internal ProfileImportPanel(RectTransform layer)
        {
            _root = LibraryUi.CreateRect("ProfileImport", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(1120f, 760f), "导入个人档案", () => _onCancel?.Invoke(), withSubtitle: true);
            _modal.Subtitle.text = "确认前不会改动本机档案；同一局只会计入一次";
            RectTransform body = _modal.Body;

            string[] names = { "Added", "Duplicates", "Conflicts" };
            for (int i = 0; i < 3; i++)
            {
                Image tile = PackWidgets.Group(body, names[i]);
                tile.rectTransform.anchorMin = new Vector2(i / 3f, 1f);
                tile.rectTransform.anchorMax = new Vector2((i + 1) / 3f, 1f);
                tile.rectTransform.pivot = new Vector2(0.5f, 1f);
                tile.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 10f, -TILE_HEIGHT);
                tile.rectTransform.offsetMax = new Vector2(i == 2 ? 0f : -10f, 0f);
                _values[i] = LibraryWidgets.Label("Value", tile.transform, "0", 64f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_values[i]);
                _values[i].overflowMode = TextOverflowModes.Overflow;
                _values[i].alignment = TextAlignmentOptions.BottomLeft;
                LibraryWidgets.TopStrip(_values[i].rectTransform, 26f, 20f, 6f, 76f);
                _captions[i] = LibraryWidgets.Label("Caption", tile.transform, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.BottomStrip(_captions[i].rectTransform, 28f, 20f, 18f, 26f);
            }

            float top = TILE_HEIGHT + 26f;
            Vector2 topLeft = new Vector2(0f, 1f);
            _avatar = new ProfileAvatarView(body, "Avatar", 56f, glow: false);
            LibraryWidgets.Place(_avatar.Root, topLeft, new Vector2(0.5f, 0.5f), new Vector2(32f, -(top + ROW_HEIGHT * 0.5f)),
                new Vector2(56f, 56f));
            _appearance = PackWidgets.UserLabel("Appearance", body, string.Empty, 22f, LibraryTheme.Colors.TextPrimary);
            LibraryWidgets.TopStrip(_appearance.rectTransform, 84f, 0f, top + 14f, 36f);

            top += ROW_HEIGHT + 12f;
            _badge = new RankingBadge(body, 58f);
            LibraryWidgets.Place(_badge.Root, topLeft, new Vector2(0.5f, 0.5f), new Vector2(32f, -(top + ROW_HEIGHT * 0.5f)),
                new Vector2(58f, 58f));
            _ranking = LibraryWidgets.Label("Ranking", body, string.Empty, 22f, LibraryTheme.Colors.TextPrimary);
            _ranking.richText = true;
            LibraryWidgets.TopStrip(_ranking.rectTransform, 84f, 0f, top + 14f, 36f);

            top += ROW_HEIGHT + 20f;
            _notes = PackWidgets.Paragraph(body, "Notes", string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Inset(_notes.rectTransform, 0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 20f, 0f, top);

            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _confirm = ServerWidgets.LabelledButton(body, "Confirm", "确认合并", LibraryIcon.Check, () => _onConfirm?.Invoke(),
                BUTTON_WIDTH, height, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_confirm.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero,
                new Vector2(BUTTON_WIDTH, height));
            _cancel = LibraryWidgets.PillButton("Cancel", body, "取消", () => _onCancel?.Invoke(), BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)_cancel.targetGraphic, height * 0.5f);
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <param name="before">The profile as it is now, for the Ranking Score before the merge.</param>
        internal void Show(ProfileImportPreview preview, PlayerProfileSnapshot before, Action confirm, Action cancel)
        {
            _onConfirm = confirm;
            _onCancel = cancel;
            int conflicts = preview.ConflictingIds?.Count ?? 0;
            int missing = preview.MissingIdLines?.Count ?? 0;
            SetTile(0, preview.Added, "局新的游玩", preview.Added > 0 ? HomeTheme.Colors.Violet : LibraryTheme.Colors.TextPrimary);
            SetTile(1, preview.Duplicates, "局已在本机，跳过", LibraryTheme.Colors.TextPrimary);
            SetTile(2, conflicts, "局与本机不同，保留本机", conflicts > 0 ? LibraryTheme.Colors.Warning : LibraryTheme.Colors.TextPrimary);

            ProfileAppearance appearance = preview.Result?.Appearance;
            _avatar.SetAvatar(appearance?.AvatarId ?? ProfileAvatars.DEFAULT_ID);
            _appearance.text = preview.RestoresAppearance
                ? "恢复备份中的头像与昵称「" + appearance?.Nickname + "」"
                : "保留本机的头像与昵称「" + appearance?.Nickname + "」";

            long from = before?.Best30.Total ?? 0L;
            long to = preview.Result?.Best30.Total ?? from;
            int tierFrom = before?.Best30.TierId ?? 0;
            int tierTo = preview.Result?.Best30.TierId ?? tierFrom;
            _badge.SetTier(tierTo);
            if (to != from)
            {
                long delta = to - from;
                string sign = delta > 0 ? "+" : string.Empty;
                _ranking.text = "Ranking Score  " + ProfileCopy.Number(from) + "  →  " + ProfileCopy.Number(to)
                    + "  <color=#C2AEFF>" + sign + ProfileCopy.Number(delta) + "</color>"
                    + (tierTo != tierFrom ? "    " + RankingTiers.Name(tierFrom) + " → " + RankingTiers.Name(tierTo) : string.Empty);
            }
            else
            {
                _ranking.text = "Ranking Score 保持 " + ProfileCopy.Number(from);
            }

            var notes = new StringBuilder();
            if (conflicts > 0)
            {
                notes.Append("与本机同一局但内容不同：").Append(ProfileCopy.Ids(preview.ConflictingIds)).Append("。这些局保留本机记录。\n");
            }

            if (missing > 0)
            {
                notes.Append("备份历史").Append(ProfileCopy.Lines(preview.MissingIdLines)).Append("缺少局 ID，无法判断是否重复，已跳过。\n");
            }

            int danAdded = preview.DanDefinitionsAdded + preview.DanAttemptsAdded;
            int danConflicts = preview.DanConflictingIds?.Count ?? 0;
            if (danAdded > 0 || preview.DanDuplicates > 0 || danConflicts > 0)
            {
                notes.Append("段位：");
                var parts = new List<string>();
                if (preview.DanDefinitionsAdded > 0) parts.Add("新增 " + ProfileCopy.Number(preview.DanDefinitionsAdded) + " 个段位");
                if (preview.DanAttemptsAdded > 0) parts.Add(ProfileCopy.Number(preview.DanAttemptsAdded) + " 次挑战记录");
                if (preview.DanDuplicates > 0) parts.Add(ProfileCopy.Number(preview.DanDuplicates) + " 项已在本机");
                if (danConflicts > 0) parts.Add(ProfileCopy.Number(danConflicts) + " 次挑战与本机不同，保留本机");
                notes.Append(string.Join("，", parts)).Append("。");
                notes.Append('\n');
            }

            if (preview.RestoresAppearance && preview.Result != null)
            {
                var worn = new StringBuilder();
                foreach (Medal medal in preview.Result.WornMedals)
                {
                    if (medal != null) worn.Append('「').Append(MedalCopy.Name(medal)).Append('」');
                }

                if (worn.Length > 0) notes.Append("会恢复佩戴").Append(worn).Append("。\n");
            }

            bool changes = preview.HasChanges;
            if (!changes)
            {
                notes.Append("备份里没有本机缺少的内容，无需合并。");
            }
            else if (notes.Length == 0)
            {
                notes.Append("所有记录均通过校验。合并后 Best 30 按全部记录重新计算。\n");
            }

            int reached = preview.ReachedAchievementIds?.Count ?? 0;
            if (changes && reached > 0)
            {
                notes.Append("合并后可解锁成就：").Append(AchievementCopy.Names(preview.ReachedAchievementIds)).Append("。");
            }

            _notes.text = notes.ToString().TrimEnd('\n');

            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _confirm.gameObject.SetActive(changes);
            ((TextMeshProUGUI)_cancel.GetComponentInChildren<TextMeshProUGUI>()).text = changes ? "取消" : "知道了";
            LibraryWidgets.Place((RectTransform)_cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(changes ? -(BUTTON_WIDTH + 16f) : 0f, 0f), new Vector2(BUTTON_WIDTH, height));
            PackWidgets.SetEnabled(_confirm, true);
            PackWidgets.SetEnabled(_cancel, true);
            _modal.Open();
        }

        /// <summary>Holds both buttons while a confirm is being written.</summary>
        internal void SetWorking(bool working)
        {
            PackWidgets.SetEnabled(_confirm, !working);
            PackWidgets.SetEnabled(_cancel, !working);
        }

        internal void Close()
        {
            _onConfirm = null;
            _onCancel = null;
            _modal.Close();
        }

        public void Dispose() => _modal.Dispose();

        private void SetTile(int index, int value, string caption, Color32 color)
        {
            _values[index].text = ProfileCopy.Number(value);
            _values[index].color = color;
            _captions[index].text = caption;
        }
    }

    /// <summary>
    /// Confirms deleting history the way the package card confirms 删除曲包. The first tap turns
    /// the row red and shakes it, and the second deletes. One run can always be deleted. Clearing
    /// everything needs a backup of the current history. Without one the card says so and offers
    /// 导出备份 in place of the delete row.
    /// </summary>
    internal sealed class ProfileDeletePanel : IDisposable
    {
        internal const string ROOT_NAME = "ProfileDelete";

        private readonly LibraryModal _modal;
        private readonly LibraryActionRow _delete;
        private readonly LibraryActionRow _export;
        private Action _onDelete;
        private Action _onExport;

        internal ProfileDeletePanel(RectTransform layer)
        {
            RectTransform root = LibraryUi.CreateRect(ROOT_NAME, layer);
            LibraryUi.Stretch(root);
            float header = LibraryTheme.Layout.MODAL_HEADER_HEIGHT;
            float height = header + 8f + LibraryActionRow.HEIGHT + LibraryTheme.Layout.MODAL_PADDING;
            _modal = new LibraryModal(root, new Vector2(780f, height), string.Empty, Close, withSubtitle: true);
            _modal.Title.fontSize = 30f;
            _modal.Title.richText = false;
            _modal.Subtitle.richText = false;
            RectTransform body = _modal.Body;
            body.offsetMax = new Vector2(body.offsetMax.x, -header - 8f);

            _delete = new LibraryActionRow(body, 0, "Delete", LibraryIcon.Trash, string.Empty, string.Empty, string.Empty, string.Empty,
                LibraryTheme.Colors.Danger, LibraryTheme.Colors.Danger, OnDeleteClicked);
            _export = new LibraryActionRow(body, 0, "Export", LibraryIcon.Export, "导出备份", "导出成功后会回到这里，继续清空",
                string.Empty, string.Empty, HomeTheme.Colors.Violet, HomeTheme.Colors.Violet, OnExportClicked);
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <param name="standing">The run's place in the Best 30, or 0 when it holds none.</param>
        /// <param name="backedUp">True when the last backup holds the whole current history, so this run too.</param>
        internal void ShowRun(CustomMusicScorePlayHistoryRecord play, int standing, bool backedUp, Action delete)
        {
            ProfileCopy.TryWhen(play, out string date, out string time);
            string when = string.IsNullOrEmpty(time) ? date : date + " " + time;
            Present(ProfileCopy.Title(play),
                ProfileCopy.Difficulty(play) + "  ·  " + when + "  ·  " + ProfileCopy.Number(play.TotalScore), delete, null);
            _delete.SetText("删除这条记录",
                standing > 0 ? "Best 30 第 " + standing + " 名，删除后 Ranking Score 会重新计算" : "从全部历史中移除这一局",
                "再点一次确认删除",
                backedUp ? "可从已导出的备份恢复" : "此操作无法撤销");
        }

        /// <param name="backedUp">False shows 导出备份 instead of the delete row.</param>
        internal void ShowClear(int count, bool backedUp, Action delete, Action export)
        {
            if (backedUp)
            {
                Present("清空全部历史", "共 " + ProfileCopy.Number(count) + " 局游玩记录，当前历史已备份", delete, null);
                _delete.SetText("删除全部记录", "Best 30 与 Ranking Score 会一并清零",
                    "再点一次确认清空", "可从已导出的备份恢复");
            }
            else
            {
                Present("清空前需要备份", "清空后只能从备份恢复，请先导出当前的 " + ProfileCopy.Number(count) + " 局记录",
                    null, export);
            }
        }

        internal void Close()
        {
            _onDelete = null;
            _onExport = null;
            _delete.SetArmed(false, false);
            _modal.Close();
        }

        public void Dispose()
        {
            _delete.Dispose();
            _export.Dispose();
            _modal.Dispose();
        }

        private void Present(string title, string subtitle, Action delete, Action export)
        {
            _modal.Title.text = title;
            _modal.Subtitle.text = subtitle;
            _onDelete = delete;
            _onExport = export;
            _delete.Rect.gameObject.SetActive(delete != null);
            _export.Rect.gameObject.SetActive(export != null);
            _delete.SetArmed(false, false);
            _modal.Open(delete != null ? UiCue.Open : UiCue.Blocked);
        }

        private void OnDeleteClicked()
        {
            if (!_delete.Armed)
            {
                UiSound.Play(UiCue.Warning);
                _delete.SetArmed(true, true);
                return;
            }

            Action action = _onDelete;
            Close();
            action?.Invoke();
        }

        private void OnExportClicked()
        {
            Action action = _onExport;
            Close();
            action?.Invoke();
        }
    }
}
