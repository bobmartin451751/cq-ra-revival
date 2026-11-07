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

        /// <summary>True when the nav grid at this world point is walkable (or when no grid exists).</summary>
        public static bool IsWalkable(EntityManager entityManager, float2 point)
        {
            if (!TryGetGrid(entityManager, out var info, out var gridEntity))
            {
                return true;
            }

            var cell = NavGridMath.WorldToCell(info, point);
            if (!info.InBounds(cell))
            {
                return false;
            }

            var buffer = entityManager.GetBuffer<NavCell>(gridEntity, true);
            return buffer[cell.y * info.Size.x + cell.x].Walkable != 0;
        }

        /// <summary>
        /// Permanently blocks every nav cell whose centre lies inside the circle.
        /// Used when a building is placed so units path around it.
        /// </summary>
        public static void BlockCircle(EntityManager entityManager, float2 centre, float radius)
        {
            if (!TryGetGrid(entityManager, out var info, out var gridEntity))
            {
                return;
            }

            var buffer = entityManager.GetBuffer<NavCell>(gridEntity);
            var min = NavGridMath.WorldToCell(info, centre - radius);
            var max = NavGridMath.WorldToCell(info, centre + radius);

            for (var z = math.max(0, min.y); z <= math.min(info.Size.y - 1, max.y); z++)
            {
                for (var x = math.max(0, min.x); x <= math.min(info.Size.x - 1, max.x); x++)
                {
                    var cellCentre = NavGridMath.CellCentre(info, new int2(x, z));
                    if (math.distance(cellCentre, centre) <= radius)
                    {
                        buffer[z * info.Size.x + x] = new NavCell { Walkable = 0 };
                    }
                }
            }
        }

        /// <summary>Drops every cached flow field so new blockers are respected.</summary>
        public static void InvalidateFlowFields(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldMeta>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
            entities.Dispose();
        }
    }
}
