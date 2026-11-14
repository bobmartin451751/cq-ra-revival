using System.Collections.Generic;
using CQRa.Sim.AI;
using CQRa.Sim.Campaign;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase34Tests
    {
        [Test]
        public void MissionCatalog_LoadsFifteenCampaignMissions()
        {
            MissionCatalog.Load();

            Assert.IsTrue(MissionCatalog.LoadedFromDisk, "missions.json must be present");
            Assert.AreEqual(15, MissionCatalog.Missions.Length);

            var ids = new HashSet<string>();
            foreach (var mission in MissionCatalog.Missions)
            {
                Assert.IsFalse(string.IsNullOrEmpty(mission.id));
                Assert.IsTrue(ids.Add(mission.id), $"duplicate mission id {mission.id}");
                Assert.IsFalse(string.IsNullOrEmpty(mission.name));
                Assert.IsFalse(string.IsNullOrEmpty(mission.briefing));
                Assert.IsFalse(string.IsNullOrEmpty(mission.objectiveText));
                Assert.IsFalse(string.IsNullOrEmpty(mission.playerFaction));
                Assert.IsFalse(string.IsNullOrEmpty(mission.aiFaction));
                Assert.AreNotEqual(mission.playerFaction, mission.aiFaction);
                Assert.GreaterOrEqual(mission.teamCount, 2);
                Assert.LessOrEqual(mission.teamCount, 8);

                var mapIndex = MapCatalog.FindIndex(mission.mapId);
                Assert.GreaterOrEqual(mapIndex, 0, $"missing map {mission.mapId}");
                var map = MapCatalog.Get(mapIndex);
                Assert.LessOrEqual(mission.teamCount, map.MaxPlayers,
                    $"{mission.name} needs a map that supports its team count");

                Assert.IsTrue(System.Enum.TryParse(mission.difficulty, true, out AiDifficulty _),
                    $"unknown difficulty on {mission.name}");
            }
        }

        [Test]
        public void Campaign_EndsWithAnEightPlayerBrutalFinale()
        {
            MissionCatalog.Load();

            var final = MissionCatalog.Missions[MissionCatalog.Missions.Length - 1];
            Assert.AreEqual("destroyAll", final.objective);
            Assert.AreEqual(AiDifficulty.Brutal, MissionCatalog.DifficultyFor(final));
            Assert.AreEqual(FactionId.Meridian, FactionCatalog.Parse(final.playerFaction));
            Assert.AreEqual(FactionId.Ashfall, FactionCatalog.Parse(final.aiFaction));
            Assert.AreEqual(8, final.teamCount);
            Assert.GreaterOrEqual(MapCatalog.FindIndex(final.mapId), 0);
        }

        [Test]
        public void Campaign_UsesAllThreeFactionsAcrossMissions()
        {
            MissionCatalog.Load();

            var seen = new HashSet<FactionId>();
            foreach (var mission in MissionCatalog.Missions)
            {
                seen.Add(FactionCatalog.Parse(mission.playerFaction));
                seen.Add(FactionCatalog.Parse(mission.aiFaction));
            }

            Assert.IsTrue(seen.Contains(FactionId.Ashfall));
            Assert.IsTrue(seen.Contains(FactionId.Verdant));
            Assert.IsTrue(seen.Contains(FactionId.Meridian));
        }
    }
}
