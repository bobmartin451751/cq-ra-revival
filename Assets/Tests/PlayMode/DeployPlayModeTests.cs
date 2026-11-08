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
    public sealed class DeployPlayModeTests
    {
        [UnityTest]
        public IEnumerator DeployAtChosenSite_CreatesYardThere()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 160f, 81, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var mcv = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 3,
                    UnitMaterials.BaseIndex(0, UnitKind.Mcv),
                    new float3(-20f, 0f, 0f), 0f, UnitKind.Mcv, 0);

                Assert.IsTrue(
                    BaseCommands.TryDeployMcvAt(
                        entityManager, mcv, new float3(10f, 0f, 4f), out var failure),
                    $"deploy should succeed ({failure})");

                Assert.IsFalse(entityManager.Exists(mcv));
                var yard = BaseCommands.FindConstructionYard(entityManager, 0);
                Assert.AreNotEqual(Entity.Null, yard);

                var position = entityManager.GetComponentData<LocalTransform>(yard).Position;
                Assert.Less(math.distance(position, new float3(10f, 0f, 4f)), 0.5f);
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator DeployAtOccupiedSite_IsRejected_AndKeepsMcv()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 160f, 81, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var mcv = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 3,
                    UnitMaterials.BaseIndex(0, UnitKind.Mcv),
                    new float3(-20f, 0f, 0f), 0f, UnitKind.Mcv, 0);

                var blocker = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0,
                    UnitMaterials.BaseIndex(0, UnitKind.Infantry),
                    new float3(5f, 0f, 0f), 0f, UnitKind.Infantry, 0);

                Assert.IsFalse(
                    BaseCommands.TryDeployMcvAt(
                        entityManager, mcv, new float3(5f, 0f, 0f), out var failure));
                Assert.AreEqual(BuildFailure.Occupied, failure);
                Assert.IsTrue(entityManager.Exists(mcv), "a rejected deploy must keep the MCV");
                Assert.IsTrue(entityManager.Exists(blocker));
                Assert.AreEqual(
                    Entity.Null,
                    BaseCommands.FindConstructionYard(entityManager, 0));
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator DeployTooFar_IsRejected()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 200f, 101, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var mcv = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 3,
                    UnitMaterials.BaseIndex(0, UnitKind.Mcv),
                    float3.zero, 0f, UnitKind.Mcv, 0);

                Assert.IsFalse(
                    BaseCommands.TryDeployMcvAt(
                        entityManager, mcv, new float3(90f, 0f, 0f), out var failure));
                Assert.AreEqual(BuildFailure.TooFar, failure);
                Assert.IsTrue(entityManager.Exists(mcv));
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
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
            DestroyAll<CQRa.Sim.Fog.FogState>(entityManager);

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
