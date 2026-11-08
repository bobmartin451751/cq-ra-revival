using CQRa.Sim.Base;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase11Tests
    {
        [Test]
        public void GunTower_IsAffordableDefensiveStructure()
        {
            var tower = BuildCatalog.Get(BuildingKind.GunTower);
            Assert.Greater(tower.Cost, 0);
            Assert.Greater(tower.PowerUsed, 0);
            Assert.Greater(tower.Radius, 0f);
            Assert.AreEqual("Gun Tower", BuildCatalog.DisplayName(BuildingKind.GunTower));

            Assert.Greater(DefenseCatalog.GunTowerDamage, 0f);
            Assert.Greater(DefenseCatalog.GunTowerRange, 0f);
            Assert.Greater(DefenseCatalog.GunTowerCooldown, 0f);
        }

        [Test]
        public void Wall_IsCheapAndSmallerThanBuildings()
        {
            var wall = BuildCatalog.Get(BuildingKind.Wall);
            var barracks = BuildCatalog.Get(BuildingKind.Barracks);

            Assert.Greater(wall.Cost, 0);
            Assert.Less(wall.Cost, barracks.Cost);
            Assert.AreEqual(0, wall.PowerUsed);
            Assert.Less(wall.Radius, barracks.Radius);
            Assert.Less(wall.NavRadius, barracks.NavRadius);
            Assert.AreEqual("Wall", BuildCatalog.DisplayName(BuildingKind.Wall));
        }

        [Test]
        public void WallAndTower_AreSellableAndRepairableBuildings()
        {
            Assert.IsTrue(RepairRules.CanSell(BuildingKind.Wall));
            Assert.IsTrue(RepairRules.CanSell(BuildingKind.GunTower));
            Assert.AreEqual(25, RepairRules.SellRefund(BuildingKind.Wall));
            Assert.AreEqual(250, RepairRules.SellRefund(BuildingKind.GunTower));
        }
    }
}
