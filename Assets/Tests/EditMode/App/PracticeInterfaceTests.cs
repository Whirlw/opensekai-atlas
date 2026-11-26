using System;
using System.Globalization;
using NUnit.Framework;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode
{
    public sealed class PracticeInterfaceTests
    {
        [TestCase("", "", 0d, null)]
        [TestCase("", "3.25", 0d, 3.25d)]
        [TestCase("-0.5", "12", -0.5d, 12d)]
        [TestCase("0.5001", "", 0.5001d, null)]
        public void RangeAcceptsChartSecondsAndOpenEndedSegments(string from, string to, double start, double? end)
        {
            Assert.That(PracticeForm.ReadRange(from, to), Is.EqualTo((start, end)));
        }

        [TestCase("4.2", "4.2")]
        [TestCase("5", "-3")]
        [TestCase("Infinity", "")]
        [TestCase("0", "NaN")]
        public void InvalidRangeCannotLaunch(string from, string to) =>
            Assert.Throws<FormatException>(() => PracticeForm.ReadRange(from, to));

        [Test]
        public void SequenceEditorRoundTripsRatesAndTimedRestsIndependentOfDeviceLocale()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                var template = PracticeForm.ReadSequence("渐快", "0.5×， 2S, 0.7123456X, 0.250000000001s");
                Assert.That(template.Steps.Count, Is.EqualTo(4));
                Assert.That(template.Steps[0].Speed, Is.EqualTo(0.5f));
                Assert.That(template.Steps[1].RestSeconds, Is.EqualTo(2d));
                var restored = PracticeForm.ReadSequence(template.Name, PracticeForm.FormatSequence(template));
                Assert.That(restored.Steps[2].Speed, Is.EqualTo(template.Steps[2].Speed));
                Assert.That(restored.Steps[3].RestSeconds, Is.EqualTo(template.Steps[3].RestSeconds));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("1x,")]
        [TestCase("0x")]
        [TestCase("NaNx")]
        [TestCase("Infinityx")]
        [TestCase("2s")]
        [TestCase("1x, -2s")]
        [TestCase("1x, 0s")]
        [TestCase("1z")]
        public void InvalidSequenceIsRejectedAsAWhole(string sequence) =>
            Assert.Throws<FormatException>(() => PracticeForm.ReadSequence("模板", sequence));

        [Test]
        public void SwipingLeftFromTheLibraryReachesThePracticeHero()
        {
            int library = HomeView.PageOf(HomeDestination.Library);
            Assert.That(HomeView.PageOf(HomeDestination.Profile), Is.EqualTo(library - 1));
            Assert.That(HomeView.PageOf(HomeDestination.Practice), Is.EqualTo(library + 1));
            Assert.That((int)HomeDestination.Practice % 3, Is.Zero);
        }

        private sealed class Area : IPracticeInputArea
        {
            public bool ContainsControl(Vector2 point) => point.y > 100f;
        }

        [Test]
        public void UiGestureRemainsCapturedAfterSeekingAndLaneGesturesRemainPlayable()
        {
            var collector = new LiveInputCollector(4) { PracticeInputArea = new Area() };
            var bar = new Vector2(40, 150);
            var lane = new Vector2(40, 50);
            Assert.That(collector.IsPracticeControlTouch(3, TouchPhase.Began, bar), Is.True);
            Assert.That(collector.IsPracticeControlTouch(4, TouchPhase.Began, lane), Is.False);
            collector.Reset();
            Assert.That(collector.IsPracticeControlTouch(3, TouchPhase.Moved, lane), Is.True);
            Assert.That(collector.IsPracticeControlTouch(4, TouchPhase.Moved, bar), Is.False);
            Assert.That(collector.IsPracticeControlTouch(3, TouchPhase.Ended, lane), Is.True);
            Assert.That(collector.IsPracticeControlTouch(3, TouchPhase.Began, lane), Is.False);
            // First gameplay sample can be a drag if the original press occurred while paused.
            Assert.That(collector.IsPracticeControlTouch(7, TouchPhase.Moved, bar), Is.True);
        }

        [Test]
        public void LiveControlsSubmitExactRateAndSeekAgainstTheFullChart()
        {
            var root = new GameObject("Live");
            try
            {
                var controls = new Controls();
                PracticeLiveControls.Create(root.transform, controls);
                var speed = root.GetComponentInChildren<TMP_InputField>();
                speed.onEndEdit.Invoke("0.75321");
                Assert.That(controls.Rate, Is.EqualTo(0.75321d).Within(1e-6));
                var slider = root.GetComponentInChildren<Slider>(true);
                slider.value = 0.75f;
                Assert.That(controls.PositionSeconds, Is.EqualTo(74d));
                controls.Paused = false;
                slider.value = 0.5f;
                Assert.That(controls.PositionSeconds, Is.EqualTo(74d));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private sealed class Controls : IPracticeControls
        {
            public double PositionSeconds { get; private set; }
            public double MinimumSeconds => -1d;
            public double MaximumSeconds => 99d;
            public double Rate { get; private set; } = 1d;
            public bool Paused { get; set; } = true;
            public bool Resting => false;
            public bool Completed => false;
            public void SetRate(double speed) => Rate = speed;
            public void Seek(double seconds) => PositionSeconds = seconds;
            public void TogglePause() => Paused = !Paused;
            public void Restart() { }
            public void Quit() { }
        }
    }
}
