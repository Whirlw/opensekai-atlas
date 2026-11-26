using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSekai.App.Library;
using OpenSekai.App.Server;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Packages;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        /// <summary>
        /// Puts the sample library in: packages, Ranked verdicts, runs, saved servers and a resource
        /// pack. On the way it pictures the server list without servers. It refuses to run outside a
        /// sample workspace.
        /// </summary>
        private IEnumerator SampleShot(Shot shot)
        {
            yield return WaitForLibrary();
            SampleLibrary.WritePackages();
            Resync();
            yield return new WaitForSecondsRealtime(2f);
            SampleLibrary.SeedRecords();
            CustomMusicScorePlayHistoryStorage.ReloadSummaries();

            yield return EmptyServersFrame(shot);
            ServerRegistry servers = ServerRegistry.Shared;
            servers.TryAdd(ServerKind.Sonolus, SampleServer.NAME, SampleServer.BASE_URL, out _);
            servers.TryAdd(ServerKind.Sonolus, "社区测试服", "https://test.example.org/sekai", out _);
            servers.TryAdd(ServerKind.WebView, "示例网页谱面站", "https://web.example.net", out _);

            var import = MaterialPackStore.Shared.ImportAsync(SampleLibrary.WriteResourcePack(Path.Combine(_request.outDir, "sample")));
            yield return WaitUntil(() => import.IsCompleted, "the sample resource pack");
            if (import.Exception != null)
            {
                Log("the sample resource pack was not imported: " + import.Exception.GetBaseException().Message);
            }

            Resync();
            // New songs are scanned for loudness, which the status line reports; let the scan finish.
            yield return new WaitForSecondsRealtime(6f);
            Log("sample library: " + SampleLibrary.Songs.Count + " songs");
        }

        /// <summary>Clears the workspace's saved servers and pictures the list that has none.</summary>
        private IEnumerator EmptyServersFrame(Shot shot)
        {
            ServerRegistry servers = ServerRegistry.Shared;
            foreach (ServerEntry entry in servers.Servers.ToArray())
            {
                servers.TryRemove(entry, out _);
            }

            HomeScreen home = HomeScreen.Current;
            home.ShowHomeImmediately();
            yield return null;
            home.EnterServer();
            yield return WaitUntil(() => home.IsServerShown, "the server list");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, FileStem(shot, null) + "-servers-empty", "the server list with no server", null);
            UnityEngine.Object.FindFirstObjectByType<ServerScreen>().Back();
            yield return WaitUntil(() => home.IsHomeShown && !home.IsServerShown, "the home page after back");
            home.ShowLibraryImmediately();
            yield return WaitForLibrary();
        }

        private static void Resync()
        {
            LibraryScreen screen = UnityEngine.Object.FindFirstObjectByType<LibraryScreen>();
            object presenter = screen.GetType().GetField("_presenter", ANY).GetValue(screen);
            presenter.GetType().GetMethod("Resync", ANY).Invoke(presenter, null);
        }

        /// <summary>
        /// The library in every state a picture of it needs, on the sample library: rows, tiles,
        /// groups, an open dropdown, no match, a song without audio and one without a cover, the hint
        /// under a blocked 详情, the 谱面详情 card both ways with its rules, and the settings card's
        /// two ends.
        /// </summary>
        private IEnumerator LibraryStatesShot(Shot shot)
        {
            yield return WaitForLibrary();
            LibraryScreen screen = UnityEngine.Object.FindFirstObjectByType<LibraryScreen>();
            var view = (LibraryView)screen.GetType().GetField("_view", ANY).GetValue(screen);
            object presenter = screen.GetType().GetField("_presenter", ANY).GetValue(screen);
            void Call(string name, object argument) => presenter.GetType().GetMethod(name, ANY).Invoke(presenter, new[] { argument });
            void Tap(string name) => view.GetType().GetMethod(name, ANY).Invoke(view, null);
            void Select(string title)
            {
                var items = (IReadOnlyList<SongPackageItem>)view.GetType().GetField("_items", ANY).GetValue(view);
                Call("Select", items.First(item => item.Entry?.Manifest?.title == title));
            }

            string stem = FileStem(shot, null);
            Call("SetSearch", string.Empty);
            Call("SetGroupMode", LibraryModel.GroupKeys[0]);
            Call("SetViewMode", LibraryViewMode.List);
            Select("Aurora Line");
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture(shot, stem + "-list", "rows", null);
            Select("Saffron Relay");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture(shot, stem + "-accent", "another cover, another accent", null);

            Select("Glass Garden");
            Call("SetViewMode", LibraryViewMode.Grid);
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture(shot, stem + "-grid", "tiles", null);
            Call("SetGroupMode", LibraryModel.GroupKeys[1]);
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, stem + "-grouped", "tiles in groups", null);
            Call("SetGroupMode", LibraryModel.GroupKeys[0]);
            Call("SetViewMode", LibraryViewMode.List);
            yield return new WaitForSecondsRealtime(1f);

            var sort = (LibraryDropdown)view.GetType().GetField("_sortDropdown", ANY).GetValue(view);
            sort.GetType().GetMethod("Toggle", ANY).Invoke(sort, null);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture(shot, stem + "-dropdown", "the sort dropdown open", null);
            sort.Close();

            Call("SetSearch", "没有这首歌");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture(shot, stem + "-empty", "a search with no match", null);
            Call("SetSearch", string.Empty);
            yield return new WaitForSecondsRealtime(0.8f);

            Select("Faraway Choir");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture(shot, stem + "-missing", "audio missing", null);
            Select("Northbound");
            yield return new WaitForSecondsRealtime(1.5f);
            Tap("OnDetailsButton");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Capture(shot, stem + "-hint", "no cover, and the hint under a blocked 详情", null);

            Select("Starlit Atlas");
            yield return new WaitForSecondsRealtime(1.5f);
            Tap("OnDetailsButton");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, stem + "-details-ranked", "谱面详情, Ranked", null);
            object details = view.GetType().GetField("_chartInfo", ANY).GetValue(view);
            details.GetType().GetMethod("OpenRules", ANY).Invoke(details, null);
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Capture(shot, stem + "-rules", "Ranked 规则", null);
            view.TryHandleBack();
            view.TryHandleBack();
            Select("Night Signal");
            yield return new WaitForSecondsRealtime(1.2f);
            Tap("OnDetailsButton");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, stem + "-details-unranked", "谱面详情, Unranked", null);
            view.TryHandleBack();

            view.ShowSettings();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture(shot, stem + "-settings", "settings", null);
            LibrarySettingsPanel settings = screen.GetComponentInChildren<LibrarySettingsPanel>(true);
            var scroll = (ScrollRect)settings.GetType().GetField("_mainScroll", ANY).GetValue(settings);
            scroll.verticalNormalizedPosition = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture(shot, stem + "-settings-end", "settings, scrolled to the end", null);
            settings.Hide();
            Select("Aurora Line");
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
