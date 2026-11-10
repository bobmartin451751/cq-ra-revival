using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase5Tests
    {
        [Test]
        public void BuildCatalog_HasSaneCostsAndPower()
        {
            var power = BuildCatalog.Get(BuildingKind.PowerPlant);
            var refinery = BuildCatalog.Get(BuildingKind.Refinery);
            var yard = BuildCatalog.Get(BuildingKind.ConstructionYard);

            Assert.Greater(power.Cost, 0);
            Assert.Greater(power.PowerProduced, 0);
            Assert.AreEqual(0, power.PowerUsed);

            Assert.Greater(refinery.Cost, power.Cost);
            Assert.Greater(refinery.PowerUsed, 0);
            Assert.AreEqual(0, refinery.PowerProduced);

            Assert.AreEqual(0, yard.Cost, "the yard is deployed, not purchased");
            Assert.Greater(yard.PowerProduced, 0);
            Assert.Greater(BuildCatalog.BuildRadius, 0f);
            Assert.AreEqual(
                7,
                BuildCatalog.PlayerBuildable.Length,
                "power, refinery, barracks, factory, tower, wall and naval yard are buildable");
        }

        [Test]
        public void BuildRules_RadiusAndOverlap()
        {
            Assert.IsTrue(BuildRules.IsWithinBuildRadius(new float2(0f, 0f), new float2(0f, 0f), 45f));
            Assert.IsTrue(BuildRules.IsWithinBuildRadius(new float2(45f, 0f), new float2(0f, 0f), 45f));
            Assert.IsFalse(BuildRules.IsWithinBuildRadius(new float2(45.1f, 0f), new float2(0f, 0f), 45f));

            Assert.IsTrue(BuildRules.FootprintsOverlap(new float2(0f, 0f), 9f, new float2(10f, 0f), 6f));
            Assert.IsFalse(BuildRules.FootprintsOverlap(new float2(0f, 0f), 9f, new float2(16f, 0f), 6f));
        }

        [Test]
        public void Economy_SpendAndAfford()
        {
            var state = new EconomyState { Team0Credits = 500, Team1Credits = 100 };

            Assert.IsTrue(BuildRules.CanAfford(500, 300));
            Assert.IsFalse(BuildRules.CanAfford(200, 300));

            Assert.IsTrue(Economy.TrySpend(ref state, 0, 300));
            Assert.AreEqual(200, Economy.GetCredits(state, 0));

            Assert.IsFalse(Economy.TrySpend(ref state, 0, 300), "cannot spend more than the balance");
            Assert.AreEqual(200, Economy.GetCredits(state, 0));

            Economy.AddCredits(ref state, 1, 250);
            Assert.AreEqual(350, Economy.GetCredits(state, 1));
            Assert.AreEqual(200, Economy.GetCredits(state, 0), "teams are independent");
        }

        [Test]
        public void Mcv_IsUnarmedHeavyVehicle()
        {
            var mcv = CombatStats.For(UnitKind.Mcv);
            var harvester = CombatStats.For(UnitKind.Harvester);

            Assert.IsFalse(mcv.IsArmed);
            Assert.Greater(mcv.MaxHealth, harvester.MaxHealth);
            Assert.Greater(mcv.Armor, 0f);
            Assert.Greater(UnitFactory.RadiusFor(UnitKind.Mcv), UnitFactory.RadiusFor(UnitKind.Harvester));
            Assert.AreEqual(13, UnitMaterials.KindCount, "all base, unique, support and naval unit kinds are renderable");
        }

        [Test]
        public void HarvesterTuning_IsSane()
        {
            Assert.Greater(Economy.CargoCapacity, 0);
            Assert.Greater(Economy.HarvestRate, 0f);
            Assert.Greater(Economy.CargoCapacity / Economy.HarvestRate, 5f, "a load should take real time");
        }
    }
}
