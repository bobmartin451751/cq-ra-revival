using CQRa.Sim.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Deterministic steering and local avoidance.
    ///
    /// Each unit samples its flow field once per fixed tick, blends in direct steering
    /// during the final approach, adds separation from nearby units, and slides along
    /// blocked cells instead of penetrating them. Ground height comes from the
    /// simulation-owned height buffer, so no physics or NavMesh is involved.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldSystem))]
    public partial class UnitMovementSystem : SystemBase
    {
        private const float TurnRate = 7.5f;
        private const float SeparationStrength = 1.25f;
        private const float MaxSeparation = 0.75f;
        private const float SeparationCellSize = 8f;
        private const float VelocitySmoothing = 12f;

        /// <summary>
        /// Within this radius of the group's shared destination, units steer straight
        /// at their own formation slot instead of following the shared flow field.
        /// The shared field leads to the click point, which can be on the far side of
        /// the group: a unit whose slot is behind the pack would orbit the group
        /// centre forever because the field keeps pulling it back in.
        /// </summary>
        private const float FormationApproachRadius = 22f;
        private const float FormationApproachMargin = 12f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var dataQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainInfo>(),
                ComponentType.ReadOnly<TerrainHeightSample>(),
                ComponentType.ReadOnly<NavGridInfo>(),
                ComponentType.ReadOnly<NavCell>());

            if (dataQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var dataEntity = dataQuery.GetSingletonEntity();
            var terrainInfo = entityManager.GetComponentData<TerrainInfo>(dataEntity);
            var gridInfo = entityManager.GetComponentData<NavGridInfo>(dataEntity);
            var heights = entityManager.GetBuffer<TerrainHeightSample>(dataEntity, true).AsNativeArray();
            var nav = entityManager.GetBuffer<NavCell>(dataEntity, true).AsNativeArray();
            var fieldLookup = GetBufferLookup<FlowFieldDirection>(true);

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<MoveTarget>(),
                ComponentType.ReadWrite<UnitMotion>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<UnitTag>());

            using var entities = unitQuery.ToEntityArray(Allocator.Temp);
            var count = entities.Length;
            if (count == 0)
            {
                return;
            }

            var positions = new NativeArray<float3>(count, Allocator.Temp);
            var radii = new NativeArray<float>(count, Allocator.Temp);
            var air = new NativeArray<byte>(count, Allocator.Temp);

            for (var i = 0; i < count; i++)
            {
                positions[i] = entityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                radii[i] = entityManager.GetComponentData<UnitInfo>(entities[i]).Radius;
                air[i] = entityManager.HasComponent<AirUnit>(entities[i])
                    ? (byte)1
                    : entityManager.HasComponent<NavalUnit>(entities[i]) ? (byte)2 : (byte)0;
            }

            var spatialHash = new NativeParallelMultiHashMap<int, int>(
                math.max(4, count * 3),
                Allocator.Temp);

            for (var i = 0; i < count; i++)
            {
                spatialHash.Add(HashKey(positions[i]), i);
            }

            var dt = SystemAPI.Time.DeltaTime;
            var smoothing = 1f - math.exp(-VelocitySmoothing * dt);
            var maxTurn = TurnRate * dt;

            for (var i = 0; i < count; i++)
            {
                var entity = entities[i];
                var transform = entityManager.GetComponentData<LocalTransform>(entity);
                var motion = entityManager.GetComponentData<UnitMotion>(entity);
                var target = entityManager.GetComponentData<MoveTarget>(entity);
                var info = entityManager.GetComponentData<UnitInfo>(entity);
                var position = transform.Position;
                var isAir = air[i] == 1;
                var isNaval = air[i] == 2;
                var altitude = isAir ? entityManager.GetComponentData<AirUnit>(entity).Altitude : 0f;

                if (entityManager.HasComponent<DeployState>(entity) &&
                    entityManager.GetComponentData<DeployState>(entity).Deployed != 0)
                {
                    if (motion.Speed != 0f)
                    {
                        motion.Velocity = float2.zero;
                        motion.Speed = 0f;
                        entityManager.SetComponentData(entity, motion);
                    }

                    continue;
                }

                if (target.HasOrder == 0)
                {
                    if (motion.Speed != 0f)
                    {
                        motion.Velocity = float2.zero;
                        motion.Speed = 0f;
                        entityManager.SetComponentData(entity, motion);
                    }

                    continue;
                }

                var positionXZ = new float2(position.x, position.z);
                var toTarget = new float2(
                    target.Destination.x - position.x,
                    target.Destination.z - position.z);
                var distance = math.length(toTarget);

                if (distance <= target.ArriveRadius)
                {
                    target.HasOrder = 0;
                    target.Field = Entity.Null;
                    motion.Velocity = float2.zero;
                    motion.Speed = 0f;
                    entityManager.SetComponentData(entity, target);
                    entityManager.SetComponentData(entity, motion);
                    continue;
                }

                var desiredDirection = float2.zero;
                var hasFieldDirection = false;

                if (!isAir &&
                    !isNaval &&
                    target.Field != Entity.Null &&
                    fieldLookup.TryGetBuffer(target.Field, out var field))
                {
                    var cell = NavGridMath.WorldToCell(gridInfo, positionXZ);
                    if (gridInfo.InBounds(cell))
                    {
                        var index = cell.y * gridInfo.Size.x + cell.x;
                        var direction = field[index].Value;
                        if (direction != NavGridMath.NoDirection)
                        {
                            desiredDirection = NavGridMath.Direction(direction);
                            hasFieldDirection = true;
                        }
                    }
                }

                var directDirection = toTarget / math.max(distance, 0.0001f);

                if (!hasFieldDirection)
                {
                    desiredDirection = directDirection;
                }
                else
                {
                    var blend = math.saturate((target.ArriveRadius + 6f - distance) / 6f);
                    var groupDistance = math.distance(
                        positionXZ,
                        new float2(target.PathDestination.x, target.PathDestination.z));

                    // Inside the formation footprint, steer straight at the unit's own slot.
                    // The shared field always aims at the click point, so a unit heading for
                    // a slot on the far side would otherwise be pulled back towards the
                    // centre and oscillate there forever.
                    var approachStart = target.FormationRadius > 0f
                        ? target.FormationRadius + FormationApproachMargin
                        : FormationApproachRadius;
                    var approach = math.saturate(
                        (approachStart - groupDistance) / FormationApproachMargin);
                    blend = math.max(blend, approach);
                    desiredDirection = math.normalizesafe(
                        math.lerp(desiredDirection, directDirection, blend),
                        directDirection);
                }

                var separation = isAir
                    ? float2.zero
                    : ComputeSeparation(
                        spatialHash,
                        positions,
                        radii,
                        air,
                        i,
                        positionXZ);

                // Near the arrival point, spacing must not prevent units from settling.
                var separationScale = math.lerp(
                    0.2f,
                    1f,
                    math.saturate((distance - target.ArriveRadius) / 8f));

                // Cap avoidance so a dense pack can deflect a unit but never reverse it.
                // Without this, several overlapping neighbours can out-vote the move
                // direction and lock units at the edge of a group forever.
                var separationSteer = separation * (SeparationStrength * separationScale);
                var separationMagnitude = math.length(separationSteer);
                if (separationMagnitude > MaxSeparation)
                {
                    separationSteer *= MaxSeparation / separationMagnitude;
                }

                var steer = desiredDirection + separationSteer;
                var desiredVelocity = math.normalizesafe(steer, desiredDirection) * info.MaxSpeed;

                // Ease off on the last few metres so units settle instead of orbiting.
                var slow = math.saturate((distance - target.ArriveRadius) / 3f);
                desiredVelocity *= math.lerp(0.35f, 1f, slow);

                motion.Velocity = math.lerp(motion.Velocity, desiredVelocity, smoothing);
                motion.Speed = math.length(motion.Velocity);

                var next = positionXZ + motion.Velocity * dt;

                if (isNaval)
                {
                    var offset = next - WaterLake.Centre;
                    var distanceFromCentre = math.length(offset);
                    var maxRadius = WaterLake.Radius - 2f;
                    if (distanceFromCentre > maxRadius)
                    {
                        next = WaterLake.Centre + math.normalizesafe(offset, new float2(1f, 0f)) * maxRadius;
                    }
                }

                if (!isAir && !isNaval && !IsWalkable(nav, gridInfo, next))
                {
                    // Slide along whichever axis is still free.
                    var slideX = new float2(next.x, positionXZ.y);
                    var slideZ = new float2(positionXZ.x, next.y);

                    if (IsWalkable(nav, gridInfo, slideX))
                    {
                        next = slideX;
                        motion.Velocity = new float2(motion.Velocity.x, 0f);
                    }
                    else if (IsWalkable(nav, gridInfo, slideZ))
                    {
                        next = slideZ;
                        motion.Velocity = new float2(0f, motion.Velocity.y);
                    }
                    else
                    {
                        next = positionXZ;
                        motion.Velocity = float2.zero;
                    }

                    motion.Speed = math.length(motion.Velocity);
                }

                var limit = terrainInfo.Size * 0.5f - 2f;
                next.x = math.clamp(next.x, -limit, limit);
                next.y = math.clamp(next.y, -limit, limit);

                if (motion.Speed > 0.15f)
                {
                    var desiredYaw = math.atan2(motion.Velocity.x, motion.Velocity.y);
                    var delta = WrapAngle(desiredYaw - motion.Yaw);
                    motion.Yaw += math.clamp(delta, -maxTurn, maxTurn);
                }

                var height = isNaval
                    ? WaterLake.Level
                    : TerrainSampler.Sample(heights, terrainInfo, next.x, next.y) + altitude;

                transform.Position = new float3(next.x, height, next.y);
                transform.Rotation = quaternion.RotateY(motion.Yaw);

                entityManager.SetComponentData(entity, transform);
                entityManager.SetComponentData(entity, motion);
            }

            positions.Dispose();
            radii.Dispose();
            air.Dispose();
            spatialHash.Dispose();
        }

        private static float2 ComputeSeparation(
            NativeParallelMultiHashMap<int, int> hash,
            NativeArray<float3> positions,
            NativeArray<float> radii,
            NativeArray<byte> air,
            int self,
            float2 position)
        {
            var cellX = (int)math.floor(position.x / SeparationCellSize);
            var cellZ = (int)math.floor(position.y / SeparationCellSize);
            var separation = float2.zero;

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var key = SpatialKey(cellX + dx, cellZ + dz);
                    if (!hash.TryGetFirstValue(key, out var other, out var iterator))
                    {
                        continue;
                    }

                    do
                    {
                        if (other == self)
                        {
                            continue;
                        }

                        if (air[other] != air[self])
                        {
                            continue;
                        }

                        var delta = position - new float2(positions[other].x, positions[other].z);
                        var distanceSq = math.lengthsq(delta);
                        var range = radii[self] + radii[other] + 2f;

                        if (distanceSq > 0.0001f && distanceSq < range * range)
                        {
                            var distance = math.sqrt(distanceSq);
                            separation += delta / distance * ((range - distance) / range);
                        }
                    }
                    while (hash.TryGetNextValue(out other, ref iterator));
                }
            }

            return separation;
        }

        private static bool IsWalkable(NativeArray<NavCell> nav, in NavGridInfo info, float2 point)
        {
            var cell = NavGridMath.WorldToCell(info, point);
            if (!info.InBounds(cell))
            {
                return false;
            }

            return nav[cell.y * info.Size.x + cell.x].Walkable != 0;
        }

        private static int HashKey(float3 position)
        {
            return SpatialKey(
                (int)math.floor(position.x / SeparationCellSize),
                (int)math.floor(position.z / SeparationCellSize));
        }

        private static int SpatialKey(int x, int z)
        {
            return (x * 73856093) ^ (z * 19349663);
        }

        private static float WrapAngle(float angle)
        {
            while (angle > math.PI)
            {
                angle -= math.PI * 2f;
            }

            while (angle < -math.PI)
            {
                angle += math.PI * 2f;
            }

            return angle;
        }
    }
}
