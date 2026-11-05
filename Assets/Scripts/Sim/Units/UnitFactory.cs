using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Creates renderable ECS units at runtime using Entities Graphics.
    /// Shared meshes and materials keep draw calls low; selection changes swap the
    /// entity's material index to a dedicated highlight material.
    /// </summary>
    public static class UnitFactory
    {
        public static RenderMeshArray CreateRenderMeshArray(Material[] materials, Mesh[] meshes)
        {
            return new RenderMeshArray(materials, meshes);
        }

        public static Entity Spawn(
            EntityManager entityManager,
            RenderMeshArray renderMeshArray,
            int meshIndex,
            int materialIndex,
            float3 position,
            float yawRadians,
            UnitKind kind,
            int team)
        {
            var entity = entityManager.CreateEntity();

            entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                position,
                quaternion.RotateY(yawRadians),
                1f));

            entityManager.AddComponent<UnitTag>(entity);
            entityManager.AddComponentData(entity, new UnitInfo
            {
                Team = team,
                Kind = kind,
                Radius = RadiusFor(kind),
                MaxSpeed = SpeedFor(kind),
            });

            entityManager.AddComponentData(entity, new Selected());
            entityManager.SetComponentEnabled<Selected>(entity, false);

            entityManager.AddComponentData(entity, new HighlightState { IsSelected = 0 });

            var description = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(materialIndex, meshIndex);
            RenderMeshUtility.AddComponents(entity, entityManager, description, renderMeshArray, materialMeshInfo);

            return entity;
        }

        public static float RadiusFor(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Tank: return 3.4f;
                case UnitKind.Harvester: return 3.8f;
                default: return 0.6f;
            }
        }

        public static float SpeedFor(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Tank: return 9.0f;
                case UnitKind.Harvester: return 6.0f;
                default: return 4.0f;
            }
        }
    }
}
