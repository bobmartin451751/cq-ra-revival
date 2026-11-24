using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class SuperweaponPlayModeTests
    {
        [Test]
        public void ChargedSuperweapon_DamagesEnemies_AndNeedsRecharge()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Verdant });

            var building = BuildingFactory.SpawnBuilding(
                entityManager, 0, BuildingKind.Superweapon, float3.zero);
            var target = BuildingFactory.SpawnBuilding(
                entityManager, 1, BuildingKind.Barracks, new float3(12f, 0f, 0f));

            try
            {
                var state = entityManager.GetComponentData<SuperweaponState>(building);
                state.ChargeSeconds = 10f;
                state.Charge = 10f;
                state.Ready = 1;
                entityManager.SetComponentData(building, state);

                var before = entityManager.GetComponentData<Health>(target).Current;
                Assert.IsTrue(SuperweaponCommands.TryFire(
                    entityManager, building, new float3(12f, 0f, 0f), out var failure), failure);

                var afterFire = entityManager.GetComponentData<SuperweaponState>(building);
                Assert.AreEqual(0f, afterFire.Charge, 0.001f);
                Assert.AreEqual(0, afterFire.Ready);

                Assert.IsFalse(SuperweaponCommands.TryFire(
                    entityManager, building, new float3(12f, 0f, 0f), out _),
                    "an uncharged superweapon must reject a second shot");

                var system = world.GetExistingSystemManaged<SuperweaponSystem>();
                system.Update();

                var after = entityManager.GetComponentData<Health>(target).Current;
                Assert.Less(after, before, "the first barrage pulse must deal damage");

                using var strikes = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<SuperweaponStrike>());
                Assert.IsFalse(strikes.IsEmptyIgnoreFilter,
                    "the strike keeps pulsing for its remaining pulses");
            }
            finally
            {
                DestroyAll<SuperweaponStrike>(entityManager);
                DestroyAll<BuildingTag>(entityManager);
                DestroyAll<UnitTag>(entityManager);
                FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Ashfall });
            }
        }

        private static void DestroyAll<T>(EntityManager entityManager) where T : struct, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
            entities.Dispose();
        }
    }
}
