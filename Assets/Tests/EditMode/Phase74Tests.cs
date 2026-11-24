using CQRa.Sim.Content;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase74Tests
    {
        [Test]
        public void CustomMap_IsDeterministic()
        {
            var a = CustomMapFactory.Build(777, MapSizeTier.Medium, true, 4);
            var b = CustomMapFactory.Build(777, MapSizeTier.Medium, true, 4);

            Assert.AreEqual(a.Id, b.Id);
            Assert.AreEqual(a.WorldSize, b.WorldSize);
            Assert.AreEqual(a.Spawns.Length, b.Spawns.Length);
            Assert.AreEqual(a.WaterBodies.Length, b.WaterBodies.Length);
            Assert.AreEqual(a.WaterBodies[0].Centre, b.WaterBodies[0].Centre);
            Assert.AreEqual(a.Obstacles.Length, b.Obstacles.Length);
            Assert.AreEqual(a.OrePositions.Length, b.OrePositions.Length);
            Assert.AreEqual(a.CratePositions.Length, b.CratePositions.Length);
            Assert.AreEqual(a.Seed, b.Seed);

            var c = CustomMapFactory.Build(778, MapSizeTier.Medium, true, 4);
            Assert.AreNotEqual(
                a.WaterBodies[0].Centre,
                c.WaterBodies[0].Centre,
                "a different seed must move the water");
        }

        [Test]
        public void CustomMap_ClampsPlayersToTier()
        {
            var small = CustomMapFactory.Build(1, MapSizeTier.Small, false, 8);
            Assert.AreEqual(2, small.MaxPlayers);
            Assert.AreEqual(2, small.Spawns.Length);
            Assert.AreEqual(0, small.WaterBodies.Length);

            var huge = CustomMapFactory.Build(1, MapSizeTier.Huge, true, 8);
            Assert.AreEqual(8, huge.MaxPlayers);
            Assert.AreEqual(520f, huge.WorldSize);
            Assert.AreEqual(1, huge.WaterBodies.Length);
        }

        [Test]
        public void CustomMapStore_RoundTripsSettings()
        {
            var data = CustomMapStore.Capture(9001, MapSizeTier.Large, true, 5);
            var loaded = CustomMapStore.FromJson(CustomMapStore.ToJson(data));
            Assert.IsNotNull(loaded);
            Assert.AreEqual(9001, loaded.seed);
            Assert.AreEqual((int)MapSizeTier.Large, loaded.tier);
            Assert.AreEqual(1, loaded.water);
            Assert.AreEqual(5, loaded.teams);

            var map = CustomMapStore.BuildMap(loaded);
            Assert.IsNotNull(map);
            Assert.AreEqual(MapSizeTier.Large, map.SizeTier);
            Assert.AreEqual(420f, map.WorldSize);
            Assert.AreEqual(5, map.MaxPlayers);
        }

        [Test]
        public void MapRuntime_CustomMapOverridesCatalogUntilCleared()
        {
            var map = CustomMapFactory.Build(42, MapSizeTier.Medium, false, 3);
            MapRuntime.SelectCustom(map);
            Assert.IsTrue(MapRuntime.HasCustom);
            Assert.AreSame(map, MapRuntime.Current);

            MapRuntime.Select(2);
            Assert.IsFalse(MapRuntime.HasCustom);
            Assert.AreEqual(2, MapRuntime.SelectedIndex);
            Assert.AreSame(MapCatalog.Get(2), MapRuntime.Current);

            MapRuntime.Reset();
        }
    }
}
