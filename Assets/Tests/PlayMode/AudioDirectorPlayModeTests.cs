using System;
using CQRa.Core;
using NUnit.Framework;
using UnityEngine;

namespace CQRa.Tests.PlayMode
{
    public sealed class AudioDirectorPlayModeTests
    {
        [Test]
        public void AudioDirector_HasClipsForEveryCue()
        {
            var go = new GameObject("AudioDirectorTest");
            go.AddComponent<AudioDirector>();

            try
            {
                var cueCount = Enum.GetValues(typeof(AudioCue)).Length;
                Assert.AreEqual(cueCount, AudioDirector.ClipCount);

                Assert.IsTrue(AudioDirector.HasCue(AudioCue.AckMove));
                Assert.IsTrue(AudioDirector.HasCue(AudioCue.AckAttack));
                Assert.IsTrue(AudioDirector.HasCue(AudioCue.AckBuild));
                Assert.IsTrue(AudioDirector.HasCue(AudioCue.Announcer));
                Assert.IsTrue(AudioDirector.HasCue(AudioCue.Superweapon));
                Assert.IsTrue(AudioDirector.HasCue(AudioCue.HeroAbility));

                AudioDirector.Play(AudioCue.Announcer);
                AudioDirector.Play(AudioCue.Superweapon);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
