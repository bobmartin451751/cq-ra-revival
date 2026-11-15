using CQRa.Sim.Base;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase42Tests
    {
        [Test]
        public void EachFaction_HasTheSharedBarracksSpecialists()
        {
            foreach (var faction in FactionCatalog.All)
            {
                var roster = ProductionCatalog.RosterFor(faction);
                CollectionAssert.Contains(roster, UnitKind.AntiTank);
                CollectionAssert.Contains(roster, UnitKind.AntiAir);
                CollectionAssert.Contains(roster, UnitKind.Grenadier);
                CollectionAssert.Contains(roster, UnitKind.Medic);
            }

            CollectionAssert.Contains(
                ProductionCatalog.RosterFor(FactionId.Ashfall), UnitKind.Breacher);
            CollectionAssert.Contains(
                ProductionCatalog.RosterFor(FactionId.Meridian), UnitKind.Marksman);
            CollectionAssert.Contains(
                ProductionCatalog.RosterFor(FactionId.Verdant), UnitKind.Mantis);
        }

        [Test]
        public void SpecialistTargeting_IsCorrect()
        {
            Assert.AreEqual(TargetClass.Air, AirCatalog.DefaultTargeting(UnitKind.AntiAir));
            Assert.AreEqual(TargetClass.None, AirCatalog.DefaultTargeting(UnitKind.Medic));
            Assert.AreEqual(TargetClass.Ground, AirCatalog.DefaultTargeting(UnitKind.AntiTank));
            Assert.AreEqual(TargetClass.Ground, AirCatalog.DefaultTargeting(UnitKind.Grenadier));
            Assert.AreEqual(TargetClass.Ground, AirCatalog.DefaultTargeting(UnitKind.Marksman));
        }

        [Test]
        public void Specialists_AreBarracksUnits()
        {
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.AntiTank).Producer);
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.AntiAir).Producer);
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.Grenadier).Producer);
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.Medic).Producer);
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.Breacher).Producer);
            Assert.AreEqual(BuildingKind.Barracks, ProductionCatalog.Get(UnitKind.Marksman).Producer);
        }
    }
}
