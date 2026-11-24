using CQRa.Sim.Core;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase79Tests
    {
        [Test]
        public void SaveMigration_NormalisesLegacyAndRejectsFuture()
        {
            Assert.IsNull(MatchSaveSerializer.Migrate(null));

            var legacy = new MatchSaveData
            {
                version = 0,
                mapId = null,
                teamCount = 99,
            };
            var migrated = MatchSaveSerializer.Migrate(legacy);
            Assert.IsNotNull(migrated);
            Assert.AreEqual(MatchSaveSerializer.CurrentVersion, migrated.version);
            Assert.AreEqual("ash-basin", migrated.mapId);
            Assert.AreEqual(8, migrated.teamCount);

            var future = new MatchSaveData { version = MatchSaveSerializer.CurrentVersion + 1 };
            Assert.IsNull(MatchSaveSerializer.Migrate(future));
        }

        [Test]
        public void SaveData_RoundTripsTimestamp()
        {
            var data = new MatchSaveData
            {
                version = MatchSaveSerializer.CurrentVersion,
                savedUtc = "2026-11-24T12:00:00.0000000Z",
                mapId = "ash-basin",
                teamCount = 4,
            };

            var loaded = MatchSaveSerializer.FromJson(MatchSaveSerializer.ToJson(data));
            Assert.AreEqual(data.savedUtc, loaded.savedUtc);
            Assert.AreEqual(4, loaded.teamCount);
        }

        [Test]
        public void SaveSlots_HaveDistinctPathsAndClampSelection()
        {
            var a = SaveSlots.PathFor(0);
            var b = SaveSlots.PathFor(1);
            var c = SaveSlots.PathFor(2);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(b, c);
            Assert.IsTrue(a.Contains("slot1"));
            Assert.IsTrue(c.Contains("slot3"));

            SaveSlots.Select(-5);
            Assert.AreEqual(0, SaveSlots.CurrentSlot);
            SaveSlots.Select(99);
            Assert.AreEqual(SaveSlots.Count - 1, SaveSlots.CurrentSlot);
            SaveSlots.Select(0);
        }
    }
}
