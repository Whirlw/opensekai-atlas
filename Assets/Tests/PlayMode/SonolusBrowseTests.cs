using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.App.Server;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Sonolus;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class SonolusBrowseTests
    {
        [UnityTest]
        public IEnumerator DetailErrorFollowsTheSelectedTabAndRetryLoadsIt()
        {
            var first = new UniTaskCompletionSource<SonolusLevelDetails>();
            var retry = new UniTaskCompletionSource<SonolusLevelDetails>();
            int calls = 0;
            var address = SonolusServerAddress.Parse("example.com");
            var entry = new ServerEntry("test", ServerKind.Sonolus, "Test server", address.BaseUrl);
            var session = new SonolusSession(entry, address);
            var item = new Newtonsoft.Json.Linq.JObject { ["name"] = "test" };
            var screen = ServerScreen.Create();
            SonolusLevelPage page = null;
            try
            {
                screen.SetPresented(true, false);
                page = new SonolusLevelPage(screen, session, SonolusLevel.From(item),
                    (_, __) => ++calls == 1 ? first.Task : retry.Task);
                page.OnShown();
                page.ShowTab(1);
                page.ShowTab(0);
                page.ShowTab(2);
                first.TrySetException(new SonolusException("detail request failed"));
                page.OnHidden();
                page.OnShown();
                page.ShowTab(1);
                var status = page.Root.GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.text == "detail request failed" && text.gameObject.activeInHierarchy);
                status.transform.parent.Find("Retry").GetComponent<Button>().onClick.Invoke();
                Assert.That(calls, Is.EqualTo(2));
                retry.TrySetResult(SonolusLevelDetails.From(new Newtonsoft.Json.Linq.JObject { ["item"] = item }));
                Assert.That(page.HasDetails, Is.True);
                Assert.That(status.text, Does.Not.Contain("failed"));
            }
            finally
            {
                page?.Dispose();
                UnityEngine.Object.Destroy(screen.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturningFromHomeRestartsTheInterruptedFirstSearchPage()
        {
            var firstPage = new UniTaskCompletionSource<SonolusPage<SonolusLevel>>();
            var resumedPage = new UniTaskCompletionSource<SonolusPage<SonolusLevel>>();
            var requests = new List<(string keywords, int page, string cursor, CancellationToken token)>();
            var empty = new SonolusPage<SonolusLevel>(Array.Empty<SonolusLevel>(), 0, null);
            SonolusServerAddress address = SonolusServerAddress.Parse("example.com");
            var entry = new ServerEntry("test", ServerKind.Sonolus, "Test server", address.BaseUrl);
            var session = new SonolusSession(entry, address) { LevelsInfo = SonolusLevelsInfo.From(null) };
            ServerScreen screen = ServerScreen.Create();
            SonolusBrowsePage browse = null;
            try
            {
                screen.SetPresented(true, false);
                browse = new SonolusBrowsePage(screen, session, (query, page, cursor, token) =>
                {
                    string keywords = query.Single(pair => pair.Key == "keywords").Value;
                    requests.Add((keywords, page, cursor, token));
                    return requests.Count == 1 ? firstPage.Task : resumedPage.Task;
                });
                Button home = browse.Root.Find("Modes/Mode-首页").GetComponent<Button>();
                Button results = browse.Root.Find("Modes/Mode-搜索结果").GetComponent<Button>();
                var status = browse.Root.Find("Body/Status/Text").GetComponent<TMP_Text>();

                browse.Search("test chart");
                Assert.That(requests, Has.Count.EqualTo(1));
                home.onClick.Invoke();
                Assert.That(requests[0].token.IsCancellationRequested, Is.True);

                results.onClick.Invoke();
                Assert.That(requests, Has.Count.EqualTo(2));
                Assert.That(requests[1].keywords, Is.EqualTo("test chart"));
                Assert.That(requests[1].page, Is.Zero);
                Assert.That(requests[1].cursor, Is.Null);
                Assert.That(requests[1].token.IsCancellationRequested, Is.False);

                firstPage.TrySetResult(empty);
                Assert.That(status.text, Is.EqualTo("正在加载谱面…"));
                resumedPage.TrySetResult(empty);
                Assert.That(status.text, Is.EqualTo("没有找到符合条件的谱面"));

                home.onClick.Invoke();
                results.onClick.Invoke();
                Assert.That(requests, Has.Count.EqualTo(2), "A completed empty search is preserved.");
                Assert.That(status.isActiveAndEnabled, Is.True);
            }
            finally
            {
                browse?.Dispose();
                firstPage.TrySetCanceled();
                resumedPage.TrySetCanceled();
                UnityEngine.Object.Destroy(screen.gameObject);
            }

            yield return null;
        }
    }
}
