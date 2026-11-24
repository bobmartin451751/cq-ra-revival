using CQRa.Sim.Campaign;
using CQRa.Sim.Content;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase75Tests
    {
        [Test]
        public void CampaignGraph_IsHandAuthoredAndValid()
        {
            MissionCatalog.Load();
            var missions = MissionCatalog.Missions;
            Assert.AreEqual(15, missions.Length);
            Assert.IsTrue(MissionCatalog.IsUnlocked(0), "the first mission is always open");

            for (var i = 0; i < missions.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(missions[i].id));
                Assert.IsFalse(string.IsNullOrEmpty(missions[i].codex),
                    $"mission {i} needs codex text");
                Assert.GreaterOrEqual(
                    MapCatalog.FindIndex(missions[i].mapId), 0,
                    $"mission {i} must point at a real map");
            }

            Assert.AreEqual(2, missions[0].next.Length, "mission 1 splits into two branches");
            Assert.AreEqual(2, missions[6].requires.Length, "mission 7 can be reached by two routes");
            Assert.AreEqual(2, missions[14].requires.Length, "the finale joins both late branches");
            Assert.AreEqual("m15", missions[13].next[0]);
            Assert.AreEqual(-1, MissionCatalog.NextMissionIndex(14));
        }

        [Test]
        public void CampaignGraph_RoutesResolveToMissions()
        {
            MissionCatalog.Load();
            var missions = MissionCatalog.Missions;

            for (var i = 0; i < missions.Length; i++)
            {
                if (missions[i].next == null)
                {
                    continue;
                }

                for (var n = 0; n < missions[i].next.Length; n++)
                {
                    Assert.GreaterOrEqual(
                        MissionCatalog.FindIndex(missions[i].next[n]), 0,
                        $"next id {missions[i].next[n]} must exist");
                }

                if (missions[i].requires != null)
                {
                    for (var r = 0; r < missions[i].requires.Length; r++)
                    {
                        Assert.GreaterOrEqual(
                            MissionCatalog.FindIndex(missions[i].requires[r]), 0,
                            $"required id {missions[i].requires[r]} must exist");
                    }
                }
            }

            Assert.AreEqual(1, MissionCatalog.NextMissionIndex(0));
            Assert.AreEqual(5, MissionCatalog.NextMissionIndex(3));
        }
    }
}
