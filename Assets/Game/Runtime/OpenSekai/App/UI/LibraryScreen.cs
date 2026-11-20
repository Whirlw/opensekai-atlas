using System;
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

        /// <summary>False while another page (the home page) is on screen in its place.</summary>
        public bool IsPresented => _view?.IsPresented == true;

        /// <summary>The page's back button; whoever owns the page decides where back leads.</summary>
        internal event Action BackRequested;

        /// <summary>
        /// Builds the lightweight library page on top of the services the entry point assembled.
        /// The page takes only the two it needs: the package store and the playback entry.
        /// </summary>
        /// <param name="presented">False builds the page off screen: no camera, no song preview,
        /// until <see cref="SetPresented"/> brings it in.</param>
        public static LibraryScreen Create(AppContext context, Transform parent = null, bool presented = true)
        {
            var root = new GameObject("LibraryScreen", typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var screen = root.AddComponent<LibraryScreen>();
            screen.Build(context, presented);
            return screen;
        }

        /// <summary>Brings the page on screen or takes it off.</summary>
        /// <param name="animate">Coming on: lift the curtain and let the list cascade in.</param>
        internal void SetPresented(bool presented, bool animate) => _view?.SetPresented(presented, animate);

        /// <summary>Lowers the page's curtain to the ground color, then runs <paramref name="onCovered"/>.</summary>
        internal void PlayExit(Action onCovered)
        {
            if (_view == null)
            {
                onCovered?.Invoke();
                return;
            }

            _view.PlayExit(onCovered);
        }

        /// <summary>
        /// Gives the Android back key to whatever is open on the page (a menu, a card, the loading
        /// screen). False when nothing took it, and back should leave the page.
        /// </summary>
        internal bool TryHandleBack() => _view != null && _view.TryHandleBack();

        public void Refresh() => _presenter?.Refresh();

        public void ShowResult(PlayResult result) => _presenter?.ShowResult(result);

        private void Awake() => LibraryPageRegistry.Register(this);

        private void Build(AppContext context, bool presented)
        {
            _context = context;
            _view = new LibraryView(gameObject, presented) { BackRequested = () => BackRequested?.Invoke() };
            var loudness = new LibraryLoudnessScanner(context.Packages, status => _view?.SetStatus(status));
            _presenter = new LibraryPresenter(_view, context.Packages, context.Playback.StartAsync, loudness: loudness);
            _view.BuildLayout(_presenter);
            _presenter.Refresh();
            _view.BuildPanels(_presenter);
        }

        private void Start() => ShowResult(PlayResultHandoff.Take());

        private void OnEnable() => _view?.SetVisible(true);

        private void OnDisable() => _view?.SetVisible(false);

        private void OnApplicationPause(bool paused) => _view?.SetApplicationPaused(paused);

        private void OnDestroy()
        {
            LibraryPageRegistry.Unregister(this);
            _presenter?.Dispose();
            _view?.Dispose();
            _context = null;
        }
    }
}
