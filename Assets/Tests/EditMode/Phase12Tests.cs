using CQRa.Sim.Base;
using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase12Tests
    {
        [Test]
        public void Gunship_IsBuiltAtTheWarFactory()
        {
            var spec = ProductionCatalog.Get(UnitKind.Gunship);
            Assert.AreEqual(BuildingKind.WarFactory, spec.Producer);
            Assert.Greater(spec.Cost, 0);
            Assert.Greater(spec.Seconds, 0f);
            Assert.AreEqual("Gunship", spec.Label);

            var factory = ProductionCatalog.Get(UnitKind.Tank);
            Assert.Greater(spec.Cost, factory.Cost, "aircraft should cost more than a tank");
        }

        [Test]
        public void Gunship_IsFastFragileAndArmed()
        {
            var gunship = CombatStats.For(UnitKind.Gunship);
            var tank = CombatStats.For(UnitKind.Tank);

            Assert.IsTrue(gunship.IsArmed);
            Assert.Less(gunship.MaxHealth, tank.MaxHealth);
            Assert.Greater(UnitFactory.SpeedFor(UnitKind.Gunship), UnitFactory.SpeedFor(UnitKind.Tank));
            Assert.Greater(gunship.Range, 0f);
            Assert.Greater(gunship.Damage, 0f);
        }

        [Test]
        public void Gunship_ScoutsFurtherThanGroundUnits()
        {
            Assert.Greater(VisionCatalog.ForUnit(UnitKind.Gunship), VisionCatalog.ForUnit(UnitKind.Tank));
            Assert.Greater(UnitMaterials.KindCount, 4);
        }
    }
}
