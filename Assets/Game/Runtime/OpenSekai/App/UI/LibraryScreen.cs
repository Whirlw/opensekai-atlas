using OpenSekai.App;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    public sealed class LibraryScreen : MonoBehaviour, ILibraryPage
    {
        private LibraryView _view;
        private LibraryPresenter _presenter;
        private AppContext _context;

        public bool IsReady => isActiveAndEnabled && _view?.HasList == true;

        /// <summary>
        /// Builds the lightweight library page on top of the services the entry point assembled.
        /// The page takes only the two it needs: the package store and the playback entry.
        /// </summary>
        public static LibraryScreen Create(AppContext context, Transform parent = null)
        {
            var root = new GameObject("LibraryScreen", typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var screen = root.AddComponent<LibraryScreen>();
            screen.Build(context);
            return screen;
        }

        public void Refresh() => _presenter?.Refresh();
        public void ShowResult(PlayResult result) => _presenter?.ShowResult(result);

        private void Awake() => LibraryPageRegistry.Register(this);

        private void Build(AppContext context)
        {
            _context = context;
            _view = new LibraryView(gameObject);
            _presenter = new LibraryPresenter(_view, context.Packages, context.Playback.StartAsync);
            _view.BuildLayout(_presenter);
            _presenter.Refresh();
            _view.BuildPanels(_presenter);
        }

        private void Start() => ShowResult(PlayResultHandoff.Take());

        private void OnDestroy()
        {
            LibraryPageRegistry.Unregister(this);
            _presenter?.Dispose();
            _context = null;
        }
    }
}
