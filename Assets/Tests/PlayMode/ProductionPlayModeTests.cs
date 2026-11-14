using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using CQRa.View;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class ProductionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Barracks_ProducesInfantry_AndSendsItToRally()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            try
            {
                var economy = CreateEconomy(entityManager, 1000, 1000);
                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, float3.zero);

                var rally = new float3(20f, 0f, 6f);
                entityManager.SetComponentData(barracks, new RallyPoint
                {
                    Value = rally,
                    HasValue = 1,
                });

                Assert.IsTrue(
                    ProductionCommands.TryEnqueue(
                        entityManager, BuildCatalog.PlayerTeam, UnitKind.Infantry, Entity.Null, out var failure),
                    $"enqueue should succeed ({failure})");

                Assert.AreEqual(
                    900,
                    Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0),
                    "the infantry cost is paid at enqueue time");

                // Skip the build timer so the test stays quick; the completion path is
                // what matters here.
                var queue = entityManager.GetBuffer<ProductionOrder>(barracks);
                Assert.AreEqual(1, queue.Length);
                var order = queue[0];
                order.Remaining = 0.05f;
                queue[0] = order;

                yield return new WaitForSeconds(0.5f);

                using var unitQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<MoveTarget>(),
                    ComponentType.ReadOnly<LocalTransform>());
                using var units = unitQuery.ToEntityArray(Allocator.Temp);

                var produced = Entity.Null;
                foreach (var unit in units)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (info.Team == 0 && info.Kind == UnitKind.Infantry)
                    {
                        produced = unit;
                        break;
                    }
                }

                Assert.AreNotEqual(Entity.Null, produced, "the barracks must spawn an infantryman");
                Assert.AreEqual(0, entityManager.GetBuffer<ProductionOrder>(barracks).Length, "queue is consumed");

                var move = entityManager.GetComponentData<MoveTarget>(produced);
                Assert.AreNotEqual(0, move.HasOrder, "the new unit must receive a rally order");
                Assert.Less(
                    math.distance(move.Destination, rally),
                    2f,
                    "the rally destination should be the building's rally point");
            }
            finally
            {
                Cleanup(entityManager, data, material);
            }
        }

        [UnityTest]
        public IEnumerator Dock_ProducesShipsInsideItsWaterBody()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 240f, 121, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            MapRuntime.Select(0);

            try
            {
                var body = MapWater.Body(0);
                var dockPosition = new float3(
                    body.Centre.x + body.Radius + 5f,
                    0f,
                    body.Centre.y);
                CreateEconomy(entityManager, 5000, 5000);
                var dock = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Dock, dockPosition);

                Assert.IsTrue(
                    entityManager.HasComponent<NavalDock>(dock),
                    "a shoreline dock records its water body");

                Assert.IsTrue(
                    ProductionCommands.TryEnqueue(
                        entityManager, 0, UnitKind.PatrolBoat, dock, out var failure),
                    $"dock should queue a patrol boat ({failure})");

                var queue = entityManager.GetBuffer<ProductionOrder>(dock);
                var order = queue[0];
                order.Remaining = 0.05f;
                queue[0] = order;

                yield return new WaitForSeconds(0.6f);

                using var unitQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<NavalUnit>(),
                    ComponentType.ReadOnly<LocalTransform>());
                using var units = unitQuery.ToEntityArray(Allocator.Temp);

                var produced = Entity.Null;
                foreach (var unit in units)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (info.Team == 0 && info.Kind == UnitKind.PatrolBoat)
                    {
                        produced = unit;
                        break;
                    }
                }

                Assert.AreNotEqual(Entity.Null, produced, "the dock must launch a patrol boat");

                var naval = entityManager.GetComponentData<NavalUnit>(produced);
                var position = entityManager.GetComponentData<LocalTransform>(produced).Position;

                Assert.AreEqual(0, naval.BodyIndex);
                Assert.AreEqual(body.Level, position.y, 0.01f);
                Assert.IsTrue(
                    MapWater.Contains(0, new float2(position.x, position.z)),
                    "the launched ship must start inside the dock's water");
            }
            finally
            {
                MapRuntime.Reset();
                Cleanup(entityManager, data, material);
            }
        }

        [UnityTest]
        public IEnumerator Production_RejectsMissingBuildingCreditsAndFullQueue()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            try
            {
                var economy = CreateEconomy(entityManager, 5000, 5000);

                Assert.IsFalse(
                    ProductionCommands.TryEnqueue(
                        entityManager, 0, UnitKind.Infantry, Entity.Null, out var failure),
                    "no barracks means no infantry");
                Assert.AreEqual(ProductionFailure.NoBuilding, failure);

                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, float3.zero);

                var state = entityManager.GetComponentData<EconomyState>(economy);
                Economy.SetCredits(ref state, 0, 50);
                entityManager.SetComponentData(economy, state);

                Assert.IsFalse(
                    ProductionCommands.TryEnqueue(
                        entityManager, 0, UnitKind.Infantry, Entity.Null, out failure),
                    "50 credits cannot buy a 100-credit infantryman");
                Assert.AreEqual(ProductionFailure.NotEnoughCredits, failure);
                Assert.AreEqual(50, Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));

                state = entityManager.GetComponentData<EconomyState>(economy);
                Economy.SetCredits(ref state, 0, 5000);
                entityManager.SetComponentData(economy, state);

                for (var i = 0; i < ProductionCatalog.QueueLimit; i++)
                {
                    Assert.IsTrue(
                        ProductionCommands.TryEnqueue(
                            entityManager, 0, UnitKind.Infantry, barracks, out failure),
                        $"queue slot {i} should accept an order ({failure})");
                }

                Assert.IsFalse(
                    ProductionCommands.TryEnqueue(entityManager, 0, UnitKind.Infantry, barracks, out failure),
                    "the queue must have a hard limit");
                Assert.AreEqual(ProductionFailure.QueueFull, failure);
            }
            finally
            {
                Cleanup(entityManager, data, material);
            }
        }

        [UnityTest]
        public IEnumerator Production_FallsBackToAnotherProducerWithRoom()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            try
            {
                CreateEconomy(entityManager, 5000, 5000);

                var first = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, new float3(-12f, 0f, 0f));
                var second = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, new float3(12f, 0f, 0f));

                for (var i = 0; i < ProductionCatalog.QueueLimit; i++)
                {
                    Assert.IsTrue(ProductionCommands.TryEnqueue(
                        entityManager, 0, UnitKind.Infantry, first, out _));
                }

                Assert.AreEqual(
                    ProductionCatalog.QueueLimit,
                    entityManager.GetBuffer<ProductionOrder>(first).Length);

                Assert.IsTrue(
                    ProductionCommands.TryEnqueue(
                        entityManager, 0, UnitKind.Infantry, first, out var failure),
                    $"a full selected barracks must fall back to another ({failure})");

                Assert.AreEqual(
                    1,
                    entityManager.GetBuffer<ProductionOrder>(second).Length,
                    "the second barracks should receive the order");
                Assert.AreEqual(
                    ProductionCatalog.QueueLimit,
                    entityManager.GetBuffer<ProductionOrder>(first).Length,
                    "the full queue must be left untouched");
            }
            finally
            {
                Cleanup(entityManager, data, material);
            }
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

        private static void Cleanup(EntityManager entityManager, Entity data, Material material)
        {
            DestroyAll<UnitTag>(entityManager);
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<OreNode>(entityManager);
            DestroyAll<EconomyState>(entityManager);
            DestroyAll<MatchState>(entityManager);

            if (entityManager.Exists(data))
            {
                entityManager.DestroyEntity(data);
            }

            Object.DestroyImmediate(material);
        }

        private static void DestroyAll<T>(EntityManager entityManager)
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
