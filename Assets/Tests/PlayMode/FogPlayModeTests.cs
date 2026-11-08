using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Fog;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class FogPlayModeTests
    {
        [UnityTest]
        public IEnumerator Fog_RevealsAroundUnits_AndKeepsExploredCells()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);

            try
            {
                var scout = CreateVisionUnit(entityManager, 0, float3.zero, 32f);

                yield return new WaitForSeconds(0.4f);

                Assert.IsTrue(
                    FogQuery.TryGet(entityManager, 0, out _, out _),
                    "the fog system must create a grid per team");
                Assert.IsTrue(
                    FogQuery.IsVisible(entityManager, 0, float3.zero),
                    "the scout's own position must be visible");
                Assert.IsFalse(
                    FogQuery.IsVisible(entityManager, 0, new float3(60f, 0f, 0f)),
                    "distant cells must not be visible");
                Assert.IsFalse(
                    FogQuery.IsExplored(entityManager, 0, new float3(60f, 0f, 0f)),
                    "unvisited cells must stay unexplored");

                var transform = entityManager.GetComponentData<LocalTransform>(scout);
                transform.Position = new float3(60f, 0f, 0f);
                entityManager.SetComponentData(scout, transform);

                yield return new WaitForSeconds(0.4f);

                Assert.IsTrue(
                    FogQuery.IsVisible(entityManager, 0, new float3(60f, 0f, 0f)),
                    "fog must follow moving units");
                Assert.IsFalse(
                    FogQuery.IsVisible(entityManager, 0, float3.zero),
                    "the old position is no longer visible");
                Assert.IsTrue(
                    FogQuery.IsExplored(entityManager, 0, float3.zero),
                    "the old position must remain explored");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        [UnityTest]
        public IEnumerator Fog_HidesEnemiesOutsidePlayerVision()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);

            try
            {
                CreateVisionUnit(entityManager, 0, float3.zero, 30f);
                var enemy = CreateVisionUnit(entityManager, 1, new float3(80f, 0f, 0f), 30f);

                yield return new WaitForSeconds(0.5f);

                Assert.IsTrue(
                    entityManager.HasComponent<DisableRendering>(enemy),
                    "enemies outside player vision must be hidden");

                var transform = entityManager.GetComponentData<LocalTransform>(enemy);
                transform.Position = new float3(6f, 0f, 0f);
                entityManager.SetComponentData(enemy, transform);

                yield return new WaitForSeconds(0.5f);

                Assert.IsFalse(
                    entityManager.HasComponent<DisableRendering>(enemy),
                    "enemies inside player vision must be rendered");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        private static Entity CreateVisionUnit(
            EntityManager entityManager,
            int team,
            float3 position,
            float vision)
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
            entityManager.AddComponentData(entity, new Health
            {
                Max = 50f,
                Current = 50f,
            });
            entityManager.AddComponentData(entity, new VisionSource { Radius = vision });
            entityManager.AddComponentData(
                entity,
                LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private static void Cleanup(EntityManager entityManager, Entity data)
        {
            DestroyAll<UnitTag>(entityManager);
            DestroyAll<FogState>(entityManager);
            DestroyAll<MatchState>(entityManager);
            DestroyAll<EconomyState>(entityManager);

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
