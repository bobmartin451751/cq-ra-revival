using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Autonomous harvester loop: seek the nearest ore patch, harvest, drive to the
    /// nearest refinery, deposit, repeat. The loop only writes move orders when the
    /// harvester has no order, so a player move order always takes precedence until the
    /// harvester arrives.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(EconomySystem))]
    public partial class HarvesterSystem : SystemBase
    {
        private const float HarvestRange = 8f;
        private const float DockRange = 20f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            if (!BaseCommands.TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                return;
            }

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<HarvesterJob>(),
                ComponentType.ReadWrite<MoveTarget>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var harvesters = query.ToEntityArray(Allocator.Temp);
            var dt = SystemAPI.Time.DeltaTime;
            var economyChanged = false;

            for (var i = 0; i < harvesters.Length; i++)
            {
                var entity = harvesters[i];
                if (entityManager.GetComponentData<Health>(entity).Current <= 0f)
                {
                    continue;
                }

                var info = entityManager.GetComponentData<UnitInfo>(entity);
                var job = entityManager.GetComponentData<HarvesterJob>(entity);
                var move = entityManager.GetComponentData<MoveTarget>(entity);
                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                var team = info.Team;

                switch (job.Phase)
                {
                    case HarvesterPhase.Idle:
                        if (move.HasOrder != 0)
                        {
                            break;
                        }

                        if (BaseCommands.FindNearestRefinery(entityManager, team, position) == Entity.Null)
                        {
                            break;
                        }

                        var ore = FindNearestOre(entityManager, position);
                        if (ore != Entity.Null)
                        {
                            job.Ore = ore;
                            job.Phase = HarvesterPhase.ToOre;
                        }

                        break;

                    case HarvesterPhase.ToOre:
                        if (!IsValidOre(entityManager, job.Ore))
                        {
                            job.Ore = Entity.Null;
                            job.Phase = job.Cargo > 0f ? HarvesterPhase.ToRefinery : HarvesterPhase.Idle;
                            job.AutoOrder = 0;
                            break;
                        }

                        var orePosition = entityManager.GetComponentData<LocalTransform>(job.Ore).Position;
                        if (Distance2D(position, orePosition) <= HarvestRange &&
                            (job.AutoOrder != 0 || move.HasOrder == 0))
                        {
                            job.Phase = HarvesterPhase.Harvesting;
                            job.AutoOrder = 0;
                            ClearOrder(ref move);
                        }
                        else if (move.HasOrder == 0)
                        {
                            IssueMove(ref move, ref job, orePosition);
                        }

                        break;

                    case HarvesterPhase.Harvesting:
                        if (move.HasOrder != 0)
                        {
                            // Player took control mid-harvest; resume the loop afterwards.
                            job.Phase = HarvesterPhase.Idle;
                            job.AutoOrder = 0;
                            break;
                        }

                        if (!IsValidOre(entityManager, job.Ore))
                        {
                            job.Ore = Entity.Null;
                            job.Phase = HarvesterPhase.ToRefinery;
                            job.AutoOrder = 0;
                            break;
                        }

                        var oreEntity = job.Ore;
                        var node = entityManager.GetComponentData<OreNode>(oreEntity);
                        var take = math.min(
                            (int)math.round(Economy.HarvestRate * dt),
                            math.min(node.Amount, (int)math.floor(Economy.CargoCapacity - job.Cargo)));
                        take = math.max(0, take);

                        if (take > 0)
                        {
                            node.Amount -= take;
                            job.Cargo += take;
                            entityManager.SetComponentData(oreEntity, node);
                        }

                        if (job.Cargo >= Economy.CargoCapacity - 0.001f || node.Amount <= 0)
                        {
                            job.Phase = HarvesterPhase.ToRefinery;
                        }

                        break;

                    case HarvesterPhase.ToRefinery:
                        if (job.Cargo <= 0f)
                        {
                            job.Phase = HarvesterPhase.Idle;
                            job.AutoOrder = 0;
                            break;
                        }

                        var refinery = BaseCommands.FindNearestRefinery(entityManager, team, position);
                        if (refinery == Entity.Null)
                        {
                            job.Phase = HarvesterPhase.Idle;
                            job.AutoOrder = 0;
                            break;
                        }

                        var refineryPosition = entityManager.GetComponentData<LocalTransform>(refinery).Position;
                        if (Distance2D(position, refineryPosition) <= DockRange)
                        {
                            Economy.AddCredits(ref economy, team, (int)job.Cargo);
                            job.Cargo = 0f;
                            job.Phase = HarvesterPhase.Idle;
                            job.AutoOrder = 0;
                            economyChanged = true;
                            ClearOrder(ref move);
                        }
                        else if (move.HasOrder == 0)
                        {
                            IssueMove(ref move, ref job, refineryPosition);
                        }

                        break;
                }

                entityManager.SetComponentData(entity, job);
                entityManager.SetComponentData(entity, move);
            }

            if (economyChanged)
            {
                entityManager.SetComponentData(economyEntity, economy);
            }
        }

        private static Entity FindNearestOre(EntityManager entityManager, float3 position)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<OreNode>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var nodes = query.ToEntityArray(Allocator.Temp);

            var best = Entity.Null;
            var bestDistanceSq = float.MaxValue;

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = entityManager.GetComponentData<OreNode>(nodes[i]);
                if (node.Amount <= 0)
                {
                    continue;
                }

                var nodePosition = entityManager.GetComponentData<LocalTransform>(nodes[i]).Position;
                var distanceSq = math.distancesq(position, nodePosition);
                if (distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq && best != Entity.Null && nodes[i].Index < best.Index))
                {
                    bestDistanceSq = distanceSq;
                    best = nodes[i];
                }
            }

            return best;
        }

        private static bool IsValidOre(EntityManager entityManager, Entity ore)
        {
            return ore != Entity.Null &&
                   entityManager.Exists(ore) &&
                   entityManager.HasComponent<OreNode>(ore) &&
                   entityManager.GetComponentData<OreNode>(ore).Amount > 0;
        }

        private static void IssueMove(ref MoveTarget move, ref HarvesterJob job, float3 destination)
        {
            move.Destination = destination;
            move.PathDestination = destination;
            move.Field = Entity.Null;
            move.FormationRadius = 0f;
            move.HasOrder = 1;
            job.AutoOrder = 1;
        }

        private static void ClearOrder(ref MoveTarget move)
        {
            move.HasOrder = 0;
            move.Field = Entity.Null;
        }

        private static float Distance2D(float3 a, float3 b)
        {
            return math.distance(new float2(a.x, a.z), new float2(b.x, b.z));
        }
    }
}
