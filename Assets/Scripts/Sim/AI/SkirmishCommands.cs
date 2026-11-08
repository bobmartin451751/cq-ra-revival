using CQRa.Sim.Base;
using CQRa.Sim.Fog;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;

namespace CQRa.Sim.AI
{
    /// <summary>
    /// Match-setup commands shared by the skirmish UI and tests. The player always
    /// controls team 0; the AI controls team 1 and only ever gets one commander.
    /// </summary>
    public static class SkirmishCommands
    {
        public const int PlayerTeam = 0;
        public const int AiTeam = 1;

        public static void StartMatch(EntityManager entityManager, AiDifficulty difficulty)
        {
            ClearCommanders(entityManager);
            CreateCommander(entityManager, AiTeam, difficulty);
        }

        public static Entity CreateCommander(
            EntityManager entityManager,
            int team,
            AiDifficulty difficulty)
        {
            var entity = entityManager.CreateEntity(typeof(AiCommander));
            entityManager.SetComponentData(entity, new AiCommander
            {
                Team = team,
                Difficulty = difficulty,
                ThinkTimer = 0f,
            });

            return entity;
        }

        public static void ClearCommanders(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
            entities.Dispose();
        }

        public static bool HasCommander(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<AiCommander>(entities[i]).Team == team)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes every runtime match entity so a scene reload starts from a clean world.
        /// Scene-authored data (nav grid, terrain) is left alone; UnitSpawner and
        /// EconomyAuthoring rebuild their own content in Start.
        /// </summary>
        public static void ResetRuntimeState(EntityManager entityManager)
        {
            ClearCommanders(entityManager);
            DestroyAll<UnitTag>(entityManager);
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<OreNode>(entityManager);
            DestroyAll<EconomyState>(entityManager);
            DestroyAll<MatchState>(entityManager);
            DestroyAll<FlowFieldMeta>(entityManager);
            DestroyAll<FogState>(entityManager);
        }

        private static void DestroyAll<T>(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
            entities.Dispose();
        }

        /// <summary>Player-facing result label for a MatchState winner value.</summary>
        public static string ResultText(int winner)
        {
            if (winner == PlayerTeam)
            {
                return "VICTORY";
            }

            return winner == AiTeam ? "DEFEAT" : "DRAW";
        }
    }
}
