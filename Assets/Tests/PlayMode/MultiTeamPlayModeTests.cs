using System.Collections;
using CQRa.Sim.AI;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class MultiTeamPlayModeTests
    {
        [Test]
        public void UnitSpawner_DefaultsToMcvOnly()
        {
            var go = new UnityEngine.GameObject("SpawnerProbe");
            try
            {
                var spawner = go.AddComponent<UnitSpawner>();
                Assert.AreEqual(1, spawner.McvsPerTeam);
                Assert.AreEqual(0, spawner.HarvestersPerTeam);
                Assert.AreEqual(0, spawner.TanksPerTeam);
                Assert.AreEqual(0, spawner.InfantryPerTeam);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator StartMatch_EightTeams_CreatesSevenCommanders()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            try
            {
                SkirmishCommands.StartMatch(entityManager, 8, TeamMode.Teams, AiDifficulty.Veteran);

                Assert.AreEqual(8, MatchRuntime.TeamCount);
                Assert.AreEqual(TeamMode.Teams, MatchRuntime.Mode);

                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>());
                using var commanders = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(7, commanders.Length);

                var seen = new bool[8];
                for (var i = 0; i < commanders.Length; i++)
                {
                    var team = entityManager.GetComponentData<AiCommander>(commanders[i]).Team;
                    Assert.GreaterOrEqual(team, 1);
                    Assert.Less(team, 8);
                    seen[team] = true;
                }

                for (var team = 1; team < 8; team++)
                {
                    Assert.IsTrue(seen[team], $"missing AI commander for team {team}");
                }
            }
            finally
            {
                SkirmishCommands.ClearCommanders(entityManager);
                MatchRuntime.Reset();
                FactionRuntime.Reset();
            }
        }
    }
}
