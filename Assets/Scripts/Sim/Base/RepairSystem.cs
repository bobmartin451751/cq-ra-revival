using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Continuous building repair. Heals at a fixed rate while the building is flagged
    /// for repair and the team can pay the per-tick credit cost. Stops automatically
    /// when the building is full or the team runs dry.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(EconomySystem))]
    public partial class RepairSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            if (!BaseCommands.TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                return;
            }

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingTag>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadWrite<RepairState>());

            using var buildings = query.ToEntityArray(Allocator.Temp);
            var dt = SystemAPI.Time.DeltaTime;
            var creditsChanged = false;

            for (var i = 0; i < buildings.Length; i++)
            {
                var entity = buildings[i];
                var repair = entityManager.GetComponentData<RepairState>(entity);
                if (repair.Active == 0)
                {
                    continue;
                }

                var health = entityManager.GetComponentData<Health>(entity);
                if (health.Current >= health.Max - 0.001f)
                {
                    repair.Active = 0;
                    entityManager.SetComponentData(entity, repair);
                    continue;
                }

                var team = entityManager.GetComponentData<BuildingInfo>(entity).Team;
                var cost = (int)math.ceil(RepairRules.CreditsPerSecond * dt);

                if (!Economy.TrySpend(ref economy, team, cost))
                {
                    repair.Active = 0;
                    entityManager.SetComponentData(entity, repair);
                    continue;
                }

                creditsChanged = true;
                health.Current = math.min(health.Max, health.Current + RepairRules.HealPerSecond * dt);

                entityManager.SetComponentData(entity, health);
                entityManager.SetComponentData(entity, repair);
            }

            if (creditsChanged)
            {
                entityManager.SetComponentData(economyEntity, economy);
            }
        }
    }
}
