using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase37Tests
    {
        [Test]
        public void Economy_SupportsEightTeams()
        {
            var state = EconomyState.Create(3000);

            for (var team = 0; team < EconomyState.TeamCount; team++)
            {
                Assert.AreEqual(3000, Economy.GetCredits(state, team));
                Economy.SetCredits(ref state, team, 100 + team);
                Economy.SetPower(ref state, team, 10 + team, team);
            }

            for (var team = 0; team < EconomyState.TeamCount; team++)
            {
                Assert.AreEqual(100 + team, Economy.GetCredits(state, team));
                Assert.AreEqual(10 + team, Economy.GetPowerMade(state, team));
                Assert.AreEqual(team, Economy.GetPowerUsed(state, team));
            }
        }

        [Test]
        public void MatchRuntime_SupportsFreeForAllAndTeams()
        {
            try
            {
                MatchRuntime.Configure(8, TeamMode.FreeForAll);
                Assert.AreEqual(8, MatchRuntime.TeamCount);
                Assert.IsFalse(MatchRuntime.AreAllied(0, 1));
                Assert.IsTrue(MatchRuntime.AreAllied(7, 7));

                MatchRuntime.Configure(8, TeamMode.Teams);
                Assert.IsTrue(MatchRuntime.AreAllied(0, 3));
                Assert.IsTrue(MatchRuntime.AreAllied(4, 7));
                Assert.IsFalse(MatchRuntime.AreAllied(0, 4));

                MatchRuntime.Configure(99, TeamMode.Teams);
                Assert.AreEqual(8, MatchRuntime.TeamCount);
            }
            finally
            {
                MatchRuntime.Reset();
            }
        }

        [Test]
        public void MapCatalog_HasAPlayerCountForEverySize()
        {
            var small = 0;
            var medium = 0;
            var large = 0;
            var huge = 0;

            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                switch (map.SizeTier)
                {
                    case MapSizeTier.Small: small++; break;
                    case MapSizeTier.Medium: medium++; break;
                    case MapSizeTier.Large: large++; break;
                    default: huge++; break;
                }

                var spawnCount = map.Spawns != null && map.Spawns.Length > 0
                    ? map.Spawns.Length
                    : 2;
                Assert.AreEqual(map.MaxPlayers, spawnCount, map.Name);
                for (var team = 0; team < spawnCount; team++)
                {
                    var spawn = map.SpawnFor(team);
                    Assert.Less(
                        math.length(spawn.Position),
                        map.BasinRadius,
                        $"{map.Name} spawn {team} must be in the basin");
                }
            }

            Assert.AreEqual(8, small);
            Assert.AreEqual(12, medium);
            Assert.AreEqual(12, large);
            Assert.AreEqual(18, huge);
        }
    }
}
