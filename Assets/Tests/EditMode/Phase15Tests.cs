using CQRa.Sim.AI;
using CQRa.Sim.Campaign;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase15Tests
    {
        [Test]
        public void MissionCatalog_HasPlayableMissions()
        {
            Assert.IsNotNull(MissionCatalog.Missions);
            Assert.GreaterOrEqual(MissionCatalog.Missions.Length, 3);

            var first = MissionCatalog.Missions[0];
            Assert.IsFalse(string.IsNullOrEmpty(first.name));
            Assert.IsFalse(string.IsNullOrEmpty(first.briefing));
            Assert.IsFalse(string.IsNullOrEmpty(first.objective));
            Assert.AreEqual(AiDifficulty.Recruit, MissionCatalog.DifficultyFor(first));
        }

        [Test]
        public void MissionObjectives_AreSupported()
        {
            foreach (var mission in MissionCatalog.Missions)
            {
                Assert.IsTrue(
                    mission.objective == "destroyAll" ||
                    mission.objective == "survive" ||
                    mission.objective == "destroyYard",
                    $"unknown objective on {mission.name}");
            }

            var survive = System.Array.Find(
                MissionCatalog.Missions,
                m => m.objective == "survive");
            Assert.IsNotNull(survive);
            Assert.Greater(survive.surviveSeconds, 0f);
        }

        [Test]
        public void FirstMission_IsAlwaysUnlocked()
        {
            Assert.IsTrue(MissionCatalog.IsUnlocked(0));
        }
    }
}
