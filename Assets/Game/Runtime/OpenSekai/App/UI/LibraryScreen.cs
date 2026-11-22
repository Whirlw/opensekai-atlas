using System;
using System.Collections.Generic;
using OpenSekai.App;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    public sealed partial class LibraryScreen : MonoBehaviour, ILibraryPage
    {
        private LibraryView _view;
        private LibraryPresenter _presenter;
        private IEnumerator<object> _build;

        public bool IsReady => isActiveAndEnabled && _build == null && _view?.HasList == true;

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
            var screen = BeginBuild(context, parent, presented);
            screen.FinishBuild();
            return screen;
        }

        internal static LibraryScreen BeginBuild(AppContext context, Transform parent = null, bool presented = false)
        {
            var root = new GameObject("LibraryScreen", typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var screen = root.AddComponent<LibraryScreen>();
            screen._build = screen.Build(context, presented);
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

        public void Refresh()
        {
            // An in-progress build reads the current catalog in its final step.
            if (_build == null) _presenter?.Refresh();
        }

        public void ShowResult(PlayResult result) => _presenter?.ShowResult(result);

        private void Awake() => LibraryPageRegistry.Register(this);

        internal void AdvanceBuild()
        {
            if (_build == null || _build.MoveNext()) return;
            _build.Dispose();
            _build = null;
        }

        internal void FinishBuild()
        {
            while (_build != null) AdvanceBuild();
        }

        private void Start()
        {
            var result = PlayResultHandoff.Take();
            if (result == null) return;
            FinishBuild();
            ShowResult(result);
        }

        private void OnEnable() => _view?.SetVisible(true);

        private void OnDisable() => _view?.SetVisible(false);

        private void OnApplicationPause(bool paused) => _view?.SetApplicationPaused(paused);

        private void OnDestroy()
        {
            LibraryPageRegistry.Unregister(this);
            _presenter?.Dispose();
            _view?.Dispose();
            _build?.Dispose();
        }
    }
}
