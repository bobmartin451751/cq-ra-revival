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
    public sealed class EngineerCapturePlayModeTests
    {
        [UnityTest]
        public IEnumerator Engineer_CapturesEnemyBuilding_AndIsConsumed()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 100f, 51, 1f);

            try
            {
                var engineer = CreateUnit(entityManager, 0, UnitKind.Engineer, new float3(0f, 0f, 0f));
                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 1, BuildingKind.Barracks, new float3(5f, 0f, 0f));

                var queue = entityManager.GetBuffer<ProductionOrder>(barracks);
                queue.Add(new ProductionOrder
                {
                    Kind = UnitKind.Infantry,
                    Remaining = 2f,
                    Total = 4f,
                });

                Assert.IsTrue(
                    BaseCommands.TryCaptureBuilding(
                        entityManager, engineer, barracks, out var failure),
                    $"capture should succeed ({failure})");

                Assert.IsFalse(entityManager.Exists(engineer), "the engineer is consumed");
                Assert.AreEqual(
                    0,
                    entityManager.GetComponentData<BuildingInfo>(barracks).Team,
                    "the building changes owner");
                Assert.AreEqual(
                    0,
                    entityManager.GetBuffer<ProductionOrder>(barracks).Length,
                    "captured production queues are cleared");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        [UnityTest]
        public IEnumerator Engineer_TooFar_IsRejected()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 100f, 51, 1f);

            try
            {
                var engineer = CreateUnit(entityManager, 0, UnitKind.Engineer, float3.zero);
                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 1, BuildingKind.Barracks, new float3(30f, 0f, 0f));

                Assert.IsFalse(
                    BaseCommands.TryCaptureBuilding(
                        entityManager, engineer, barracks, out var failure));
                Assert.AreEqual(CaptureFailure.TooFar, failure);
                Assert.IsTrue(entityManager.Exists(engineer));
                Assert.AreEqual(1, entityManager.GetComponentData<BuildingInfo>(barracks).Team);
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        private static Entity CreateUnit(
            EntityManager entityManager,
            int team,
            UnitKind kind,
            float3 position)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponent<UnitTag>(entity);
            entityManager.AddComponentData(entity, new UnitInfo
            {
                Team = team,
                Kind = kind,
                Radius = 0.6f,
                MaxSpeed = 5f,
            });
            entityManager.AddComponentData(entity, new Health { Max = 40f, Current = 40f });
            entityManager.AddComponentData(
                entity,
                LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private static void Cleanup(EntityManager entityManager, Entity data)
        {
            DestroyAll<UnitTag>(entityManager);
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<Crate>(entityManager);
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
