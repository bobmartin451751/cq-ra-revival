using CQRa.Sim.Campaign;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase88Tests
    {
        [Test]
        public void NextMissions_ExposeEveryBranch()
        {
            MissionCatalog.Load();
            var first = MissionCatalog.NextMissionIndices(0);
            Assert.AreEqual(2, first.Length);
            Assert.AreEqual("m2", MissionCatalog.Missions[first[0]].id);
            Assert.AreEqual("m3", MissionCatalog.Missions[first[1]].id);
            Assert.AreEqual(first[0], MissionCatalog.NextMissionIndex(0));

            var mid = MissionCatalog.NextMissionIndices(3);
            Assert.AreEqual(2, mid.Length);
            Assert.AreEqual("m6", MissionCatalog.Missions[mid[0]].id);
            Assert.AreEqual("m7", MissionCatalog.Missions[mid[1]].id);
        }

        [Test]
        public void NextMissions_HandleFinaleAndInvalidIndexes()
        {
            MissionCatalog.Load();
            var finale = MissionCatalog.NextMissionIndices(MissionCatalog.Missions.Length - 1);
            Assert.IsEmpty(finale);
            Assert.AreEqual(-1, MissionCatalog.NextMissionIndex(MissionCatalog.Missions.Length - 1));
            Assert.IsEmpty(MissionCatalog.NextMissionIndices(-1));
            Assert.IsEmpty(MissionCatalog.NextMissionIndices(999));
        }
    }
}
