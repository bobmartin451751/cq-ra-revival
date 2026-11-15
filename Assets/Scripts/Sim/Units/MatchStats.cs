using CQRa.Sim.Base;
using Unity.Entities;

namespace CQRa.Sim.Units
{
    /// <summary>Singleton marker for per-team match statistics.</summary>
    public struct MatchStats : IComponentData
    {
    }

    /// <summary>One team's running combat/production totals.</summary>
    public struct TeamStats : IBufferElementData
    {
        public int Team;
        public int Kills;
        public int Losses;
        public int UnitsBuilt;
        public int BuildingsDestroyed;
        public int BuildingsLost;
    }

    public static class MatchStatsCommands
    {
        public static Entity Ensure(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchStats>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = entityManager.CreateEntity(typeof(MatchStats));
            var buffer = entityManager.AddBuffer<TeamStats>(entity);
            buffer.ResizeUninitialized(MatchRuntime.MaxTeams);
            for (var team = 0; team < buffer.Length; team++)
            {
                buffer[team] = new TeamStats { Team = team };
            }

            return entity;
        }

        public static void AddKill(EntityManager entityManager, int attackerTeam, Entity victim)
        {
            var stats = Ensure(entityManager);
            var buffer = entityManager.GetBuffer<TeamStats>(stats);
            var targetIsBuilding = entityManager.HasComponent<BuildingInfo>(victim);
            if (attackerTeam >= 0 && attackerTeam < buffer.Length)
            {
                var row = buffer[attackerTeam];
                row.Kills++;
                if (targetIsBuilding)
                {
                    row.BuildingsDestroyed++;
                }

                buffer[attackerTeam] = row;
            }

            if (!TryGetTeam(entityManager, victim, out var victimTeam) ||
                victimTeam < 0 ||
                victimTeam >= buffer.Length)
            {
                return;
            }

            var victimRow = buffer[victimTeam];
            victimRow.Losses++;
            if (targetIsBuilding)
            {
                victimRow.BuildingsLost++;
            }

            buffer[victimTeam] = victimRow;
        }

        public static void AddBuilt(EntityManager entityManager, int team)
        {
            var stats = Ensure(entityManager);
            var buffer = entityManager.GetBuffer<TeamStats>(stats);
            if (team < 0 || team >= buffer.Length)
            {
                return;
            }

            var row = buffer[team];
            row.UnitsBuilt++;
            buffer[team] = row;
        }

        public static bool TryRead(EntityManager entityManager, int team, out TeamStats stats)
        {
            stats = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchStats>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var buffer = entityManager.GetBuffer<TeamStats>(query.GetSingletonEntity(), true);
            if (team < 0 || team >= buffer.Length)
            {
                return false;
            }

            stats = buffer[team];
            return true;
        }

        private static bool TryGetTeam(EntityManager entityManager, Entity entity, out int team)
        {
            if (entityManager.HasComponent<UnitInfo>(entity))
            {
                team = entityManager.GetComponentData<UnitInfo>(entity).Team;
                return true;
            }

            if (entityManager.HasComponent<BuildingInfo>(entity))
            {
                team = entityManager.GetComponentData<BuildingInfo>(entity).Team;
                return true;
            }

            team = 0;
            return false;
        }
    }
}
