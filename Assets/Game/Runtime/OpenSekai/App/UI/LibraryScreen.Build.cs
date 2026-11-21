using System.Collections.Generic;
using OpenSekai.App;

namespace OpenSekai.App.UI
{
    public sealed partial class LibraryScreen
    {
        private IEnumerator<object> Build(AppContext context, bool presented)
        {
            _view = new LibraryView(gameObject, presented) { BackRequested = () => BackRequested?.Invoke() };
            var loudness = new LibraryLoudnessScanner(context.Packages, status => _view?.SetStatus(status));
            _presenter = new LibraryPresenter(_view, context.Packages, context.Playback.StartAsync,
                loudness: loudness, persistBrowsePreferences: true);
            foreach (var step in _view.BuildLayout(_presenter)) yield return step;
            yield return null;
            foreach (var step in _view.BuildInitialListing()) yield return step;
        }

    }
}
