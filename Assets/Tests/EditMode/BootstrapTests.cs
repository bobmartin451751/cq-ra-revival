using NUnit.Framework;
using CQRa.Core;

namespace CQRa.Tests.EditMode
{
    public sealed class BootstrapTests
    {
        [Test]
        public void SimRate_IsThirtyHertz()
        {
            Assert.AreEqual(30, GameBootstrap.SimHz);
        }

        [Test]
        public void FixedTimestep_MatchesSimRate()
        {
            const float expected = 1f / 30f;
            Assert.AreEqual(expected, 1f / GameBootstrap.SimHz, 1e-7f);
        }
    }
}
