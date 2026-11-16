using System;
using System.Collections.Generic;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.UI
{
    /// <summary>Everything the page needs to draw the list, the group bar and the detail side.</summary>
    internal sealed class LibraryViewState
    {
        internal LibraryViewState(
            LibraryListing listing,
            LibraryGroupKey groupKey,
            LibrarySortKey sortKey,
            LibraryViewMode viewMode,
            SongPackageItem selected,
            IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            Listing = listing;
            GroupKey = groupKey;
            SortKey = sortKey;
            ViewMode = viewMode;
            Selected = selected;
            History = history;
        }

        internal LibraryListing Listing { get; }
        internal LibraryGroupKey GroupKey { get; }
        internal LibrarySortKey SortKey { get; }
        internal LibraryViewMode ViewMode { get; }

        /// <summary>The package shown on the detail side; null when the list is empty.</summary>
        internal SongPackageItem Selected { get; }

        internal IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> History { get; }
    }

    internal interface ILibraryView
    {
        bool HasList { get; }

        /// <summary>Redraws the list, the group bar, the option labels and the detail side.</summary>
        void Render(LibraryViewState state);

        /// <summary>Moves the selection inside the list that is already on screen.</summary>
        void SetSelection(SongPackageItem selected);

        /// <summary>Redraws the visible cells and the detail side of the package with this score key.</summary>
        void RefreshScore(string scoreKey);

        void SetStatus(string message);
        void ShowSettings();
        void ShowResult(PlayResult result);
        void ShowLoading(string title, string packageDirectory);
        void HideLoading();
        void SetLoadingMessage(string message);
        void ShowActions(SongPackageItem item, Action export, Action delete);
    }
}
