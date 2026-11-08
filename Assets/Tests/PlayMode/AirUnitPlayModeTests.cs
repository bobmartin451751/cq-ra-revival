using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Fog;
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
    public sealed class AirUnitPlayModeTests
    {
        [UnityTest]
        public IEnumerator Gunship_FliesOverBlockedTerrain()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 141, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);

                // A solid wall of impassable cells at x = 0.
                for (var z = 40; z <= 100; z++)
                {
                    SetWalkable(entityManager, data, 70, z, 0);
                }

                var gunship = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Gunship,
                    UnitMaterials.BaseIndex(0, UnitKind.Gunship),
                    new float3(-30f, 0f, 0f), 0f, UnitKind.Gunship, 0);

                var units = new NativeList<Entity>(1, Allocator.Temp);
                units.Add(gunship);
                var issued = MoveOrderIssuer.TryIssue(
                    entityManager, units, new float3(30f, 0f, 0f), out _);
                units.Dispose();
                Assert.IsTrue(issued);

                for (var i = 0; i < 30; i++)
                {
                    var position = entityManager.GetComponentData<LocalTransform>(gunship).Position;
                    if (position.x > 5f)
                    {
                        break;
                    }

                    yield return new WaitForSeconds(0.2f);
                }

                var final = entityManager.GetComponentData<LocalTransform>(gunship).Position;
                Assert.Greater(final.x, 5f, "the gunship must cross the blocked column");
                Assert.Greater(final.y, 5f, "the gunship must fly above the ground");
                Assert.IsFalse(math.any(math.isnan(final)), "flight positions must stay finite");
            }
            finally
            {
                Cleanup(entityManager, data, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator WarFactory_ProducesGunship()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 141, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();

            try
            {
                CreateEconomy(entityManager, 3000, 3000);
                var factory = BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.WarFactory, new float3(-30f, 0f, 0f));

                Assert.IsTrue(
                    ProductionCommands.TryEnqueue(
                        entityManager, 0, UnitKind.Gunship, Entity.Null, out var failure),
                    $"gunship enqueue should succeed ({failure})");

                var queue = entityManager.GetBuffer<ProductionOrder>(factory);
                var order = queue[0];
                order.Remaining = 0.05f;
                queue[0] = order;

                yield return new WaitForSeconds(0.6f);

                using var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<AirUnit>(),
                    ComponentType.ReadOnly<LocalTransform>());
                using var units = query.ToEntityArray(Allocator.Temp);

                var produced = Entity.Null;
                foreach (var unit in units)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (info.Team == 0 && info.Kind == UnitKind.Gunship)
                    {
                        produced = unit;
                        break;
                    }
                }

                Assert.AreNotEqual(Entity.Null, produced, "the war factory must produce a gunship");
                Assert.IsTrue(entityManager.HasComponent<AirUnit>(produced));
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

        private static void SetWalkable(
            EntityManager entityManager,
            Entity data,
            int x,
            int z,
            byte value)
        {
            var info = entityManager.GetComponentData<NavGridInfo>(data);
            var nav = entityManager.GetBuffer<NavCell>(data);
            nav[z * info.Size.x + x] = new NavCell { Walkable = value };
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
