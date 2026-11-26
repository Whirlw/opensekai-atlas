using NUnit.Framework;
using OpenSekai.Diagnostics;
using OpenSekai.Shared.Diagnostics;
using UnityEngine;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class DiagnosticsLifecycleTests
    {
        [Test]
        public void HiddenHudKeepsItsLifecycleAndFollowsBothSwitches()
        {
            var hud = Object.FindFirstObjectByType<PerformanceHud>(FindObjectsInactive.Include);
            Assert.That(hud, Is.Not.Null);
            bool trace = LiveTrace.Enabled;
            bool preference = PerformanceHud.Enabled;
            try
            {
                LiveTrace.Enabled = false;
                PerformanceHud.Enabled = true;
                Assert.That(hud.gameObject.activeInHierarchy, Is.True, "even a never-shown HUD must receive OnDestroy");
                Assert.That(hud.isActiveAndEnabled, Is.False);
                LiveTrace.Enabled = true;
                Assert.That(hud.isActiveAndEnabled, Is.True);
                PerformanceHud.Enabled = false;
                Assert.That(hud.isActiveAndEnabled, Is.False);
            }
            finally
            {
                PerformanceHud.Enabled = preference;
                LiveTrace.Enabled = trace;
            }
        }
    }
}
