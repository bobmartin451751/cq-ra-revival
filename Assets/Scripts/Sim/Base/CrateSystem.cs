using CQRa.Sim.Fog;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Supply crates: any unit passing within the pickup radius collects the crate.
    /// Credit crates feed the team economy; repair crates heal the collector. Crates are
    /// deterministic, non-blocking and do not affect pathing.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FogSystem))]
    public partial class CrateSystem : SystemBase
    {
        private const float PickupRadius = 3f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var crateQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Crate>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var crates = crateQuery.ToEntityArray(Allocator.Temp);
            if (crates.Length == 0)
            {
                return;
            }

            if (!BaseCommands.TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                return;
            }

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            var creditsChanged = false;

            for (var c = 0; c < crates.Length; c++)
            {
                var crate = crates[c];
                if (!entityManager.Exists(crate))
                {
                    continue;
                }

                var cratePosition = entityManager.GetComponentData<LocalTransform>(crate).Position;
                var pickup = Entity.Null;
                var bestDistanceSq = PickupRadius * PickupRadius;

                for (var u = 0; u < units.Length; u++)
                {
                    if (entityManager.GetComponentData<Health>(units[u]).Current <= 0f)
                    {
                        continue;
                    }

                    var unitPosition = entityManager.GetComponentData<LocalTransform>(units[u]).Position;
                    var distanceSq = math.distancesq(
                        new float2(cratePosition.x, cratePosition.z),
                        new float2(unitPosition.x, unitPosition.z));

                    if (distanceSq <= bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        pickup = units[u];
                    }
                }

                if (pickup == Entity.Null)
                {
                    continue;
                }

                var info = entityManager.GetComponentData<Crate>(crate);
                var team = entityManager.GetComponentData<UnitInfo>(pickup).Team;

                if (info.Kind == 0)
                {
                    Economy.AddCredits(ref economy, team, info.Reward);
                    creditsChanged = true;
                }
                else
                {
                    var health = entityManager.GetComponentData<Health>(pickup);
                    health.Current = math.min(health.Max, health.Current + info.Reward);
                    entityManager.SetComponentData(pickup, health);
                }

                entityManager.DestroyEntity(crate);
            }

            if (creditsChanged)
            {
                entityManager.SetComponentData(economyEntity, economy);
            }
        }
    }
}
