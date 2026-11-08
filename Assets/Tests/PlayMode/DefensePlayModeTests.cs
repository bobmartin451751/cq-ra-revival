using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Fog;
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
    public sealed class DefensePlayModeTests
    {
        [UnityTest]
        public IEnumerator GunTower_KillsVisibleEnemyInRange()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);

            try
            {
                BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.GunTower, float3.zero);

                var enemy = CreateMinimalUnit(entityManager, 1, new float3(15f, 0f, 0f));

                yield return new WaitForSeconds(3f);

                Assert.IsFalse(
                    entityManager.Exists(enemy),
                    "a gun tower must kill a visible enemy infantryman in range");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        [UnityTest]
        public IEnumerator GunTower_DoesNotFireThroughFog()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);

            try
            {
                BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.GunTower, float3.zero);

                // In weapon range (28 m) but outside the tower's 24 m vision.
                var enemy = CreateMinimalUnit(entityManager, 1, new float3(26f, 0f, 0f));

                yield return new WaitForSeconds(1.5f);

                Assert.IsTrue(entityManager.Exists(enemy), "fogged enemies must survive");
                Assert.AreEqual(
                    50f,
                    entityManager.GetComponentData<Health>(enemy).Current,
                    0.01f,
                    "the tower must not shoot into the shroud");

                var transform = entityManager.GetComponentData<LocalTransform>(enemy);
                transform.Position = new float3(18f, 0f, 0f);
                entityManager.SetComponentData(enemy, transform);

                yield return new WaitForSeconds(3f);

                Assert.IsFalse(
                    entityManager.Exists(enemy),
                    "once visible the tower must engage");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        [UnityTest]
        public IEnumerator Wall_BlocksNav_AndSellsForRefund()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);

            try
            {
                var economy = CreateEconomy(entityManager, 1000, 1000);
                BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.ConstructionYard, new float3(-20f, 0f, 0f));

                Assert.IsTrue(
                    BaseCommands.TryPlaceBuilding(
                        entityManager, 0, BuildingKind.Wall, float3.zero, out var failure),
                    $"wall placement should succeed ({failure})");

                Assert.AreEqual(
                    950,
                    Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));
                Assert.IsFalse(
                    NavGridQuery.IsWalkable(entityManager, float2.zero),
                    "a wall must block navigation");

                var wall = FindBuilding(entityManager, 0, BuildingKind.Wall);
                Assert.AreNotEqual(Entity.Null, wall);

                Assert.IsTrue(
                    BaseCommands.TrySell(entityManager, wall, out var refund, out var sellFailure),
                    $"wall sale should succeed ({sellFailure})");
                Assert.AreEqual(25, refund);
                Assert.AreEqual(
                    975,
                    Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));
                Assert.IsTrue(
                    NavGridQuery.IsWalkable(entityManager, float2.zero),
                    "selling a wall must free its nav cells");
            }
            finally
            {
                Cleanup(entityManager, data);
            }
        }

        private static Entity CreateMinimalUnit(EntityManager entityManager, int team, float3 position)
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

        private static Entity FindBuilding(EntityManager entityManager, int team, BuildingKind kind)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(entities[i]);
                if (info.Team == team && info.Kind == kind)
                {
                    return entities[i];
                }
            }

            return Entity.Null;
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
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<OreNode>(entityManager);
            DestroyAll<EconomyState>(entityManager);
            DestroyAll<MatchState>(entityManager);
            DestroyAll<FogState>(entityManager);

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
