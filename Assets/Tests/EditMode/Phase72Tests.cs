using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase72Tests
    {
        [Test]
        public void EachFactionHasItsOwnSuperweapon()
        {
            Assert.AreEqual(
                SuperweaponKind.ArtilleryBarrage,
                SuperweaponCatalog.ForFaction(FactionId.Ashfall));
            Assert.AreEqual(
                SuperweaponKind.SporeBloom,
                SuperweaponCatalog.ForFaction(FactionId.Verdant));
            Assert.AreEqual(
                SuperweaponKind.OrbitalLance,
                SuperweaponCatalog.ForFaction(FactionId.Meridian));

            Assert.AreNotEqual(
                SuperweaponCatalog.DisplayName(SuperweaponKind.ArtilleryBarrage),
                SuperweaponCatalog.DisplayName(SuperweaponKind.SporeBloom));
            Assert.AreNotEqual(
                SuperweaponCatalog.DisplayName(SuperweaponKind.SporeBloom),
                SuperweaponCatalog.DisplayName(SuperweaponKind.OrbitalLance));
        }

        [Test]
        public void SuperweaponIsBuildableAndCostsPower()
        {
            var spec = BuildCatalog.Get(BuildingKind.Superweapon);
            Assert.Greater(spec.Cost, 0);
            Assert.Greater(spec.PowerUsed, 0);

            var found = false;
            for (var i = 0; i < BuildCatalog.PlayerBuildable.Length; i++)
            {
                found |= BuildCatalog.PlayerBuildable[i] == BuildingKind.Superweapon;
            }

            Assert.IsTrue(found, "the superweapon must appear in the build menu");
            Assert.AreEqual(
                "Superweapon",
                BuildCatalog.DisplayName(BuildingKind.Superweapon));
        }

        [Test]
        public void SuperweaponStatsAreDistinct()
        {
            Assert.Greater(
                SuperweaponCatalog.Pulses(SuperweaponKind.ArtilleryBarrage), 1);
            Assert.Greater(
                SuperweaponCatalog.Pulses(SuperweaponKind.SporeBloom), 1);
            Assert.AreEqual(1, SuperweaponCatalog.Pulses(SuperweaponKind.OrbitalLance));
            Assert.Greater(
                SuperweaponCatalog.Damage(SuperweaponKind.OrbitalLance),
                SuperweaponCatalog.Damage(SuperweaponKind.ArtilleryBarrage));
            Assert.Greater(
                SuperweaponCatalog.Radius(SuperweaponKind.SporeBloom),
                SuperweaponCatalog.Radius(SuperweaponKind.OrbitalLance));
        }
    }
}
