using CQRa.Sim.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Maintains a small cache of flow fields keyed by destination cell.
    ///
    /// Units request a field implicitly: a move order clears <see cref="MoveTarget.Field"/>.
    /// This system snaps the destination to the nearest walkable cell, finds or builds
    /// the matching field, and writes the field entity back to every unit heading there.
    /// Fields are shared, so a 200-unit attack move costs one Dijkstra pass.
    ///
    /// Runs in the fixed-step group so it is part of the deterministic 30 Hz simulation.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(UnitMovementSystem))]
    public partial class FlowFieldSystem : SystemBase
    {
        private const int MaxFields = 16;
        private const int DestinationSnapRadius = 20;

        private int _generation;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var dataQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NavGridInfo>(),
                ComponentType.ReadOnly<NavCell>());

            if (dataQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var dataEntity = dataQuery.GetSingletonEntity();
            var gridInfo = entityManager.GetComponentData<NavGridInfo>(dataEntity);

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<MoveTarget>(),
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var units = unitQuery.ToEntityArray(Allocator.Temp);
            if (units.Length == 0)
            {
                return;
            }

            using var fieldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldMeta>());
            using var fieldEntities = fieldQuery.ToEntityArray(Allocator.Temp);

            var fields = new NativeList<Entity>(Allocator.Temp);
            fields.AddRange(fieldEntities);

            var referenced = new NativeParallelHashSet<Entity>(math.max(4, units.Length), Allocator.Temp);
            for (var i = 0; i < units.Length; i++)
            {
                var target = entityManager.GetComponentData<MoveTarget>(units[i]);
                if (target.HasOrder != 0 && target.Field != Entity.Null)
                {
                    referenced.Add(target.Field);
                }
            }

            var requested = new NativeList<int>(Allocator.Temp);
            var walkable = default(NativeArray<byte>);
            var hasWalkable = false;

            for (var u = 0; u < units.Length; u++)
            {
                var entity = units[u];
                var target = entityManager.GetComponentData<MoveTarget>(entity);

                if (target.HasOrder == 0)
                {
                    if (target.Field != Entity.Null)
                    {
                        target.Field = Entity.Null;
                        entityManager.SetComponentData(entity, target);
                    }

                    continue;
                }

                if (!hasWalkable)
                {
                    var nav = entityManager.GetBuffer<NavCell>(dataEntity, true);
                    walkable = new NativeArray<byte>(nav.Length, Allocator.TempJob);
                    for (var i = 0; i < nav.Length; i++)
                    {
                        walkable[i] = nav[i].Walkable;
                    }

                    hasWalkable = true;
                }

                var rawCell = NavGridMath.WorldToCell(
                    gridInfo,
                    new float2(target.PathDestination.x, target.PathDestination.z));
                var cell = NavGridBuilder.NearestWalkable(
                    walkable,
                    gridInfo.Size,
                    rawCell,
                    DestinationSnapRadius);

                if (!cell.Equals(target.DestinationCell))
                {
                    target.DestinationCell = cell;
                    target.Field = Entity.Null;
                    entityManager.SetComponentData(entity, target);
                }

                var needsField = target.Field == Entity.Null || !entityManager.Exists(target.Field);

                if (!needsField)
                {
                    var meta = entityManager.GetComponentData<FlowFieldMeta>(target.Field);
                    needsField = !meta.Destination.Equals(cell);
                }

                if (needsField)
                {
                    requested.Add(cell.y * gridInfo.Size.x + cell.x);
                }
            }

            requested.Sort();

            var lastRequested = -1;
            for (var r = 0; r < requested.Length; r++)
            {
                var packed = requested[r];
                if (packed == lastRequested)
                {
                    continue;
                }

                lastRequested = packed;

                var cell = new int2(packed % gridInfo.Size.x, packed / gridInfo.Size.x);
                var field = BuildOrReuse(entityManager, gridInfo, walkable, fields, referenced, cell);
                AssignField(entityManager, units, cell, field);
            }

            requested.Dispose();
            referenced.Dispose();
            fields.Dispose();

            if (hasWalkable)
            {
                walkable.Dispose();
            }
        }

        private Entity BuildOrReuse(
            EntityManager entityManager,
            in NavGridInfo gridInfo,
            NativeArray<byte> walkable,
            NativeList<Entity> fields,
            NativeParallelHashSet<Entity> referenced,
            int2 cell)
        {
            var fieldEntity = Entity.Null;
            var oldestIndex = -1;
            var oldestGeneration = int.MaxValue;

            for (var i = 0; i < fields.Length; i++)
            {
                var candidate = fields[i];
                if (!entityManager.Exists(candidate) ||
                    !entityManager.HasComponent<FlowFieldMeta>(candidate))
                {
                    fields.RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                var meta = entityManager.GetComponentData<FlowFieldMeta>(candidate);
                if (meta.Destination.Equals(cell))
                {
                    fieldEntity = candidate;
                    break;
                }

                // Never repurpose a field that a unit is currently following;
                // otherwise the unit silently starts walking to someone else's target.
                if (referenced.Contains(candidate))
                {
                    continue;
                }

                if (meta.Generation < oldestGeneration)
                {
                    oldestGeneration = meta.Generation;
                    oldestIndex = i;
                }
            }

            var cellCount = gridInfo.Size.x * gridInfo.Size.y;

            if (fieldEntity == Entity.Null)
            {
                if (fields.Length >= MaxFields && oldestIndex >= 0)
                {
                    fieldEntity = fields[oldestIndex];
                }
                else
                {
                    fieldEntity = entityManager.CreateEntity(typeof(FlowFieldMeta));
                    entityManager.AddBuffer<FlowFieldDirection>(fieldEntity);
                    fields.Add(fieldEntity);
                }
            }

            _generation++;
            entityManager.SetComponentData(fieldEntity, new FlowFieldMeta
            {
                Destination = cell,
                Generation = _generation,
            });

            var directions = new NativeArray<byte>(cellCount, Allocator.TempJob);
            var integration = new NativeArray<int>(cellCount, Allocator.TempJob);

            try
            {
                var job = new BuildFlowFieldJob
                {
                    Walkable = walkable,
                    Size = gridInfo.Size,
                    Destination = cell,
                    Directions = directions,
                    Integration = integration,
                };

                Unity.Jobs.IJobExtensions.Schedule(job, default).Complete();

                var buffer = entityManager.GetBuffer<FlowFieldDirection>(fieldEntity);
                buffer.ResizeUninitialized(cellCount);
                for (var i = 0; i < cellCount; i++)
                {
                    buffer[i] = new FlowFieldDirection { Value = directions[i] };
                }
            }
            finally
            {
                directions.Dispose();
                integration.Dispose();
            }

            return fieldEntity;
        }

        private static void AssignField(
            EntityManager entityManager,
            NativeArray<Entity> units,
            int2 cell,
            Entity field)
        {
            for (var i = 0; i < units.Length; i++)
            {
                var target = entityManager.GetComponentData<MoveTarget>(units[i]);
                if (target.HasOrder == 0 || !target.DestinationCell.Equals(cell) || target.Field == field)
                {
                    continue;
                }

                target.Field = field;
                entityManager.SetComponentData(units[i], target);
            }
        }
    }
}
