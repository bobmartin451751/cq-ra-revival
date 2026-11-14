using System.Collections.Generic;
using CQRa.Sim.Base;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase39Tests
    {
        [Test]
        public void EachFaction_HasFourUniqueAircraft()
        {
            var factions = new[] { FactionId.Ashfall, FactionId.Verdant, FactionId.Meridian };
            var seen = new HashSet<UnitKind>();

            foreach (var faction in factions)
            {
                var aircraft = 0;
                foreach (var kind in ProductionCatalog.RosterFor(faction))
                {
                    if (!AirCatalog.IsAircraft(kind))
                    {
                        continue;
                    }

                    aircraft++;
                    Assert.IsTrue(seen.Add(kind), $"{kind} is duplicated across factions");
                    Assert.AreEqual(
                        BuildingKind.Airfield,
                        ProductionCatalog.Get(kind).Producer,
                        $"{kind} must be built at the Airfield");
                }

                Assert.AreEqual(4, aircraft, $"{faction} needs four aircraft");
            }

            Assert.AreEqual(12, seen.Count);
        }

        [Test]
        public void TransportCapacities_MatchTheDesign()
        {
            Assert.IsTrue(AirCatalog.TryGetCargoRules(
                UnitKind.Gunship, out var infantry, out var tanks, out var withTank, out _));
            Assert.AreEqual(5, infantry);
            Assert.AreEqual(0, tanks);
            Assert.AreEqual(0, withTank);

            Assert.IsTrue(AirCatalog.TryGetCargoRules(
                UnitKind.AshfallTransport, out infantry, out tanks, out withTank, out _));
            Assert.AreEqual(20, infantry);
            Assert.AreEqual(1, tanks);
            Assert.AreEqual(5, withTank);
        }

        [Test]
        public void NewAirBuildings_AreInTheCatalog()
        {
            Assert.Greater(BuildCatalog.Get(BuildingKind.Airfield).Cost, 0);
            Assert.Greater(BuildCatalog.Get(BuildingKind.Airfield).PowerUsed, 0);
            Assert.Greater(BuildCatalog.Get(BuildingKind.AntiAir).Cost, 0);
            Assert.AreEqual(
                "AA Gun",
                BuildCatalog.DisplayName(BuildingKind.AntiAir));
        }
        [Test]
        public void AntiAir_IsAirOnlyAndNotStrongerThanAGunTower()
        {
            Assert.Less(DefenseCatalog.AntiAirDamage, DefenseCatalog.GunTowerDamage);
            Assert.Greater(DefenseCatalog.AntiAirRange, DefenseCatalog.GunTowerRange);
            Assert.Less(DefenseCatalog.AntiAirCooldown, 1f);
            Assert.AreEqual(
                TargetClass.Air,
                AirCatalog.DefaultTargeting(UnitKind.AshfallFighter),
                "fighters are air-to-air");
            Assert.AreEqual(
                TargetClass.None,
                AirCatalog.DefaultTargeting(UnitKind.AshfallTransport),
                "transport helicopters are unarmed");
        }
    }
}
