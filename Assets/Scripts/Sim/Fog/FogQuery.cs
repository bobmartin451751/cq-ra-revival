using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Fog
{
    /// <summary>Read-only fog queries used by combat, AI, selection and the HUD.</summary>
    public static class FogQuery
    {
        public static bool TryGet(
            EntityManager entityManager,
            int team,
            out Entity entity,
            out FogGridInfo info)
        {
            entity = Entity.Null;
            info = default;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<FogState>(),
                ComponentType.ReadOnly<FogTeam>(),
                ComponentType.ReadOnly<FogGridInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<FogTeam>(entities[i]).Team == team)
                {
                    entity = entities[i];
                    info = entityManager.GetComponentData<FogGridInfo>(entity);
                    return true;
                }
            }

            return false;
        }

        public static NativeArray<FogCell> GetCells(EntityManager entityManager, Entity fog)
        {
            return entityManager.GetBuffer<FogCell>(fog, true).AsNativeArray();
        }

        /// <summary>Fail-open when fog does not exist (bare EditMode tests).</summary>
        public static bool IsVisible(EntityManager entityManager, int team, float3 position)
        {
            return Read(entityManager, team, position) == FogMath.Visible;
        }

        public static bool IsExplored(EntityManager entityManager, int team, float3 position)
        {
            return Read(entityManager, team, position) >= FogMath.Explored;
        }

        public static byte Read(EntityManager entityManager, int team, float3 position)
        {
            if (!TryGet(entityManager, team, out var fog, out var info))
            {
                return FogMath.Visible;
            }

            var cells = GetCells(entityManager, fog);
            return FogMath.Read(cells, info, new float2(position.x, position.z));
        }
    }
}
