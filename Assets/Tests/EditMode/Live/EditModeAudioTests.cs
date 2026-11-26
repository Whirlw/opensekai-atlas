using CriWare;
using NUnit.Framework;
using Sekai;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class EditModeAudioTests
    {
        [Test]
        public void ARegisteredHoldCueCannotStartAnAudioRuntimeOutsidePlayMode()
        {
            var sound = new SoundManager();
            var clip = AudioClip.Create("edit-mode-hold", 64, 1, 48000, false);
            try
            {
                sound.RegisterExternalAudioClip("test", clip.name, clip);
                Assert.That(sound.ExistsCueName(clip.name), Is.True);
                Assert.That(sound.PlayHoldLoop(clip.name), Is.Zero);
                Assert.That(CriAtomAudioRuntime.Play(clip, 1f, 0f).id, Is.Zero);
                Assert.That(CriAtomPlugin.IsLibraryInitialized(), Is.False);
                Assert.That(GameObject.Find("CriAtomAudioRuntime"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
