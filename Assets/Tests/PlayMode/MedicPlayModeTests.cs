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
    public sealed class MedicPlayModeTests
    {
        [UnityTest]
        public IEnumerator Medic_HealsNearbyInfantry()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var medic = UnitFactory.SpawnDefault(
                entityManager, float3.zero, 0f, UnitKind.Medic, 0);
            var soldier = UnitFactory.SpawnDefault(
                entityManager, new float3(3f, 0f, 0f), 0f, UnitKind.Infantry, 0);
            var health = entityManager.GetComponentData<Health>(soldier);
            health.Current = 20f;
            entityManager.SetComponentData(soldier, health);

            try
            {
                yield return new WaitForSeconds(1.2f);

                Assert.Greater(
                    entityManager.GetComponentData<Health>(soldier).Current,
                    20f,
                    "the medic aura should heal nearby infantry");
            }
            finally
            {
                if (entityManager.Exists(medic))
                {
                    entityManager.DestroyEntity(medic);
                }

                if (entityManager.Exists(soldier))
                {
                    entityManager.DestroyEntity(soldier);
                }
            }
        }
    }
}
