using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class ThumperDeployPlayModeTests
    {
        [UnityTest]
        public IEnumerator Thumper_DeployTogglesRangeAndCooldown()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();

            try
            {
                entityManager.AddComponentData(entity, new DeployState
                {
                    Deployed = 0,
                    NormalRange = 34f,
                    DeployedRange = 47.6f,
                    NormalCooldown = 3f,
                    DeployedCooldown = 4.5f,
                });
                entityManager.AddComponentData(entity, new Weapon
                {
                    Damage = 70f,
                    Range = 34f,
                    Cooldown = 3f,
                    CooldownRemaining = 0f,
                    AcquireRange = 36f,
                });
                entityManager.AddComponentData(entity, new MoveTarget
                {
                    Destination = float3.zero,
                    PathDestination = float3.zero,
                    HasOrder = 1,
                });
                entityManager.AddComponentData(
                    entity,
                    LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

                Assert.IsTrue(BaseCommands.TryToggleDeploy(entityManager, entity));
                Assert.AreEqual(1, entityManager.GetComponentData<DeployState>(entity).Deployed);
                Assert.AreEqual(47.6f, entityManager.GetComponentData<Weapon>(entity).Range, 0.01f);
                Assert.AreEqual(4.5f, entityManager.GetComponentData<Weapon>(entity).Cooldown, 0.01f);
                Assert.AreEqual(0, entityManager.GetComponentData<MoveTarget>(entity).HasOrder);

                Assert.IsTrue(BaseCommands.TryToggleDeploy(entityManager, entity));
                Assert.AreEqual(0, entityManager.GetComponentData<DeployState>(entity).Deployed);
                Assert.AreEqual(34f, entityManager.GetComponentData<Weapon>(entity).Range, 0.01f);
                Assert.AreEqual(3f, entityManager.GetComponentData<Weapon>(entity).Cooldown, 0.01f);
            }
            finally
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }
            }
        }
    }
}
