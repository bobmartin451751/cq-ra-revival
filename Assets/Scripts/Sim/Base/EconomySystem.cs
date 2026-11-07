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
                state = new EconomyState
                {
                    Team0Credits = BuildCatalog.InitialCredits,
                    Team1Credits = BuildCatalog.InitialCredits,
                };
            }
            else
            {
                economyEntity = economyQuery.GetSingletonEntity();
                state = entityManager.GetComponentData<EconomyState>(economyEntity);
            }

            var made0 = 0;
            var used0 = 0;
            var made1 = 0;
            var used1 = 0;

            using var buildingQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingInfo>());
            using var buildings = buildingQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < buildings.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                if (info.Team == 0)
                {
                    made0 += info.PowerProduced;
                    used0 += info.PowerUsed;
                }
                else if (info.Team == 1)
                {
                    made1 += info.PowerProduced;
                    used1 += info.PowerUsed;
                }
            }

            state.Team0PowerMade = made0;
            state.Team0PowerUsed = used0;
            state.Team1PowerMade = made1;
            state.Team1PowerUsed = used1;

            entityManager.SetComponentData(economyEntity, state);
        }
    }
}
