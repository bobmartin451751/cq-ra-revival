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
    public sealed class ResearchPlayModeTests
    {
        [UnityTest]
        public IEnumerator WeaponsResearch_BoostsExistingAndNewUnits()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            using (var economyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>()))
            {
                var economy = economyQuery.IsEmptyIgnoreFilter
                    ? entityManager.CreateEntity(typeof(EconomyState))
                    : economyQuery.GetSingletonEntity();
                entityManager.SetComponentData(economy, EconomyState.Create(5000));
            }
            var tank = UnitFactory.SpawnDefault(
                entityManager, float3.zero, 0f, UnitKind.Tank, 0);
            var newTank = Entity.Null;
            var researchEntity = Entity.Null;

            try
            {
                var before = entityManager.GetComponentData<Weapon>(tank).Damage;
                Assert.IsTrue(
                    ResearchCommands.TryStart(
                        entityManager, 0, UpgradeKind.Weapons, out var failure),
                    $"research should start: {failure}");

                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ResearchState>());
                var researchEntities = query.ToEntityArray(Allocator.Temp);
                Assert.GreaterOrEqual(researchEntities.Length, 1);
                researchEntity = researchEntities[0];
                researchEntities.Dispose();
                var state = entityManager.GetComponentData<ResearchState>(researchEntity);
                state.SetTimer(0, 0f);
                entityManager.SetComponentData(researchEntity, state);

                yield return new WaitForSeconds(0.3f);

                state = entityManager.GetComponentData<ResearchState>(researchEntity);
                Assert.IsTrue(state.Has(0, UpgradeKind.Weapons));
                Assert.Greater(
                    entityManager.GetComponentData<Weapon>(tank).Damage,
                    before,
                    "existing units should gain the weapon upgrade");

                newTank = UnitFactory.SpawnDefault(
                    entityManager, new float3(10f, 0f, 0f), 0f, UnitKind.Tank, 0);
                Assert.Greater(
                    entityManager.GetComponentData<Weapon>(newTank).Damage,
                    before,
                    "new units should spawn with the upgrade");
            }
            finally
            {
                if (entityManager.Exists(tank))
                {
                    entityManager.DestroyEntity(tank);
                }

                if (newTank != Entity.Null && entityManager.Exists(newTank))
                {
                    entityManager.DestroyEntity(newTank);
                }

                if (researchEntity != Entity.Null && entityManager.Exists(researchEntity))
                {
                    entityManager.DestroyEntity(researchEntity);
                }
            }
        }
    }
}
