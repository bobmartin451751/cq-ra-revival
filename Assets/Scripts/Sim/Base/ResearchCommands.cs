using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Base
{
    public static class ResearchCommands
    {
        public static bool TryStart(
            EntityManager entityManager,
            int team,
            UpgradeKind kind,
            out ResearchFailure failure)
        {
            failure = ResearchFailure.None;

            if (!BaseCommands.TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                failure = ResearchFailure.NoEconomy;
                return false;
            }

            var research = ResearchQuery.GetOrCreate(entityManager);
            var state = entityManager.GetComponentData<ResearchState>(research);

            if (state.Has(team, kind))
            {
                failure = ResearchFailure.AlreadyResearched;
                return false;
            }

            if (state.IsActive(team))
            {
                failure = ResearchFailure.AlreadyResearching;
                return false;
            }

            var spec = ResearchCatalog.Get(kind);
            if (!BuildRules.CanAfford(Economy.GetCredits(economy, team), spec.Cost))
            {
                failure = ResearchFailure.NotEnoughCredits;
                return false;
            }

            Economy.TrySpend(ref economy, team, spec.Cost);
            entityManager.SetComponentData(economyEntity, economy);

            state.SetActive(team, true);
            state.SetKind(team, kind);
            state.SetTimer(team, spec.Seconds);
            entityManager.SetComponentData(research, state);
            return true;
        }

        public static string FailureText(ResearchFailure failure)
        {
            switch (failure)
            {
                case ResearchFailure.AlreadyResearched:
                    return "Already researched";
                case ResearchFailure.AlreadyResearching:
                    return "Research already in progress";
                case ResearchFailure.NotEnoughCredits:
                    return "Not enough credits";
                case ResearchFailure.NoEconomy:
                    return "Economy not initialised";
                default:
                    return string.Empty;
            }
        }
    }

    public static class ResearchQuery
    {
        public static Entity GetOrCreate(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ResearchState>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
            {
                for (var i = 1; i < entities.Length; i++)
                {
                    entityManager.DestroyEntity(entities[i]);
                }

                return entities[0];
            }

            var entity = entityManager.CreateEntity(typeof(ResearchState));
            entityManager.SetComponentData(entity, default(ResearchState));
            return entity;
        }

        public static bool TryGet(
            EntityManager entityManager,
            out Entity entity,
            out ResearchState state)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ResearchState>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0)
            {
                entity = Entity.Null;
                state = default;
                return false;
            }

            entity = entities[0];
            state = entityManager.GetComponentData<ResearchState>(entity);
            return true;
        }

        public static void Multipliers(
            EntityManager entityManager,
            int team,
            out float health,
            out float damage,
            out float speed)
        {
            health = 1f;
            damage = 1f;
            speed = 1f;

            if (!TryGet(entityManager, out _, out var state))
            {
                return;
            }

            if (state.Has(team, UpgradeKind.Armor))
            {
                health = 1.10f;
            }

            if (state.Has(team, UpgradeKind.Weapons))
            {
                damage = 1.10f;
            }

            if (state.Has(team, UpgradeKind.Logistics))
            {
                speed = 1.10f;
            }
        }
    }
}
