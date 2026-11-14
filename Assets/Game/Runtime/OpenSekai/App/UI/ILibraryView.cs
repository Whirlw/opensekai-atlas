using System;
using System.Collections.Generic;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.UI
{
    internal interface ILibraryView
    {
        bool HasList { get; }
        void ClearRows();
        void SetCount(int count);
        void SetSortLabel(string label);
        void RenderRows(IReadOnlyList<SongPackageItem> items, Action<SongPackageItem, bool> play, Action<SongPackageItem> openActions);
        void SetStatus(string message);
        void ShowSettings();
        void ShowResult(PlayResult result);
        void ShowLoading(string title);
        void HideLoading();
        void SetLoadingMessage(string message);
        void ShowActions(string title, Action export, Action delete);
    }
}
