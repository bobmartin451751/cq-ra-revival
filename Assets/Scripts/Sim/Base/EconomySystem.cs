using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Owns the per-team credits/power singleton and recomputes power totals from the
    /// buildings every fixed tick. Credits are written by the harvester loop and by
    /// build placement.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class EconomySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var economyQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<EconomyState>());
            Entity economyEntity;
            EconomyState state;

            if (economyQuery.IsEmptyIgnoreFilter)
            {
                economyEntity = entityManager.CreateEntity(typeof(EconomyState));
                state = EconomyState.Create(BuildCatalog.InitialCredits);
            }
            else
            {
                economyEntity = economyQuery.GetSingletonEntity();
                state = entityManager.GetComponentData<EconomyState>(economyEntity);
            }

            var made = new int[EconomyState.TeamCount];
            var used = new int[EconomyState.TeamCount];

            using var buildingQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingInfo>());
            using var buildings = buildingQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < buildings.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                if (info.Team < 0 || info.Team >= EconomyState.TeamCount)
                {
                    continue;
                }

                made[info.Team] += info.PowerProduced;
                used[info.Team] += info.PowerUsed;
            }

            for (var team = 0; team < EconomyState.TeamCount; team++)
            {
                Economy.SetPower(ref state, team, made[team], used[team]);
            }

            entityManager.SetComponentData(economyEntity, state);
        }
    }
}
