using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase6Tests
    {
        [Test]
        public void ProductionCatalog_MapsKindsToProducers()
        {
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.Infantry).Producer);
            Assert.AreEqual(BuildingKind.WarFactory, ProductionCatalog.Get(UnitKind.Tank).Producer);
            Assert.AreEqual(BuildingKind.WarFactory, ProductionCatalog.Get(UnitKind.Harvester).Producer);
            Assert.AreEqual(BuildingKind.WarFactory, ProductionCatalog.Get(UnitKind.Mcv).Producer);

            Assert.AreEqual(5, ProductionCatalog.Buildable.Length);
            Assert.Greater(ProductionCatalog.QueueLimit, 0);
        }

        [Test]
        public void ProductionCatalog_CostsAndTimesAreSane()
        {
            foreach (var kind in ProductionCatalog.Buildable)
            {
                var spec = ProductionCatalog.Get(kind);
                Assert.Greater(spec.Cost, 0, $"{kind} must cost credits");
                Assert.Greater(spec.Seconds, 0f, $"{kind} must take time");
                Assert.IsFalse(string.IsNullOrEmpty(spec.Label));
            }

            // Vehicles cost more and take longer than infantry.
            var infantry = ProductionCatalog.Get(UnitKind.Infantry);
            var tank = ProductionCatalog.Get(UnitKind.Tank);
            Assert.Greater(tank.Cost, infantry.Cost);
            Assert.Greater(tank.Seconds, infantry.Seconds);

            // MCV is the most expensive rebuild option.
            Assert.Greater(
                ProductionCatalog.Get(UnitKind.Mcv).Cost,
                ProductionCatalog.Get(UnitKind.Tank).Cost);
        }

        [Test]
        public void ProductionBuildings_HaveCostPowerAndFootprint()
        {
            var barracks = BuildCatalog.Get(BuildingKind.Barracks);
            var factory = BuildCatalog.Get(BuildingKind.WarFactory);

            Assert.Greater(barracks.Cost, 0);
            Assert.Greater(barracks.PowerUsed, 0);
            Assert.Greater(barracks.NavRadius, 0f);

            Assert.Greater(factory.Cost, barracks.Cost);
            Assert.Greater(factory.PowerUsed, barracks.PowerUsed);
            Assert.Greater(factory.NavRadius, barracks.NavRadius);

            Assert.AreEqual("Barracks", BuildCatalog.DisplayName(BuildingKind.Barracks));
            Assert.AreEqual("War Factory", BuildCatalog.DisplayName(BuildingKind.WarFactory));
        }

        [Test]
        public void ProductionHotkeys_AreUnique()
        {
            Assert.AreEqual("Z", ProductionCatalog.Hotkey(0));
            Assert.AreEqual("X", ProductionCatalog.Hotkey(1));
            Assert.AreEqual("C", ProductionCatalog.Hotkey(2));
            Assert.AreEqual("V", ProductionCatalog.Hotkey(3));
        }
    }
}
