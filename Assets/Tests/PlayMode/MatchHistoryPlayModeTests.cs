using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class MatchHistoryPlayModeTests
    {
        [Test]
        public void History_SamplesUnitsCreditsAndKills()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            MatchHistory.Reset();

            using var economyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            var economy = economyQuery.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(EconomyState))
                : economyQuery.GetSingletonEntity();
            entityManager.SetComponentData(economy, EconomyState.Create(750));

            var unit0 = UnitFactory.SpawnDefault(
                entityManager, new float3(0f, 0f, 0f), 0f, UnitKind.Infantry, 0);
            var unit1 = UnitFactory.SpawnDefault(
                entityManager, new float3(10f, 0f, 0f), 0f, UnitKind.Tank, 1);
            var statsEntity = MatchStatsCommands.Ensure(entityManager);
            var stats = entityManager.GetBuffer<TeamStats>(statsEntity);
            stats[0] = new TeamStats { Team = 0, Kills = 3 };

            try
            {
                MatchHistory.Capture(entityManager, 0);
                MatchHistory.Capture(entityManager, MatchHistory.SampleTicks);

                Assert.AreEqual(2, MatchHistory.Count);
                var first = MatchHistory.Samples[0];
                Assert.AreEqual(1, first.Units0);
                Assert.AreEqual(1, first.Units1);
                Assert.AreEqual(750, first.Credits0);
                Assert.AreEqual(3, first.Kills0);
                Assert.AreEqual(0, first.Kills1);
                Assert.AreEqual(MatchHistory.SampleTicks, MatchHistory.Samples[1].Tick);
            }
            finally
            {
                MatchHistory.Reset();
                if (entityManager.Exists(unit0))
                {
                    entityManager.DestroyEntity(unit0);
                }

                if (entityManager.Exists(unit1))
                {
                    entityManager.DestroyEntity(unit1);
                }

                if (entityManager.Exists(statsEntity))
                {
                    var buffer = entityManager.GetBuffer<TeamStats>(statsEntity);
                    for (var i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] = new TeamStats { Team = i };
                    }
                }

                if (entityManager.Exists(economy))
                {
                    entityManager.SetComponentData(economy, default(EconomyState));
                }
            }
        }
    }
}
