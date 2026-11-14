using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Tests.PlayMode
{
    public sealed class GarrisonPlayModeTests
    {
        [Test]
        public void Bunker_GarrisonsFiveInfantryAndFiresHarder()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var bunker = BuildingFactory.SpawnBuilding(
                entityManager, 0, BuildingKind.Bunker, float3.zero);

            try
            {
                var baseDamage = entityManager.GetComponentData<DefenseWeapon>(bunker).Damage;

                for (var i = 0; i < 5; i++)
                {
                    var soldier = CreateSoldier(entityManager);
                    Assert.IsTrue(
                        GarrisonCommands.TryEnter(entityManager, bunker, soldier, out var failure),
                        $"garrison {i} failed: {failure}");
                }

                var extra = CreateSoldier(entityManager);
                Assert.IsFalse(
                    GarrisonCommands.TryEnter(entityManager, bunker, extra, out var full),
                    "the sixth soldier must not fit");
                Assert.AreEqual(GarrisonFailure.Full, full);
                Assert.AreEqual(5, entityManager.GetBuffer<CargoUnit>(bunker).Length);
                Assert.Greater(
                    entityManager.GetComponentData<DefenseWeapon>(bunker).Damage,
                    baseDamage,
                    "garrisoned infantry should increase bunker firepower");

                Assert.AreEqual(5, GarrisonCommands.TryEject(entityManager, bunker));
                Assert.AreEqual(baseDamage, entityManager.GetComponentData<DefenseWeapon>(bunker).Damage);
            }
            finally
            {
                if (entityManager.Exists(bunker))
                {
                    entityManager.DestroyEntity(bunker);
                }

                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitTag>());
                if (!query.IsEmptyIgnoreFilter)
                {
                    entityManager.DestroyEntity(query.ToEntityArray(Unity.Collections.Allocator.Temp));
                }
            }
        }

        private static Entity CreateSoldier(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new UnitInfo
            {
                Team = 0,
                Kind = UnitKind.Infantry,
                Radius = 0.6f,
                MaxSpeed = 4f,
            });
            entityManager.AddComponentData(entity, new Health { Max = 50f, Current = 50f });
            entityManager.AddComponentData(
                entity,
                LocalTransform.FromPositionRotationScale(
                    float3.zero,
                    quaternion.identity,
                    1f));
            return entity;
        }
    }
}
