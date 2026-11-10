using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class SowerMinePlayModeTests
    {
        [UnityTest]
        public IEnumerator Sower_LaysMine_ThatDamagesEnemies()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var sower = entityManager.CreateEntity();
            var enemy = entityManager.CreateEntity();

            try
            {
                entityManager.AddComponent<UnitTag>(sower);
                entityManager.AddComponentData(sower, new UnitInfo
                {
                    Team = 0,
                    Kind = UnitKind.Sower,
                    Radius = 3f,
                    MaxSpeed = 7f,
                });
                entityManager.AddComponentData(sower, new Health { Max = 180f, Current = 180f });
                entityManager.AddComponentData(sower, new MineLayer
                {
                    Cooldown = 4f,
                    CooldownRemaining = 0f,
                });
                entityManager.AddComponentData(
                    sower,
                    LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

                entityManager.AddComponent<UnitTag>(enemy);
                entityManager.AddComponentData(enemy, new UnitInfo
                {
                    Team = 1,
                    Kind = UnitKind.Infantry,
                    Radius = 0.6f,
                    MaxSpeed = 4f,
                });
                entityManager.AddComponentData(enemy, new Health { Max = 50f, Current = 50f });
                entityManager.AddComponentData(
                    enemy,
                    LocalTransform.FromPositionRotationScale(
                        new float3(2f, 0f, 0f), quaternion.identity, 1f));

                Assert.IsTrue(BaseCommands.TryLayMine(entityManager, sower));
                Assert.Greater(
                    entityManager.GetComponentData<MineLayer>(sower).CooldownRemaining,
                    0f);

                yield return new WaitForSeconds(0.3f);

                using var mines = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Mine>());
                using var mineEntities = mines.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(0, mineEntities.Length, "the mine should detonate");

                var damaged = !entityManager.Exists(enemy) ||
                              entityManager.GetComponentData<Health>(enemy).Current < 50f;
                Assert.IsTrue(damaged, "the blast should damage the enemy");
            }
            finally
            {
                if (entityManager.Exists(sower))
                {
                    entityManager.DestroyEntity(sower);
                }

                if (entityManager.Exists(enemy))
                {
                    entityManager.DestroyEntity(enemy);
                }

                using var mines = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Mine>());
                if (!mines.IsEmptyIgnoreFilter)
                {
                    var entities = mines.ToEntityArray(Allocator.Temp);
                    entityManager.DestroyEntity(entities);
                    entities.Dispose();
                }
            }
        }
    }
}
