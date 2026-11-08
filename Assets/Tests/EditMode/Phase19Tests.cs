using CQRa.Sim.Factions;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase19Tests
    {
        [Test]
        public void FactionCatalog_HasThreeDistinctFactions()
        {
            Assert.AreEqual(3, FactionCatalog.All.Length);
            Assert.AreEqual("Ashfall Compact", FactionCatalog.Name(FactionId.Ashfall));
            Assert.AreEqual("Verdant Combine", FactionCatalog.Name(FactionId.Verdant));
            Assert.AreEqual("Meridian Syndicate", FactionCatalog.Name(FactionId.Meridian));

            var baseline = FactionCatalog.Modifiers(FactionId.Ashfall);
            Assert.AreEqual(1f, baseline.Cost, 0.001f);
            Assert.AreEqual(1f, baseline.Health, 0.001f);

            var verdant = FactionCatalog.Modifiers(FactionId.Verdant);
            var meridian = FactionCatalog.Modifiers(FactionId.Meridian);

            Assert.Less(verdant.Cost, 1f);
            Assert.Greater(verdant.Speed, 1f);
            Assert.Greater(meridian.Cost, 1f);
            Assert.Greater(meridian.Damage, 1f);
            Assert.Greater(meridian.Vision, 1f);
        }

        [Test]
        public void FactionRuntime_MapsTeamsAndCosts()
        {
            try
            {
                FactionRuntime.Set(FactionId.Meridian, FactionId.Verdant);
                Assert.AreEqual(FactionId.Meridian, FactionRuntime.ForTeam(0));
                Assert.AreEqual(FactionId.Verdant, FactionRuntime.ForTeam(1));

                Assert.AreEqual(112, FactionRuntime.CostFor(0, 100));
                Assert.AreEqual(90, FactionRuntime.CostFor(1, 100));
            }
            finally
            {
                FactionRuntime.Reset();
            }

            Assert.AreEqual(FactionId.Ashfall, FactionRuntime.ForTeam(0));
            Assert.AreEqual(100, FactionRuntime.CostFor(0, 100));
        }

        [Test]
        public void DefaultOpponent_IsAlwaysADifferentFaction()
        {
            foreach (var faction in FactionCatalog.All)
            {
                var opponent = FactionCatalog.DefaultOpponent(faction);
                Assert.AreNotEqual(faction, opponent);
            }
        }
    }
}
