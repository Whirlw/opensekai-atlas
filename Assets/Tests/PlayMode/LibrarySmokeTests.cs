using System.Collections;
using NUnit.Framework;
using OpenSekai.App.UI;
using Sekai;
using UnityEngine;
using SceneLoader = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    /// <summary>
    /// The only integration check: does the library scene actually assemble itself?
    /// Everything below this (charts, scoring, packages) is covered by the EditMode suites.
    /// </summary>
    public class LibrarySmokeTests
    {
        private const string LibraryScenePath = "Assets/Game/Scenes/Library.unity";
        private const float BootTimeoutSeconds = 15f;

        [UnityTest]
        public IEnumerator LibrarySceneBootsAndShowsThePage()
        {
            AsyncOperation load = SceneLoader.LoadSceneAsync(LibraryScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "the library scene must exist at " + LibraryScenePath);

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            LibraryScreen screen = null;

            while (Time.realtimeSinceStartup < deadline)
            {
                screen = Object.FindObjectOfType<LibraryScreen>();
                if (screen != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(screen, Is.Not.Null, "LibraryScreen was not created within " + BootTimeoutSeconds + " s");
            Assert.That(ScreenManager.ExistsInstance, Is.True, "ScreenManager should be alive after boot");
        }
    }
}
