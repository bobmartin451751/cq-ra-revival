using System.Collections;
using CQRa.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_CreatesSingleton_AndSetsFixedTimestep()
        {
            var go = new GameObject("TestBootstrap");
            go.AddComponent<GameBootstrap>();

            yield return null;

            Assert.IsNotNull(GameBootstrap.Instance, "Bootstrap singleton was not created");
            Assert.AreEqual(1f / GameBootstrap.SimHz, Time.fixedDeltaTime, 1e-5f);

            Object.DestroyImmediate(go);
            yield return null;
        }
    }
}
