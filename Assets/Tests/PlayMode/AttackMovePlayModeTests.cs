using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class AttackMovePlayModeTests
    {
        [Test]
        public void AttackMove_SetsFlagAndAcquiresTargetsWhileMoving()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var tank = UnitFactory.SpawnDefault(
                entityManager, new float3(0f, 0f, 0f), 0f, UnitKind.Tank, 0);
            var enemy = UnitFactory.SpawnDefault(
                entityManager, new float3(18f, 0f, 0f), 0f, UnitKind.Infantry, 1);

            try
            {
                var units = new NativeList<Entity>(1, Allocator.Temp);
                units.Add(tank);
                Assert.IsTrue(MoveOrderIssuer.TryIssue(
                    entityManager, units, new float3(40f, 0f, 0f), out _, true));
                units.Dispose();

                var move = entityManager.GetComponentData<MoveTarget>(tank);
                Assert.AreEqual(1, move.HasOrder);
                Assert.AreEqual(1, move.AttackMove);

                world.GetExistingSystemManaged<CombatSystem>().Update();

                var state = entityManager.GetComponentData<CombatState>(tank);
                Assert.AreNotEqual(Entity.Null, state.Target,
                    "an attack-moving unit must acquire a visible enemy");

                var normal = new NativeList<Entity>(1, Allocator.Temp);
                normal.Add(tank);
                Assert.IsTrue(MoveOrderIssuer.TryIssue(
                    entityManager, normal, new float3(40f, 0f, 0f), out _));
                normal.Dispose();
                Assert.AreEqual(0, entityManager.GetComponentData<MoveTarget>(tank).AttackMove);
            }
            finally
            {
                DestroyAll<UnitTag>(entityManager);
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
