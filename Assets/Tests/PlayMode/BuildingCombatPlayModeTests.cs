using System.Collections;
using CQRa.Sim.Base;
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
    public sealed class BuildingCombatPlayModeTests
    {
        [UnityTest]
        public IEnumerator Tank_DestroysEnemyBuilding_UnblocksNav_AndWinsMatch()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var plant = BuildingFactory.SpawnBuilding(
                    entityManager, 1, BuildingKind.PowerPlant, new float3(10f, 0f, 0f));

                var navRadius = BuildCatalog.Get(BuildingKind.PowerPlant).NavRadius;
                NavGridQuery.BlockCircle(entityManager, new float2(10f, 0f), navRadius);
                Assert.IsFalse(
                    NavGridQuery.IsWalkable(entityManager, new float2(10f, 0f)),
                    "the building footprint must start blocked");

                var health = entityManager.GetComponentData<Health>(plant);
                health.Current = 40f;
                entityManager.SetComponentData(plant, health);

                var tank = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(-10f, 0f, 0f), 0f, UnitKind.Tank, 0);

                OrderAttack(entityManager, tank, plant);

                for (var i = 0; i < 40 && entityManager.Exists(plant); i++)
                {
                    yield return new WaitForSeconds(0.2f);
                }

                Assert.IsFalse(entityManager.Exists(plant), "the tank must destroy the enemy power plant");
                Assert.IsTrue(
                    NavGridQuery.IsWalkable(entityManager, new float2(10f, 0f)),
                    "destroying a building must free its nav footprint");

                var match = GetMatchState(entityManager);
                Assert.IsTrue(match.HasValue);
                Assert.AreEqual(1, match.Value.GameOver);
                Assert.AreEqual(0, match.Value.Winner, "red should win after the last blue building dies");
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Repair_HealsAndCostsCredits_ThenSellRefundsHalf()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var economy = CreateEconomy(entityManager, 1000, 1000);
                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, float3.zero);

                var navRadius = BuildCatalog.Get(BuildingKind.Barracks).NavRadius;
                NavGridQuery.BlockCircle(entityManager, float2.zero, navRadius);

                var health = entityManager.GetComponentData<Health>(barracks);
                health.Current = 100f;
                entityManager.SetComponentData(barracks, health);

                Assert.IsTrue(
                    BaseCommands.TryToggleRepair(entityManager, barracks, out var failure),
                    $"repair toggle should succeed ({failure})");
                Assert.AreEqual(
                    1,
                    entityManager.GetComponentData<RepairState>(barracks).Active);

                yield return new WaitForSeconds(1f);

                var healed = entityManager.GetComponentData<Health>(barracks).Current;
                var creditsAfterRepair = Economy.GetCredits(
                    entityManager.GetComponentData<EconomyState>(economy), 0);

                Assert.Greater(healed, 100f, "repair must restore health over time");
                Assert.Less(creditsAfterRepair, 1000, "repair must cost credits");

                Assert.IsTrue(BaseCommands.TryToggleRepair(entityManager, barracks, out _));
                Assert.AreEqual(
                    0,
                    entityManager.GetComponentData<RepairState>(barracks).Active,
                    "a second toggle cancels repair");

                Assert.IsTrue(
                    BaseCommands.TrySell(entityManager, barracks, out var refund, out failure),
                    $"selling should succeed ({failure})");
                Assert.AreEqual(200, refund);
                Assert.IsFalse(entityManager.Exists(barracks), "sold buildings are removed");
                Assert.IsTrue(
                    NavGridQuery.IsWalkable(entityManager, float2.zero),
                    "selling must free the nav footprint");

                var creditsAfterSell = Economy.GetCredits(
                    entityManager.GetComponentData<EconomyState>(economy), 0);
                Assert.AreEqual(creditsAfterRepair + 200, creditsAfterSell);

                var yard = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.ConstructionYard, new float3(30f, 0f, 0f));
                Assert.IsFalse(BaseCommands.TrySell(entityManager, yard, out _, out failure));
                Assert.AreEqual(BuildingCommandFailure.CannotSell, failure);
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Repair_StopsWhenTheTeamCannotPay()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                CreateEconomy(entityManager, 0, 0);
                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, new float3(0f, 0f, 0f));

                var health = entityManager.GetComponentData<Health>(barracks);
                health.Current = 100f;
                entityManager.SetComponentData(barracks, health);

                Assert.IsTrue(BaseCommands.TryToggleRepair(entityManager, barracks, out _));

                yield return new WaitForSeconds(0.3f);

                Assert.AreEqual(
                    0,
                    entityManager.GetComponentData<RepairState>(barracks).Active,
                    "repair must stop when credits run out");
                Assert.AreEqual(
                    100f,
                    entityManager.GetComponentData<Health>(barracks).Current,
                    0.01f,
                    "no credits means no healing");
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator FriendlyBuilding_IsNotAttacked()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var barracks = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, new float3(10f, 0f, 0f));

                var tank = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(-10f, 0f, 0f), 0f, UnitKind.Tank, 0);

                yield return new WaitForSeconds(1f);

                Assert.IsTrue(entityManager.Exists(barracks));
                Assert.AreEqual(
                    entityManager.GetComponentData<Health>(barracks).Max,
                    entityManager.GetComponentData<Health>(barracks).Current,
                    0.01f,
                    "units must never attack friendly buildings");
                Assert.AreEqual(
                    Entity.Null,
                    entityManager.GetComponentData<CombatState>(tank).Target,
                    "friendly buildings must not be acquired as targets");
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        private static Mesh[] UnitMeshes()
        {
            return new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
                ProceduralMeshes.Box(new Vector3(5f, 3f, 8f)),
            };
        }

        private static Material[] Materials(Material material)
        {
            var materials = new Material[UnitMaterials.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            return materials;
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

        private static MatchState? GetMatchState(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return null;
            }

            return entityManager.GetComponentData<MatchState>(query.GetSingletonEntity());
        }

        private static void OrderAttack(EntityManager entityManager, Entity attacker, Entity target)
        {
            var state = entityManager.GetComponentData<CombatState>(attacker);
            state.Target = target;
            state.Explicit = 1;
            entityManager.SetComponentData(attacker, state);

            var move = entityManager.GetComponentData<MoveTarget>(attacker);
            move.HasOrder = 0;
            move.Field = Entity.Null;
            entityManager.SetComponentData(attacker, move);
        }

        private static void Cleanup(
            EntityManager entityManager,
            Entity data,
            Material material,
            params Mesh[] meshes)
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

            foreach (var mesh in meshes)
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
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
