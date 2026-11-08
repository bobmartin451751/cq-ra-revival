using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase21Tests
    {
        [Test]
        public void Skyfall_DealsDoubleDamageToAir()
        {
            Assert.AreEqual(
                CombatMath.AirSuperiorityBonus,
                CombatMath.AirSuperiorityMultiplier(UnitKind.Skyfall, true),
                0.001f);

            Assert.AreEqual(
                1f,
                CombatMath.AirSuperiorityMultiplier(UnitKind.Skyfall, false),
                0.001f);

            Assert.AreEqual(
                1f,
                CombatMath.AirSuperiorityMultiplier(UnitKind.Gunship, true),
                0.001f);
        }

        [Test]
        public void FactionRosters_IncludeUniqueUnits()
        {
            Assert.AreEqual(UnitKind.Thumper, CQRa.Sim.Base.ProductionCatalog.UniqueFor(CQRa.Sim.Factions.FactionId.Ashfall));
            Assert.AreEqual(UnitKind.Mantis, CQRa.Sim.Base.ProductionCatalog.UniqueFor(CQRa.Sim.Factions.FactionId.Verdant));
            Assert.AreEqual(UnitKind.Skyfall, CQRa.Sim.Base.ProductionCatalog.UniqueFor(CQRa.Sim.Factions.FactionId.Meridian));
        }
    }
}
