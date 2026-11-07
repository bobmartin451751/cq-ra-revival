using CQRa.Sim.Units;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Creates building and ore entities. Rendering is optional so headless tests can
    /// spawn gameplay entities without UnityEngine materials.
    /// </summary>
    public static class BuildingFactory
    {
        public const int BuildingKindCount = 5;
        public const int OreMeshIndex = BuildingKindCount;
        public const int OreMaterialIndex = BuildingKindCount * 2;
        public const int HighlightMaterialIndex = OreMaterialIndex + 1;
        public const int MaterialCount = HighlightMaterialIndex + 1;

        private static RenderMeshArray _renderMeshArray;
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

        public static void Initialize(Material[] materials, Mesh[] meshes)
        {
            _renderMeshArray = new RenderMeshArray(materials, meshes);
            _initialized = true;
        }

        public static void Shutdown()
        {
            _initialized = false;
            _renderMeshArray = default;
        }

        public static Entity SpawnBuilding(
            EntityManager entityManager,
            int team,
            BuildingKind kind,
            float3 position,
            float yawRadians = 0f)
        {
            var spec = BuildCatalog.Get(kind);
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                position,
                quaternion.RotateY(yawRadians),
                1f));

            entityManager.AddComponentData(entity, new BuildingTag());
            entityManager.AddComponentData(entity, new BuildingInfo
            {
                Team = team,
                Kind = kind,
                Radius = spec.Radius,
                PowerProduced = spec.PowerProduced,
                PowerUsed = spec.PowerUsed,
            });

            entityManager.AddComponentData(entity, new Health
            {
                Max = HealthFor(kind),
                Current = HealthFor(kind),
                Armor = 0.5f,
            });

            if (kind == BuildingKind.ConstructionYard)
            {
                entityManager.AddComponent<ConstructionYardTag>(entity);
            }
            else if (kind == BuildingKind.Refinery)
            {
                entityManager.AddComponent<RefineryTag>(entity);
            }
            else if (kind == BuildingKind.Barracks || kind == BuildingKind.WarFactory)
            {
                entityManager.AddComponent<ProductionTag>(entity);
                entityManager.AddBuffer<ProductionOrder>(entity);

                var direction = new float2(-position.x, -position.z);
                direction = math.normalizesafe(direction, new float2(0f, 1f));
                entityManager.AddComponentData(entity, new RallyPoint
                {
                    Value = new float3(
                        position.x + direction.x * 22f,
                        position.y,
                        position.z + direction.y * 22f),
                    HasValue = 1,
                });
            }

            entityManager.AddComponentData(entity, new Selected());
            entityManager.SetComponentEnabled<Selected>(entity, false);
            entityManager.AddComponentData(entity, new HighlightState { IsSelected = 0 });

            if (_initialized)
            {
                AddRenderComponents(
                    entityManager,
                    entity,
                    (int)kind,
                    team * BuildingKindCount + (int)kind);
            }

            return entity;
        }

        public static Entity SpawnOre(
            EntityManager entityManager,
            float3 position,
            int amount,
            float radius,
            float yawRadians = 0f)
        {
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                position,
                quaternion.RotateY(yawRadians),
                1f));

            entityManager.AddComponentData(entity, new OreNode
            {
                Amount = amount,
                Radius = radius,
            });

            if (_initialized)
            {
                AddRenderComponents(entityManager, entity, OreMeshIndex, OreMaterialIndex);
            }

            return entity;
        }

        private static void AddRenderComponents(
            EntityManager entityManager,
            Entity entity,
            int meshIndex,
            int materialIndex)
        {
            var description = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
            var info = MaterialMeshInfo.FromRenderMeshArrayIndices(materialIndex, meshIndex);
            RenderMeshUtility.AddComponents(entity, entityManager, description, _renderMeshArray, info);
        }

        private static float HealthFor(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.ConstructionYard:
                    return 1200f;
                case BuildingKind.Refinery:
                    return 900f;
                case BuildingKind.Barracks:
                    return 500f;
                case BuildingKind.WarFactory:
                    return 800f;
                default:
                    return 600f;
            }
        }
    }
}
