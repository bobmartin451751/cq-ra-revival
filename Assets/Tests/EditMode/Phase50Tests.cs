using CQRa.Sim.Campaign;
using CQRa.Sim.Factions;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase50Tests
    {
        [Test]
        public void HeroProgress_PersistsKills()
        {
            try
            {
                HeroProgress.Clear(FactionId.Ashfall);
                Assert.AreEqual(0, HeroProgress.Kills(FactionId.Ashfall));

                HeroProgress.Save(FactionId.Ashfall, 7);
                Assert.AreEqual(7, HeroProgress.Kills(FactionId.Ashfall));

                HeroProgress.Clear(FactionId.Ashfall);
                Assert.AreEqual(0, HeroProgress.Kills(FactionId.Ashfall));
            }
            finally
            {
                HeroProgress.Clear(FactionId.Ashfall);
            }
        }
    }
}
