using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Navigation
{
    /// <summary>MonoBehaviour-side queries against the simulation's nav grid.</summary>
    public static class NavGridQuery
    {
        public static bool TryGetGrid(EntityManager entityManager, out NavGridInfo info, out Entity entity)
        {
            info = default;
            entity = Entity.Null;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NavGridInfo>(),
                ComponentType.ReadOnly<NavCell>());

            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            entity = query.GetSingletonEntity();
            info = entityManager.GetComponentData<NavGridInfo>(entity);
            return true;
        }

        /// <summary>
        /// Snaps a world point to the nearest walkable cell centre. Returns false when
        /// no nav grid exists yet (for example in a bare test scene).
        /// </summary>
        public static bool TryClampToWalkable(EntityManager entityManager, float3 point, out float3 result)
        {
            result = point;

            if (!TryGetGrid(entityManager, out var info, out var gridEntity))
            {
                return false;
            }

            var buffer = entityManager.GetBuffer<NavCell>(gridEntity, true);
            var walkable = new NativeArray<byte>(buffer.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    walkable[i] = buffer[i].Walkable;
                }

                var cell = NavGridMath.WorldToCell(info, new float2(point.x, point.z));
                cell = NavGridBuilder.NearestWalkable(walkable, info.Size, cell, 24);
                var centre = NavGridMath.CellCentre(info, cell);
                result = new float3(centre.x, point.y, centre.y);
            }
            finally
            {
                walkable.Dispose();
            }

            return true;
        }
    }
}
