using System.Collections;
using System.Collections.Generic;
using CQRa.Sim.AI;
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
    public sealed class SkirmishAiPlayModeTests
    {
        [UnityTest]
        public IEnumerator Ai_DeploysMcv_AndBuildsStructures()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 180f, 91, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var economy = CreateEconomy(entityManager, 4000, 4000);

                UnitFactory.Spawn(
                    entityManager, renderMeshArray, 3,
                    UnitMaterials.BaseIndex(1, UnitKind.Mcv),
                    new float3(40f, 0f, 40f), 0f, UnitKind.Mcv, 1);

                CreateCommander(entityManager, 1, AiDifficulty.Hardened);

                yield return new WaitForSeconds(4f);

                Assert.AreNotEqual(
                    Entity.Null,
                    BaseCommands.FindConstructionYard(entityManager, 1),
                    "the AI must deploy its MCV");

                Assert.GreaterOrEqual(
                    CountBuildings(entityManager, 1),
                    2,
                    "the AI should add at least one building after the yard");

                var credits = Economy.GetCredits(
                    entityManager.GetComponentData<EconomyState>(economy), 1);
                Assert.Less(credits, 4000, "the AI must pay normal building costs");
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Ai_StartsProduction()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 180f, 91, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                CreateEconomy(entityManager, 7000, 7000);

                UnitFactory.Spawn(
                    entityManager, renderMeshArray, 3,
                    UnitMaterials.BaseIndex(1, UnitKind.Mcv),
                    new float3(40f, 0f, 40f), 0f, UnitKind.Mcv, 1);

                CreateCommander(entityManager, 1, AiDifficulty.Hardened);

                yield return new WaitForSeconds(8f);

                var produced = CountCombatUnits(entityManager, 1) + CountHarvesters(entityManager, 1);
                var queued = CountQueued(entityManager, 1);

                Assert.IsTrue(
                    produced > 0 || queued > 0,
                    "the AI must queue or complete units through normal production");
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Ai_OrdersArmyToAttackWhenThresholdReached()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 180f, 91, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                CreateEconomy(entityManager, 0, 0);

                BuildingFactory.SpawnBuilding(
                    entityManager, 1, BuildingKind.ConstructionYard, new float3(-40f, 0f, 0f));

                var enemy = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Barracks, float3.zero);

                var tanks = new List<Entity>(5);
                for (var i = 0; i < 5; i++)
                {
                    tanks.Add(UnitFactory.Spawn(
                        entityManager, renderMeshArray, (int)UnitKind.Tank,
                        UnitMaterials.BaseIndex(1, UnitKind.Tank),
                        new float3(-35f + i * 2f, 0f, -10f), 0f, UnitKind.Tank, 1));
                }

                // Hardened attacks at 5 units; zero credits keeps the AI in the military
                // branch instead of spending its think on construction.
                CreateCommander(entityManager, 1, AiDifficulty.Hardened);

                yield return new WaitForSeconds(2f);

                foreach (var tank in tanks)
                {
                    var state = entityManager.GetComponentData<CombatState>(tank);
                    Assert.AreEqual(
                        enemy,
                        state.Target,
                        "the AI must order its gathered army onto the enemy base");
                    Assert.AreEqual(1, state.Explicit);
                }
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        private static void CreateCommander(EntityManager entityManager, int team, AiDifficulty difficulty)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(typeof(AiCommander));
            entityManager.SetComponentData(entity, new AiCommander
            {
                Team = team,
                Difficulty = difficulty,
                ThinkTimer = 0f,
            });
        }

        private static int CountBuildings(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<BuildingInfo>(entities[i]).Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountCombatUnits(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(entities[i]);
                if (info.Team == team &&
                    (info.Kind == UnitKind.Infantry || info.Kind == UnitKind.Tank) &&
                    entityManager.GetComponentData<Health>(entities[i]).Current > 0f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountHarvesters(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(entities[i]);
                if (info.Team == team && info.Kind == UnitKind.Harvester &&
                    entityManager.GetComponentData<Health>(entities[i]).Current > 0f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountQueued(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<ProductionOrder>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<BuildingInfo>(entities[i]).Team != team)
                {
                    continue;
                }

                count += entityManager.GetBuffer<ProductionOrder>(entities[i], true).Length;
            }

            return count;
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

        private static void Cleanup(
            EntityManager entityManager,
            Entity data,
            Material material,
            params Mesh[] meshes)
        {
            DestroyAll<AiCommander>(entityManager);
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
