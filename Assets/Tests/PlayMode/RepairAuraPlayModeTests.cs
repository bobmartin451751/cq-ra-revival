using System.Collections;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class RepairAuraPlayModeTests
    {
        [UnityTest]
        public IEnumerator Rig_HealsNearbyMechanicalUnits()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var rig = entityManager.CreateEntity();
            var tank = entityManager.CreateEntity();

            try
            {
                entityManager.AddComponent<UnitTag>(rig);
                entityManager.AddComponentData(rig, new UnitInfo
                {
                    Team = 0,
                    Kind = UnitKind.Rig,
                    Radius = 3.2f,
                    MaxSpeed = 6f,
                });
                entityManager.AddComponentData(rig, new Health { Max = 260f, Current = 260f });
                entityManager.AddComponentData(rig, new RepairAura
                {
                    Radius = 10f,
                    HealPerSecond = 10f,
                });
                entityManager.AddComponentData(
                    rig,
                    LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

                entityManager.AddComponent<UnitTag>(tank);
                entityManager.AddComponentData(tank, new UnitInfo
                {
                    Team = 0,
                    Kind = UnitKind.Tank,
                    Radius = 3.4f,
                    MaxSpeed = 9f,
                });
                entityManager.AddComponentData(tank, new Health { Max = 200f, Current = 100f });
                entityManager.AddComponentData(
                    tank,
                    LocalTransform.FromPositionRotationScale(
                        new float3(5f, 0f, 0f), quaternion.identity, 1f));

                yield return new WaitForSeconds(0.5f);

                var health = entityManager.GetComponentData<Health>(tank).Current;
                Assert.Greater(health, 100f, "the Rig should repair nearby vehicles");
                Assert.LessOrEqual(health, 200f, "repair never exceeds max health");
            }
            finally
            {
                if (entityManager.Exists(rig))
                {
                    entityManager.DestroyEntity(rig);
                }

                if (entityManager.Exists(tank))
                {
                    entityManager.DestroyEntity(tank);
                }
            }
        }
    }
}
