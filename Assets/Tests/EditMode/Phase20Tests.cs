using CQRa.Sim.Base;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase20Tests
    {
        [Test]
        public void EachFaction_HasASeventhUniqueUnit()
        {
            Assert.AreEqual(8, ProductionCatalog.RosterFor(FactionId.Ashfall).Length);
            Assert.AreEqual(7, ProductionCatalog.RosterFor(FactionId.Verdant).Length);
            Assert.AreEqual(7, ProductionCatalog.RosterFor(FactionId.Meridian).Length);

            Assert.AreEqual(UnitKind.Thumper, ProductionCatalog.UniqueFor(FactionId.Ashfall));
            Assert.AreEqual(UnitKind.Mantis, ProductionCatalog.UniqueFor(FactionId.Verdant));
            Assert.AreEqual(UnitKind.Skyfall, ProductionCatalog.UniqueFor(FactionId.Meridian));

            CollectionAssert.Contains(
                ProductionCatalog.RosterFor(FactionId.Ashfall), UnitKind.Thumper);
            CollectionAssert.Contains(
                ProductionCatalog.RosterFor(FactionId.Ashfall), UnitKind.Rig);
            CollectionAssert.DoesNotContain(
                ProductionCatalog.RosterFor(FactionId.Ashfall), UnitKind.Mantis);
            CollectionAssert.Contains(
                ProductionCatalog.RosterFor(FactionId.Meridian), UnitKind.Skyfall);
        }

        [Test]
        public void FactionTroopNames_AreDistinct()
        {
            Assert.AreEqual("Sapper", ProductionCatalog.UnitName(FactionId.Ashfall, UnitKind.Infantry));
            Assert.AreEqual("Bulwark", ProductionCatalog.UnitName(FactionId.Ashfall, UnitKind.Tank));
            Assert.AreEqual("Thorn", ProductionCatalog.UnitName(FactionId.Verdant, UnitKind.Infantry));
            Assert.AreEqual("Stinger", ProductionCatalog.UnitName(FactionId.Verdant, UnitKind.Tank));
            Assert.AreEqual("Aegis", ProductionCatalog.UnitName(FactionId.Meridian, UnitKind.Infantry));
            Assert.AreEqual("Lumen", ProductionCatalog.UnitName(FactionId.Meridian, UnitKind.Tank));
            Assert.AreEqual("Thumper", ProductionCatalog.UnitName(FactionId.Ashfall, UnitKind.Thumper));
        }

        [Test]
        public void UniqueUnits_HaveTheirOwnRoles()
        {
            var thumper = ProductionCatalog.Get(UnitKind.Thumper);
            var mantis = ProductionCatalog.Get(UnitKind.Mantis);
            var skyfall = ProductionCatalog.Get(UnitKind.Skyfall);

            Assert.AreEqual(BuildingKind.WarFactory, thumper.Producer);
            Assert.AreEqual(BuildingKind.Barracks, mantis.Producer);
            Assert.AreEqual(BuildingKind.WarFactory, skyfall.Producer);

            Assert.Greater(thumper.Cost, mantis.Cost);
            Assert.Greater(skyfall.Cost, thumper.Cost);

            Assert.Greater(CombatStats.For(UnitKind.Thumper).Range, 30f);
            Assert.Greater(UnitFactory.SpeedFor(UnitKind.Mantis), 6f);
            Assert.Greater(UnitFactory.SpeedFor(UnitKind.Skyfall), 15f);
        }
    }
}
