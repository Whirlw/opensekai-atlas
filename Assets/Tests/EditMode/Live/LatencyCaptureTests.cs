using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Shared.Diagnostics;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class LatencyCaptureTests
    {
        [Test]
        public void StartingAnotherCaptureKeepsEachExportsRowsAndMetadata()
        {
            bool enabled = LatencyCapture.Enabled;
            bool mix = LatencyCapture.MixProbeEnabled;
            string directory = Path.Combine(Application.persistentDataPath, "logs");
            var existing = Directory.Exists(directory) ? Directory.GetFiles(directory, "latency-*.tsv") : Array.Empty<string>();
            string marker = Guid.NewGuid().ToString("N");
            Task first = Task.CompletedTask;
            Task second = Task.CompletedTask;
            try
            {
                LatencyCapture.Enabled = true;
                LatencyCapture.MixProbeEnabled = false;
                LatencyCapture.Begin(marker + "-first", "master", false, 0f, 0f, 0f, false);
                for (int i = 0; i < 4096; i++)
                    LatencyCapture.Record("first_sample", noteId: i);
                first = LatencyCapture.End("retry");

                LatencyCapture.Begin(marker + "-second", "master", false, 0f, 0f, 0f, false);
                LatencyCapture.Record("second_sample", noteId: 42);
                second = LatencyCapture.End("play_stopped");
                Task.WhenAll(first, second).GetAwaiter().GetResult();

                var exports = Directory.GetFiles(directory, "latency-*.tsv").Except(existing).Select(File.ReadAllLines).ToArray();
                var firstRows = exports.Single(lines => lines[0].Contains($" chart={marker}-first "));
                var secondRows = exports.Single(lines => lines[0].Contains($" chart={marker}-second "));
                Assert.That(firstRows[2], Is.EqualTo("# end=retry rows=4096 overflow=0"));
                Assert.That(firstRows.Length, Is.EqualTo(4100));
                Assert.That(firstRows[4099].Split('\t')[7], Is.EqualTo("4095"));
                Assert.That(secondRows[2], Is.EqualTo("# end=play_stopped rows=1 overflow=0"));
                Assert.That(secondRows.Length, Is.EqualTo(5));
                Assert.That(secondRows[4].Split('\t')[7], Is.EqualTo("42"));
            }
            finally
            {
                Task.WhenAll(first, second, LatencyCapture.End("test_cleanup")).GetAwaiter().GetResult();
                LatencyCapture.MixProbeEnabled = mix;
                LatencyCapture.Enabled = enabled;
                if (Directory.Exists(directory))
                {
                    foreach (string path in Directory.GetFiles(directory, "latency-*.tsv").Except(existing))
                    {
                        if (File.ReadLines(path).FirstOrDefault()?.Contains($" chart={marker}-") == true)
                            File.Delete(path);
                    }
                }
            }
        }

        [Test]
        public void ExportCarriesAndroidProbeColumnsAndExplicitTimes()
        {
            bool enabled = LatencyCapture.Enabled;
            bool mix = LatencyCapture.MixProbeEnabled;
            string directory = Path.Combine(Application.persistentDataPath, "logs");
            var existing = Directory.Exists(directory) ? Directory.GetFiles(directory, "latency-*.tsv") : Array.Empty<string>();
            string marker = Guid.NewGuid().ToString("N");
            Task export = Task.CompletedTask;
            try
            {
                LatencyCapture.Enabled = true;
                LatencyCapture.MixProbeEnabled = false;
                LatencyCapture.Begin(marker, "master", false, 0f, 0f, 0f, false);
                // Gameplay rows fill their buffer first; probe rows keep their reserved space.
                for (int i = 0; i <= LatencyCapture.CAPACITY; i++)
                    LatencyCapture.Record("filler");
                LatencyCapture.Record("clock_anchor", time: 12.5, value: 0.01, androidReceive: 5012.25, detail: "begin");
                LatencyCapture.Record("android_edge", touch: 3, finger: 2, phase: UnityEngine.InputSystem.TouchPhase.Ended,
                    x: 100.5, y: 200.25, androidEvent: 5013.125, androidReceive: 5013.5, detail: "activity");
                export = LatencyCapture.End("play_stopped");
                export.GetAwaiter().GetResult();

                string[] lines = Directory.GetFiles(directory, "latency-*.tsv").Except(existing).Select(File.ReadAllLines)
                    .Single(file => file[0].Contains($" chart={marker} "));
                Assert.That(lines[2], Is.EqualTo($"# end=play_stopped rows={LatencyCapture.CAPACITY + 2} overflow=1 probe_overflow=0"));
                string[] columns = lines[3].Split('\t');
                Assert.That(columns.Skip(15), Is.EqualTo(new[] { "x", "y", "android_event_s", "android_receive_s" }));
                string[] anchor = lines.Skip(4).Select(line => line.Split('\t')).Single(row => row[1] == "clock_anchor");
                Assert.That(anchor[2], Is.EqualTo("12.5"));
                Assert.That(anchor[18], Is.EqualTo("5012.25"));
                string[] edge = lines.Skip(4).Select(line => line.Split('\t')).Single(row => row[1] == "android_edge");
                Assert.That(edge[4], Is.EqualTo("3"));
                Assert.That(edge[6], Is.EqualTo("Ended"));
                Assert.That(edge.Skip(15), Is.EqualTo(new[] { "100.5", "200.25", "5013.125", "5013.5" }));
            }
            finally
            {
                Task.WhenAll(export, LatencyCapture.End("test_cleanup")).GetAwaiter().GetResult();
                LatencyCapture.MixProbeEnabled = mix;
                LatencyCapture.Enabled = enabled;
                if (Directory.Exists(directory))
                {
                    foreach (string path in Directory.GetFiles(directory, "latency-*.tsv").Except(existing))
                    {
                        if (File.ReadLines(path).FirstOrDefault()?.Contains($" chart={marker} ") == true)
                            File.Delete(path);
                    }
                }
            }
        }
    }
}
