using CQRa.Sim.AI;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class AiRetreatPlayModeTests
    {
        [Test]
        public void ArmyNeedsRetreat_WhenBadlyWounded()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var tank = UnitFactory.SpawnDefault(
                entityManager, new float3(0f, 0f, 0f), 0f, UnitKind.Tank, 1);
            var infantry = UnitFactory.SpawnDefault(
                entityManager, new float3(6f, 0f, 0f), 0f, UnitKind.Infantry, 1);

            try
            {
                Assert.IsFalse(AiSystem.ArmyNeedsRetreat(entityManager, 1),
                    "full-health units should keep attacking");

                Wound(entityManager, tank, 0.10f);
                Wound(entityManager, infantry, 0.10f);
                Assert.IsTrue(AiSystem.ArmyNeedsRetreat(entityManager, 1),
                    "an army below 30% health should retreat");

                Wound(entityManager, tank, 0.90f);
                Wound(entityManager, infantry, 0.90f);
                Assert.IsFalse(AiSystem.ArmyNeedsRetreat(entityManager, 1));
            }
            finally
            {
                DestroyAll<UnitTag>(entityManager);
            }
        }

        private static void Wound(EntityManager entityManager, Entity unit, float fraction)
        {
            var health = entityManager.GetComponentData<Health>(unit);
            health.Current = health.Max * fraction;
            entityManager.SetComponentData(unit, health);
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
