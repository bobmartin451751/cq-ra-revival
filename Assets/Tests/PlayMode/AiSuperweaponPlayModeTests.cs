using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class AiSuperweaponPlayModeTests
    {
        [Test]
        public void AiFiresReadySuperweapon_AtVisibleEnemyStructure()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Verdant });

            var launcher = BuildingFactory.SpawnBuilding(
                entityManager, 1, BuildingKind.Superweapon, new float3(0f, 0f, 0f));
            var target = BuildingFactory.SpawnBuilding(
                entityManager, 0, BuildingKind.Barracks, new float3(60f, 0f, 0f));

            try
            {
                var state = entityManager.GetComponentData<SuperweaponState>(launcher);
                state.Charge = state.ChargeSeconds;
                state.Ready = 1;
                entityManager.SetComponentData(launcher, state);

                Assert.IsTrue(AiSystem.TryFireSuperweapon(entityManager, 1),
                    "a charged AI superweapon must fire at a visible enemy");

                Assert.AreEqual(
                    0f,
                    entityManager.GetComponentData<SuperweaponState>(launcher).Charge,
                    0.001f,
                    "firing must consume the charge");

                using var strikes = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<SuperweaponStrike>());
                Assert.IsFalse(strikes.IsEmptyIgnoreFilter,
                    "firing must create a strike entity");

                Assert.IsFalse(AiSystem.TryFireSuperweapon(entityManager, 1),
                    "an uncharged superweapon must not fire again");
            }
            finally
            {
                DestroyAll<SuperweaponStrike>(entityManager);
                DestroyAll<BuildingTag>(entityManager);
                DestroyAll<UnitTag>(entityManager);
                FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Ashfall });
            }
        }

        [Test]
        public void AiDoesNotFireWithoutEnemy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            FactionRuntime.SetAll(new[] { FactionId.Ashfall, FactionId.Ashfall });

            var launcher = BuildingFactory.SpawnBuilding(
                entityManager, 1, BuildingKind.Superweapon, float3.zero);

            try
            {
                var state = entityManager.GetComponentData<SuperweaponState>(launcher);
                state.Charge = state.ChargeSeconds;
                state.Ready = 1;
                entityManager.SetComponentData(launcher, state);

                Assert.IsFalse(AiSystem.TryFireSuperweapon(entityManager, 1));
                Assert.IsTrue(entityManager.GetComponentData<SuperweaponState>(launcher).Ready != 0,
                    "no target means the charge is retained");
            }
            finally
            {
                DestroyAll<SuperweaponStrike>(entityManager);
                DestroyAll<BuildingTag>(entityManager);
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
