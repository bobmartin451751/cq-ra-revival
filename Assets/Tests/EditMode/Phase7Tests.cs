using CQRa.Sim.Base;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase7Tests
    {
        [Test]
        public void SellRefund_IsHalfCost_AndYardCannotBeSold()
        {
            Assert.AreEqual(200, RepairRules.SellRefund(BuildingKind.Barracks));
            Assert.AreEqual(150, RepairRules.SellRefund(BuildingKind.PowerPlant));
            Assert.AreEqual(300, RepairRules.SellRefund(BuildingKind.Refinery));
            Assert.AreEqual(0, RepairRules.SellRefund(BuildingKind.ConstructionYard));

            Assert.IsTrue(RepairRules.CanSell(BuildingKind.Barracks));
            Assert.IsTrue(RepairRules.CanSell(BuildingKind.WarFactory));
            Assert.IsFalse(RepairRules.CanSell(BuildingKind.ConstructionYard));
        }

        [Test]
        public void RepairRules_ArePositive()
        {
            Assert.Greater(RepairRules.HealPerSecond, 0f);
            Assert.Greater(RepairRules.CreditsPerSecond, 0f);

            // Repairing a full health pool must cost a meaningful amount of credits.
            var barracks = BuildCatalog.Get(BuildingKind.Barracks);
            Assert.Greater(barracks.Cost, 0);
            var seconds = 500f / RepairRules.HealPerSecond;
            Assert.Greater(seconds * RepairRules.CreditsPerSecond, 0f);
        }
    }
}
