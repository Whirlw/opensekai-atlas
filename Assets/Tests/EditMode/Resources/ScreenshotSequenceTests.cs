using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenSekai.EditorTools.Screenshots;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class ScreenshotSequenceTests
    {
        [Test]
        [Platform("Win")]
        public void CommandLineRangesAndStillsPreserveOrderAndPreset()
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            const string COMMAND = ". './Tools/screenshot-time-ranges.ps1'; " +
                "ConvertTo-ScreenshotShots @{kind='clip';name='tier-upgrade'} @('1','2','-0.5..1.5','0..0.25') 120 | ConvertTo-Json";
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + COMMAND + "\"")
            {
                WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            });
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, output);
            Shot[] shots = JsonConvert.DeserializeObject<Shot[]>(output);
            Assert.That(shots.Length, Is.EqualTo(3));
            Assert.That(shots[0].times, Is.EqualTo(new[] { 1f, 2f }));
            Assert.That(shots[1].from, Is.EqualTo(-0.5d));
            Assert.That(shots[1].to, Is.EqualTo(1.5d));
            Assert.That(shots[2].name, Is.EqualTo("tier-upgrade"));
            Assert.That(shots[2].fps, Is.EqualTo(120));
        }

        [Test]
        public void JsonRangeDefaultsToZeroAndUsesAContinuousFrameIndex()
        {
            var shot = JsonConvert.DeserializeObject<Shot>("{'kind':'intro','times':[1],'to':3.1}");
            shot.PrepareSequence();
            Assert.That(shot.from, Is.EqualTo(0));
            Assert.That(shot.FrameCount, Is.EqualTo(186));
            Assert.That(shot.FrameTime(60), Is.EqualTo(1));
            Assert.That(shot.FrameTime(185), Is.LessThan(shot.to.Value));
            string stem = shot.RangeStem("intro");
            Assert.That(stem, Is.EqualTo("intro_0.000-3.100"));
            Assert.That(Shot.FrameName(stem, 0), Is.EqualTo("intro_0.000-3.100_00000.png"));
            Assert.That(Shot.FrameName(stem, 185), Is.EqualTo("intro_0.000-3.100_00185.png"));
        }

        [Test]
        public void RepeatedResultShotsCanEachPlayTheSameTierUpgrade()
        {
            ResultSample preset = ResultSample.Preset("ap");
            var first = ScreenshotDriver.ToResult(preset).Settlement;
            var second = ScreenshotDriver.ToResult(preset).Settlement;
            Assert.That(first.TierUp && second.TierUp, Is.True);
            Assert.That(first.PlayId, Is.Not.EqualTo(second.PlayId));
            Assert.That(first.After.Total, Is.EqualTo(second.After.Total));
        }

        [TestCase("tier-upgrade", -0.5d, 1.5d, 120)]
        [TestCase("dan-award", 0d, 4d, 240)]
        [TestCase("achievement-unlock", 0d, 2.6d, 156)]
        [TestCase("achievement-card", 0d, 4.6d, 276)]
        [TestCase("special-medals", 0d, 4d, 240)]
        public void OmittedClipRangeUsesAnimationTimeline(string name, double start, double end, int frames)
        {
            var shot = new Shot { kind = "clip", name = name };
            shot.PrepareSequence();
            Assert.That(shot.from, Is.EqualTo(start));
            Assert.That(shot.to, Is.EqualTo(end));
            Assert.That(shot.FrameCount, Is.EqualTo(frames));
        }

        [Test]
        public void At120FpsMusicTimesAlternateEightAndNineMillisecondSteps()
        {
            var shot = new Shot { kind = "live", from = 36.5d, to = 41d, fps = 120 };
            shot.PrepareSequence();
            long previous = 36500;
            for (int i = 1; i < shot.FrameCount; i++)
            {
                long current = (long)Math.Round(1000d * shot.FrameTime(i));
                Assert.That(current - previous, Is.InRange(8L, 9L));
                previous = current;
            }
            Assert.That(previous, Is.EqualTo(40992));
        }

        [TestCase(0, 0, 60)]
        [TestCase(2, 1, 60)]
        [TestCase(-1, 2, 60)]
        [TestCase(0, 1, 0)]
        [TestCase(0, 1, 241)]
        public void InvalidRangeIsRejected(double start, double end, int fps)
        {
            var shot = new Shot { kind = "intro", from = start, to = end, fps = fps };
            Assert.Throws<ArgumentException>(() => shot.PrepareSequence());
        }
    }
}
