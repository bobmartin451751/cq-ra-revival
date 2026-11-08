using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Navigation;
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
    public sealed class CratePlayModeTests
    {
        [UnityTest]
        public IEnumerator CreditCrate_IsCollected_AndRemoved()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 100f, 51, 1f);

            try
            {
                var economy = CreateEconomy(entityManager, 100, 100);
                CreateUnit(entityManager, 0, new float3(5f, 0f, 0f));
                var crate = BuildingFactory.SpawnCrate(
                    entityManager, new float3(5f, 0f, 0f), 250, 0);

                yield return new WaitForSeconds(0.3f);

                Assert.IsFalse(entityManager.Exists(crate), "the crate must be consumed");
                Assert.AreEqual(
                    350,
                    Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0),
                    "credit crates pay the collector's team");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        [UnityTest]
        public IEnumerator RepairCrate_HealsTheCollector()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 100f, 51, 1f);

            try
            {
                CreateEconomy(entityManager, 100, 100);
                var unit = CreateUnit(entityManager, 0, new float3(-5f, 0f, 0f));
                var health = entityManager.GetComponentData<Health>(unit);
                health.Current = 20f;
                entityManager.SetComponentData(unit, health);

                var crate = BuildingFactory.SpawnCrate(
                    entityManager, new float3(-5f, 0f, 0f), 80, 1);

                yield return new WaitForSeconds(0.3f);

                Assert.IsFalse(entityManager.Exists(crate));
                Assert.AreEqual(
                    50f,
                    entityManager.GetComponentData<Health>(unit).Current,
                    0.01f,
                    "repair crates heal but never exceed max health");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        private static Entity CreateUnit(EntityManager entityManager, int team, float3 position)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponent<UnitTag>(entity);
            entityManager.AddComponentData(entity, new UnitInfo
            {
                Team = team,
                Kind = UnitKind.Infantry,
                Radius = 0.6f,
                MaxSpeed = 4f,
            });
            entityManager.AddComponentData(entity, new Health { Max = 50f, Current = 50f });
            entityManager.AddComponentData(
                entity,
                LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private static Entity CreateEconomy(EntityManager entityManager, int team0, int team1)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            var entity = query.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(EconomyState))
                : query.GetSingletonEntity();

            entityManager.SetComponentData(entity, new EconomyState
            {
                Team0Credits = team0,
                Team1Credits = team1,
            });

            return entity;
        }

        private static void Cleanup(EntityManager entityManager, Entity data)
        {
            DestroyAll<UnitTag>(entityManager);
            DestroyAll<Crate>(entityManager);
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<OreNode>(entityManager);
            DestroyAll<EconomyState>(entityManager);
            DestroyAll<MatchState>(entityManager);
            DestroyAll<CQRa.Sim.Fog.FogState>(entityManager);

            if (entityManager.Exists(data))
            {
                entityManager.DestroyEntity(data);
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

        private static Entity CreateFlatWorld(EntityManager entityManager, float size, int resolution, float cellSize)
        {
            var entity = entityManager.CreateEntity();
            var cells = Mathf.CeilToInt(size / cellSize);

            entityManager.AddComponentData(entity, new TerrainInfo { Resolution = resolution, Size = size });

            var heights = entityManager.AddBuffer<TerrainHeightSample>(entity);
            heights.ResizeUninitialized(resolution * resolution);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = new TerrainHeightSample { Height = 0f };
            }

            entityManager.AddComponentData(entity, new NavGridInfo
            {
                Origin = new float2(-size * 0.5f, -size * 0.5f),
                Size = new int2(cells, cells),
                CellSize = cellSize,
                MaxSlope = 2f,
            });

            var nav = entityManager.AddBuffer<NavCell>(entity);
            nav.ResizeUninitialized(cells * cells);
            for (var i = 0; i < nav.Length; i++)
            {
                nav[i] = new NavCell { Walkable = 1 };
            }

            return entity;
        }
    }
}
