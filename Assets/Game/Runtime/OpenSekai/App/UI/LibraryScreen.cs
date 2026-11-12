using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    public sealed class LibraryScreen : MonoBehaviour, ILibraryPage
    {
        private LibraryView _view;
        private LibraryPresenter _presenter;

        public bool IsReady => isActiveAndEnabled && _view?.HasList == true;

        public static LibraryScreen Create(Transform parent = null)
        {
            var root = new GameObject("LibraryScreen", typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var screen = root.AddComponent<LibraryScreen>();
            screen.Build();
            return screen;
        }

        public void Refresh() => _presenter?.Refresh();
        public void ShowResult(PlayResult result) => _presenter?.ShowResult(result);

        private void Awake() => LibraryPageRegistry.Register(this);

        private void Build()
        {
            _view = new LibraryView(gameObject);
            _presenter = new LibraryPresenter(_view, SongPackageManager.Shared, PlayService.Instance.StartAsync);
            _view.BuildLayout(_presenter);
            _presenter.Refresh();
            _view.BuildPanels(_presenter);
        }

        private void Start() => ShowResult(PlayResultHandoff.Take());

        private void OnDestroy()
        {
            LibraryPageRegistry.Unregister(this);
            _presenter?.Dispose();
        }
    }
}
