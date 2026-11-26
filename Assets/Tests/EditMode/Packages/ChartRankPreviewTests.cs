using System;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using UnityEngine.Networking;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class ChartRankPreviewTests
    {
        private const string PREVIEW = "https://chartview.unipjsk.com/preview?cfg=example";

        [TestCase(429, null)]
        [TestCase(503, null)]
        [TestCase(503, PREVIEW)]
        public void PreviewHttpErrorKeepsThePublishedRank(long status, string location)
        {
            string key = "os-" + Guid.NewGuid().ToString("N");
            var known = ChartRankCache.FromWeb("quality", 30, "published");
            ChartRankCache.Save(key, known);
            string file = Path.Combine(UnityEngine.Application.persistentDataPath, "ChartRanks", key + ".json");
            try
            {
                string saved = File.ReadAllText(file);
                UniTask<ChartRankCache.Verdict> Fetch(ChartRankCache.Verdict previous)
                {
                    ChartRankLookup.PreviewRedirect(UnityWebRequest.Result.ProtocolError, status, "HTTP error", location);
                    return UniTask.FromResult(ChartRankCache.FromUnreadableResponse("installed", 30));
                }

                var check = ChartRankLookup.CheckRemoteAsync(key, "installed", 30, Fetch).GetAwaiter().GetResult();
                Assert.That(check.Outcome, Is.EqualTo(RankCheckOutcome.RetryLater));
                Assert.That(ChartRankCache.Read(key), Is.SameAs(known));
                Assert.That(File.ReadAllText(file), Is.EqualTo(saved));
            }
            finally { File.Delete(file); }
        }

        [Test]
        public void RedirectLimitErrorStillAllowsARealPublisherRedirect()
        {
            var target = ChartRankLookup.PreviewRedirect(UnityWebRequest.Result.ConnectionError, 302, "Redirect limit", PREVIEW);
            Assert.That(target.AbsoluteUri, Is.EqualTo(PREVIEW));
        }

        [Test]
        public void OfflinePreviewPreservesConnectionFailureClassification()
        {
            var error = Assert.Throws<SonolusException>(() =>
                ChartRankLookup.PreviewRedirect(UnityWebRequest.Result.ConnectionError, 0, "DNS error", null));
            Assert.That(error.IsNetworkFailure, Is.True);
        }

        [TestCase(200)]
        [TestCase(304)]
        public void NonRedirectCannotSupplyAChartLocation(long status)
        {
            Assert.Throws<InvalidDataException>(() =>
                ChartRankLookup.PreviewRedirect(UnityWebRequest.Result.Success, status, null, PREVIEW));
        }
    }
}
