using System.Collections;
using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class SkirmishPlayModeTests
    {
        [UnityTest]
        public IEnumerator StartMatch_CreatesExactlyOneAiCommander()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            SkirmishCommands.ClearCommanders(entityManager);

            try
            {
                SkirmishCommands.StartMatch(entityManager, AiDifficulty.Hardened);

                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>());
                using var commanders = query.ToEntityArray(Allocator.Temp);

                Assert.AreEqual(1, commanders.Length, "only the AI team gets a commander");
                var commander = entityManager.GetComponentData<AiCommander>(commanders[0]);
                Assert.AreEqual(SkirmishCommands.AiTeam, commander.Team);
                Assert.AreEqual(AiDifficulty.Hardened, commander.Difficulty);
                Assert.IsFalse(
                    SkirmishCommands.HasCommander(entityManager, SkirmishCommands.PlayerTeam),
                    "the player's team must never be AI-controlled");

                // Starting a second match replaces the commander instead of stacking one.
                SkirmishCommands.StartMatch(entityManager, AiDifficulty.Brutal);
                using var replaced = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, replaced.Length);
                Assert.AreEqual(
                    AiDifficulty.Brutal,
                    entityManager.GetComponentData<AiCommander>(replaced[0]).Difficulty);
            }
            finally
            {
                SkirmishCommands.ClearCommanders(entityManager);
            }
        }
        [UnityTest]
        public IEnumerator ResetRuntimeState_ClearsPriorMatchEntities()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            entityManager.CreateEntity(typeof(UnitTag));
            entityManager.CreateEntity(typeof(BuildingTag));
            entityManager.CreateEntity(typeof(OreNode));
            entityManager.CreateEntity(typeof(EconomyState));
            entityManager.CreateEntity(typeof(MatchState));
            SkirmishCommands.CreateCommander(entityManager, SkirmishCommands.AiTeam, AiDifficulty.Recruit);

            SkirmishCommands.ResetRuntimeState(entityManager);

            Assert.AreEqual(0, Count<UnitTag>(entityManager));
            Assert.AreEqual(0, Count<BuildingTag>(entityManager));
            Assert.AreEqual(0, Count<OreNode>(entityManager));
            Assert.AreEqual(0, Count<EconomyState>(entityManager));
            Assert.AreEqual(0, Count<MatchState>(entityManager));
            Assert.AreEqual(0, Count<AiCommander>(entityManager));
        }

        private static int Count<T>(EntityManager entityManager) where T : struct, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }
    }
}
