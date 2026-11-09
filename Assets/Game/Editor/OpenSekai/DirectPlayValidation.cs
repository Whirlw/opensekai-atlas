using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sekai;
using UnityEditor;
using UnityEngine;
using OpenSekai.App.Playback;

namespace OpenSekai.EditorTools
{
    public static class DirectPlayValidation
    {
        [MenuItem("OpenSekai/Library/Verify Direct Play Failures")]
        public static void VerifyFailures()
        {
            VerifyFailuresAsync().Forget();
        }

        private static async UniTask VerifyFailuresAsync()
        {
            if (!EditorApplication.isPlaying || SceneManager.Instance.CurrentScene != SceneManager.Scene.Library)
            {
                throw new InvalidOperationException("Run Library before checking direct play failures.");
            }
            var service = PlayService.Instance;
            var previousBoot = UserDataManager.Instance.FreeLiveBootData;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Library/OpenSekai/PlaybackChecks/" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);

            var missing = await service.StartAsync(Request(Path.Combine(root, "missing")), CancellationToken.None);
            Require(missing.Status == PlayStartStatus.Failed, "Missing package was accepted.");

            string noAudio = Path.Combine(root, "no-audio");
            WritePackage(noAudio, "{}", false);
            var audioResult = await service.StartAsync(Request(noAudio), CancellationToken.None);
            Require(audioResult.Status == PlayStartStatus.Failed && audioResult.Message.Contains("音频"), "Missing audio was accepted.");

            string badChart = Path.Combine(root, "bad-chart");
            WritePackage(badChart, "{ broken json", true);
            var chartResult = await service.StartAsync(Request(badChart), CancellationToken.None);
            Require(chartResult.Status == PlayStartStatus.Failed && chartResult.Message.Contains("格式"), "Malformed chart was accepted.");

            string emptyChart = Path.Combine(root, "empty-chart");
            WritePackage(emptyChart, "{\"VersionCode\":1,\"NoteList\":[]}", true);
            var emptyResult = await service.StartAsync(Request(emptyChart), CancellationToken.None);
            Require(emptyResult.Status == PlayStartStatus.Failed && emptyResult.Message.Contains("音符"), "Empty chart was accepted.");

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var result = await service.StartAsync(Request(badChart), cancelled.Token);
                Require(result.Status == PlayStartStatus.Cancelled, "Cancelled request was accepted.");
            }

            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Builds/Baseline-2026-11-01/fixtures/baseline_tap"));
            Require(File.Exists(Path.Combine(fixture, "manifest.json")), "Local baseline fixture is missing.");
            PlayStartStatus duplicateStatus = PlayStartStatus.Failed;
            using (var cancelled = new CancellationTokenSource())
            {
                bool checkedBusy = false;
                var result = await service.StartAsync(Request(fixture), cancelled.Token, _ =>
                {
                    if (checkedBusy) return;
                    checkedBusy = true;
                    duplicateStatus = service.StartAsync(Request(fixture), CancellationToken.None).GetAwaiter().GetResult().Status;
                    cancelled.Cancel();
                });
                Require(duplicateStatus == PlayStartStatus.Busy, "Concurrent request was not rejected.");
                Require(result.Status == PlayStartStatus.Cancelled, "Cancellation during preparation was ignored.");
            }

            Require(!service.IsStarting, "Start lock was not released after cancellation or failure.");
            Require(ReferenceEquals(previousBoot, UserDataManager.Instance.FreeLiveBootData), "A failed request published live data.");
            Require(SceneManager.Instance.CurrentScene == SceneManager.Scene.Library, "Failure left the library scene.");
            Debug.Log("[DirectPlayValidation] PASS: missing package/audio, malformed/empty chart, cancellation, duplicate rejection, lock release and no live data publication.");
        }

        private static PlayRequest Request(string path)
        {
            return new PlayRequest(path, true, SceneManager.Scene.Library);
        }

        private static void WritePackage(string path, string chart, bool writeAudio)
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "manifest.json"), JsonConvert.SerializeObject(new
            {
                formatVersion = 1,
                id = "local-validation",
                title = "Local validation",
                scoreFileName = "score.json",
                audioFileName = "audio.wav"
            }));
            File.WriteAllText(Path.Combine(path, "score.json"), chart);
            if (writeAudio) File.WriteAllBytes(Path.Combine(path, "audio.wav"), new byte[] { 0 });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
