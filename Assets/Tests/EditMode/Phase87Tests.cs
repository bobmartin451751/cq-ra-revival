using CQRa.Sim.Campaign;
using CQRa.Sim.Content;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase87Tests
    {
        [Test]
        public void Briefing_ContainsObjectiveCodexAndNextBranches()
        {
            MissionCatalog.Load();
            var mission = MissionCatalog.Missions[0];
            var map = MapCatalog.Get(MapCatalog.FindIndex(mission.mapId));
            var text = CampaignBriefing.Text(mission, map.Name);

            StringAssert.Contains("OBJECTIVE", text);
            StringAssert.Contains(mission.objectiveText, text);
            StringAssert.Contains(mission.briefing, text);
            StringAssert.Contains(mission.codex, text);
            StringAssert.Contains(map.Name, text);
            StringAssert.Contains("NEXT", text);

            var next = MissionCatalog.NextMissionNames(mission);
            StringAssert.Contains(MissionCatalog.Missions[1].name, next);
            StringAssert.Contains(MissionCatalog.Missions[2].name, next);
        }

        [Test]
        public void Briefing_FinaleHasNoNextMission()
        {
            MissionCatalog.Load();
            var finale = MissionCatalog.Missions[MissionCatalog.Missions.Length - 1];
            Assert.AreEqual("Campaign finale", MissionCatalog.NextMissionNames(finale));
            Assert.AreEqual(string.Empty, CampaignBriefing.Text(null, "x"));
        }
    }
}
