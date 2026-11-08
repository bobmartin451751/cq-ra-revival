using CQRa.Sim.Factions;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    public enum ProductionFailure : byte
    {
        None = 0,
        NoBuilding = 1,
        NotEnoughCredits = 2,
        QueueFull = 3,
        NoEconomy = 4,
    }

    /// <summary>Player-facing production commands used by the HUD, tests and capture rig.</summary>
    public static class ProductionCommands
    {
        public static bool TryEnqueue(
            EntityManager entityManager,
            int team,
            UnitKind kind,
            Entity preferredBuilding,
            out ProductionFailure failure)
        {
            failure = ProductionFailure.None;

            if (!BaseCommands.TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                failure = ProductionFailure.NoEconomy;
                return false;
            }

            var spec = ProductionCatalog.Get(kind);
            var building = FindProducerWithRoom(
                entityManager, team, spec.Producer, preferredBuilding, out var anyProducer);

            if (building == Entity.Null ||
                !entityManager.HasComponent<ProductionOrder>(building))
            {
                failure = anyProducer ? ProductionFailure.QueueFull : ProductionFailure.NoBuilding;
                return false;
            }

            var queue = entityManager.GetBuffer<ProductionOrder>(building);
            var cost = FactionRuntime.CostFor(team, spec.Cost);

            if (!BuildRules.CanAfford(Economy.GetCredits(economy, team), cost))
            {
                failure = ProductionFailure.NotEnoughCredits;
                return false;
            }

            Economy.TrySpend(ref economy, team, cost);
            entityManager.SetComponentData(economyEntity, economy);

            queue.Add(new ProductionOrder
            {
                Kind = kind,
                Remaining = spec.Seconds,
                Total = spec.Seconds,
            });

            return true;
        }

        /// <summary>
        /// Prefers the selected building when it can build the kind and has room; otherwise
        /// picks the first matching producer with a free queue slot. With several barracks
        /// or factories, a full queue on one building no longer blocks the others.
        /// </summary>
        private static Entity FindProducerWithRoom(
            EntityManager entityManager,
            int team,
            BuildingKind producerKind,
            Entity preferredBuilding,
            out bool anyProducer)
        {
            anyProducer = false;

            if (preferredBuilding != Entity.Null &&
                entityManager.Exists(preferredBuilding) &&
                entityManager.HasComponent<BuildingInfo>(preferredBuilding) &&
                entityManager.HasComponent<ProductionTag>(preferredBuilding))
            {
                var preferredInfo = entityManager.GetComponentData<BuildingInfo>(preferredBuilding);
                if (preferredInfo.Team == team && preferredInfo.Kind == producerKind)
                {
                    anyProducer = true;
                    if (entityManager.HasComponent<ProductionOrder>(preferredBuilding) &&
                        entityManager.GetBuffer<ProductionOrder>(preferredBuilding, true).Length <
                        ProductionCatalog.QueueLimit)
                    {
                        return preferredBuilding;
                    }
                }
            }

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ProductionTag>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<ProductionOrder>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(entities[i]);
                if (info.Team != team || info.Kind != producerKind)
                {
                    continue;
                }

                anyProducer = true;
                if (entityManager.GetBuffer<ProductionOrder>(entities[i], true).Length <
                    ProductionCatalog.QueueLimit)
                {
                    return entities[i];
                }
            }

            return Entity.Null;
        }

        /// <summary>
        /// Prefers the selected building when it can build the kind, otherwise picks the
        /// first matching producer for the team (deterministic entity order).
        /// </summary>
        public static Entity FindProducer(
            EntityManager entityManager,
            int team,
            BuildingKind producerKind,
            Entity preferredBuilding)
        {
            if (preferredBuilding != Entity.Null &&
                entityManager.Exists(preferredBuilding) &&
                entityManager.HasComponent<BuildingInfo>(preferredBuilding) &&
                entityManager.HasComponent<ProductionTag>(preferredBuilding))
            {
                var preferredInfo = entityManager.GetComponentData<BuildingInfo>(preferredBuilding);
                if (preferredInfo.Team == team && preferredInfo.Kind == producerKind)
                {
                    return preferredBuilding;
                }
            }

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ProductionTag>(),
                ComponentType.ReadOnly<BuildingInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(entities[i]);
                if (info.Team == team && info.Kind == producerKind)
                {
                    return entities[i];
                }
            }

            return Entity.Null;
        }
    }

    /// <summary>
    /// Advances production queues and spawns completed units next to their building.
    /// The front queue item is the active build; costs are paid when enqueued, so
    /// cancelling (a future phase) would refund at enqueue price.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(EconomySystem))]
    public partial class ProductionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ProductionTag>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<RallyPoint>(),
                ComponentType.ReadWrite<ProductionOrder>());

            using var buildings = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                var queue = entityManager.GetBuffer<ProductionOrder>(building);
                if (queue.Length == 0)
                {
                    continue;
                }

                var order = queue[0];
                order.Remaining -= dt;

                if (order.Remaining > 0f)
                {
                    queue[0] = order;
                    continue;
                }

                // Consume the order before spawning: unit creation is a structural change
                // and would invalidate the queue buffer handle.
                queue.RemoveAt(0);
                SpawnCompletedUnit(entityManager, building, order.Kind);
            }
        }

        private static void SpawnCompletedUnit(EntityManager entityManager, Entity building, UnitKind kind)
        {
            var info = entityManager.GetComponentData<BuildingInfo>(building);
            var position = entityManager.GetComponentData<LocalTransform>(building).Position;
            var rally = entityManager.GetComponentData<RallyPoint>(building);

            var rallyPoint = rally.HasValue != 0
                ? rally.Value
                : position + new float3(0f, 0f, 24f);

            var direction = math.normalizesafe(
                new float2(rallyPoint.x - position.x, rallyPoint.z - position.z),
                new float2(0f, 1f));

            var spec = BuildCatalog.Get(info.Kind);
            var spawnPoint = position + new float3(direction.x, 0f, direction.y) * (spec.NavRadius + 3f);
            spawnPoint.y = position.y;

            if (NavGridQuery.TryClampToWalkable(entityManager, spawnPoint, out var safe))
            {
                spawnPoint = safe;
            }

            var yaw = math.atan2(direction.x, direction.y);
            var unit = UnitFactory.SpawnDefault(entityManager, spawnPoint, yaw, kind, info.Team);

            var move = entityManager.GetComponentData<MoveTarget>(unit);
            move.Destination = rallyPoint;
            move.PathDestination = rallyPoint;
            move.Field = Entity.Null;
            move.FormationRadius = 0f;
            move.HasOrder = 1;
            entityManager.SetComponentData(unit, move);
        }
    }
}
