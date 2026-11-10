using System.Collections;
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
    public sealed class AiStartupPlayModeTests
    {
        [UnityTest]
        public IEnumerator Ai_DeploysAndBuilds_OnEveryDifficulty()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 180f, 181, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);

            try
            {
                foreach (AiDifficulty difficulty in System.Enum.GetValues(typeof(AiDifficulty)))
                {
                    ClearRuntime(entityManager);
                    ResetNav(entityManager, data);

                    CreateEconomy(entityManager, 5000, 5000);

                    UnitFactory.Spawn(
                        entityManager, renderMeshArray, 3,
                        UnitMaterials.BaseIndex(1, UnitKind.Mcv),
                        new float3(40f, 0f, 20f), 0f, UnitKind.Mcv, 1);

                    for (var i = 0; i < 4; i++)
                    {
                        UnitFactory.Spawn(
                            entityManager, renderMeshArray, 0,
                            UnitMaterials.BaseIndex(1, UnitKind.Infantry),
                            new float3(36f + i * 2f, 0f, 20f), 0f, UnitKind.Infantry, 1);
                    }

                    SkirmishCommands.CreateCommander(entityManager, 1, difficulty);

                    yield return new WaitForSeconds(6f);

                    Assert.AreNotEqual(
                        Entity.Null,
                        BaseCommands.FindConstructionYard(entityManager, 1),
                        $"{difficulty}: AI must deploy its MCV even with a friendly crowd");

                    Assert.GreaterOrEqual(
                        CountBuildings(entityManager, 1),
                        2,
                        $"{difficulty}: AI must add at least one building after the yard");
                }
            }
            finally
            {
                ClearRuntime(entityManager);

                if (entityManager.Exists(data))
                {
                    entityManager.DestroyEntity(data);
                }

                foreach (var mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }

                Object.DestroyImmediate(material);
            }
        }

        private static void ClearRuntime(EntityManager entityManager)
        {
            DestroyAll<UnitTag>(entityManager);
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<AiCommander>(entityManager);
            DestroyAll<EconomyState>(entityManager);
            DestroyAll<MatchState>(entityManager);
            DestroyAll<CQRa.Sim.Fog.FogState>(entityManager);
            DestroyAll<CQRa.Sim.Navigation.FlowFieldMeta>(entityManager);
        }

        private static void ResetNav(EntityManager entityManager, Entity data)
        {
            var nav = entityManager.GetBuffer<NavCell>(data);
            for (var i = 0; i < nav.Length; i++)
            {
                nav[i] = new NavCell { Walkable = 1 };
            }
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

        private static Entity CreateEconomy(EntityManager entityManager, int team0, int team1)
        {
            var entity = entityManager.CreateEntity(typeof(EconomyState));
            entityManager.SetComponentData(entity, new EconomyState
            {
                Team0Credits = team0,
                Team1Credits = team1,
            });

            return entity;
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

        private static Mesh[] Meshes()
        {
            return new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
                ProceduralMeshes.Box(new Vector3(5f, 3f, 8f)),
                ProceduralMeshes.Box(new Vector3(4f, 1f, 8f)),
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
