using System;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Confirms deleting a saved 段位 the way the package card confirms 删除曲包: the first tap
    /// turns the row red and shakes it, the second deletes. The row says what goes with the course
    /// (its attempts, and its plate when it was earned or is worn).
    /// </summary>
    internal sealed class DanDeletePanel : IDisposable
    {
        private readonly LibraryModal _modal;
        private readonly LibraryActionRow _delete;
        private Action _onDelete;

        internal DanDeletePanel(RectTransform layer)
        {
            RectTransform root = LibraryUi.CreateRect("DanDelete", layer);
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
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Show(DanRecord record, Action delete)
        {
            _onDelete = delete;
            int attempts = record.Attempts.Count;
            _modal.Title.text = "删除「" + record.Definition.Label + "」";
            _modal.Subtitle.text = DanCopy.Shape(record.Definition) + "  ·  挑战 " + attempts + " 次";
            string body;
            if (record.Equipped) body = "正在佩戴：段位牌会卸下，连同 " + attempts + " 次挑战记录一起删除";
            else if (record.Earned) body = "已取得的段位牌和 " + attempts + " 次挑战记录会一起删除";
            else if (attempts > 0) body = attempts + " 次挑战记录会一起删除";
            else body = "从本机移除；以后粘贴同一段分享文本即可找回";
            _delete.SetText("删除这个段位", body, "再点一次确认删除", attempts > 0 ? "此操作无法撤销，只能从备份恢复" : "此操作无法撤销");
            _delete.SetArmed(false, false);
            _modal.Open(UiCue.Open);
        }

        internal void Close()
        {
            _onDelete = null;
            _delete.SetArmed(false, false);
            _modal.Close();
        }

        public void Dispose()
        {
            _delete.Dispose();
            _modal.Dispose();
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
    }
}
