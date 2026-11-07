using System.Collections.Generic;
using CQRa.Sim.Base;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Turns a group move into deterministic formation slots.
    ///
    /// Every unit receives its own arrival point around the clicked position, while all
    /// of them share <see cref="MoveTarget.PathDestination"/> so the flow-field system
    /// still builds exactly one field per order group. Without slots, a large group
    /// fights for a single point and separation can hold units outside their arrival
    /// radius forever.
    /// </summary>
    public static class MoveOrderIssuer
    {
        public const float MinSlotSpacing = 4.5f;
        private const float SlotPadding = 1.5f;

        public static bool TryIssue(
            EntityManager entityManager,
            NativeList<Entity> units,
            float3 clickPoint,
            out float3 resolvedPoint)
        {
            resolvedPoint = clickPoint;
            if (units.Length == 0)
            {
                return false;
            }

            if (Navigation.NavGridQuery.TryClampToWalkable(entityManager, clickPoint, out var clamped))
            {
                resolvedPoint = clamped;
            }

            var indices = new List<int>(units.Length);
            for (var i = 0; i < units.Length; i++)
            {
                indices.Add(i);
            }

            // Stable assignment order, independent of selection order.
            indices.Sort((a, b) =>
            {
                var pa = entityManager.GetComponentData<LocalTransform>(units[a]).Position;
                var pb = entityManager.GetComponentData<LocalTransform>(units[b]).Position;
                var byX = pa.x.CompareTo(pb.x);
                return byX != 0 ? byX : pa.z.CompareTo(pb.z);
            });

            var slots = BuildSlots(units.Length, SlotSpacingFor(entityManager, units));

            var formationRadius = 0f;
            for (var i = 0; i < slots.Length; i++)
            {
                formationRadius = math.max(formationRadius, math.length(slots[i].xz));
            }

            try
            {
                for (var i = 0; i < indices.Count; i++)
                {
                    var entity = units[indices[i]];
                    var destination = resolvedPoint + slots[i];

                    if (Navigation.NavGridQuery.TryClampToWalkable(entityManager, destination, out var slotClamped))
                    {
                        destination = slotClamped;
                    }

                    var target = entityManager.GetComponentData<MoveTarget>(entity);
                    target.Destination = destination;
                    target.PathDestination = resolvedPoint;
                    target.Field = Entity.Null;
                    target.FormationRadius = formationRadius;
                    target.HasOrder = 1;
                    entityManager.SetComponentData(entity, target);

                    // A move order always overrides an attack order.
                    if (entityManager.HasComponent<CombatState>(entity))
                    {
                        entityManager.SetComponentData(entity, new CombatState
                        {
                            Target = Entity.Null,
                            Explicit = 0,
                        });
                    }

                    // ...and it also takes the harvester out of auto-drive.
                    if (entityManager.HasComponent<HarvesterJob>(entity))
                    {
                        var job = entityManager.GetComponentData<HarvesterJob>(entity);
                        job.AutoOrder = 0;
                        entityManager.SetComponentData(entity, job);
                    }
                }
            }
            finally
            {
                slots.Dispose();
            }

            return true;
        }

        /// <summary>
        /// Slot pitch must fit the biggest unit in the group, otherwise tanks are ordered
        /// into footprints that physically overlap and arrive stacked on top of each other.
        /// </summary>
        public static float SlotSpacingFor(EntityManager entityManager, NativeList<Entity> units)
        {
            var maxRadius = 0f;
            for (var i = 0; i < units.Length; i++)
            {
                maxRadius = math.max(maxRadius, entityManager.GetComponentData<UnitInfo>(units[i]).Radius);
            }

            return math.max(MinSlotSpacing, maxRadius * 2f + SlotPadding);
        }

        /// <summary>Square spiral of offsets, centre first, deterministic for a count.</summary>
        public static NativeArray<float3> BuildSlots(int count, float spacing)
        {
            var slots = new NativeArray<float3>(math.max(0, count), Allocator.Temp);

            var index = 0;
            for (var ring = 0; index < count; ring++)
            {
                for (var z = -ring; z <= ring && index < count; z++)
                {
                    for (var x = -ring; x <= ring && index < count; x++)
                    {
                        if (ring > 0 && math.max(math.abs(x), math.abs(z)) != ring)
                        {
                            continue;
                        }

                        slots[index] = new float3(x * spacing, 0f, z * spacing);
                        index++;
                    }
                }
            }

            return slots;
        }
    }
}
