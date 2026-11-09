using NUnit.Framework;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>
    /// Proves the test assembly is wired up: the runner finds it, the game assemblies resolve and
    /// a test can fail when it should. Everything else in this folder builds on this.
    /// </summary>
    public class SmokeTests
    {
        [Test]
        public void EditModeAssemblyLoads()
        {
            Assert.Pass();
        }

        [Test]
        public void GameAssemblyIsReferenced()
        {
            // Sekai.ColorUtility is decompiled code we keep using, so it is a cheap probe that
            // OpenSekai.Game is actually referenced by the test assembly.
            Assert.That(typeof(Sekai.ColorUtility).Assembly.GetName().Name, Is.EqualTo("OpenSekai.Game"));
        }

        [Test]
        public void AssertionsCanFail()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
            Assert.That(() => Assert.That(1, Is.EqualTo(2)), Throws.TypeOf<AssertionException>());
        }
    }
}
