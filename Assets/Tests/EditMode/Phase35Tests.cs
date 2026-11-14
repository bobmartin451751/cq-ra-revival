using System.Collections.Generic;
using CQRa.Sim.Content;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase35Tests
    {
        [Test]
        public void MapCatalog_HasThreeDistinctMaps()
        {
            Assert.AreEqual(3, MapCatalog.Count);

            var ids = new HashSet<string>();
            var seeds = new HashSet<int>();

            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                Assert.IsNotNull(map);
                Assert.IsTrue(ids.Add(map.Id), $"duplicate map id {map.Id}");
                Assert.IsTrue(seeds.Add(map.Seed), $"duplicate map seed {map.Seed}");
                Assert.IsFalse(string.IsNullOrEmpty(map.Name));
                Assert.IsFalse(string.IsNullOrEmpty(map.Description));
                Assert.Greater(map.BasinRadius, 0f);
                Assert.Greater(map.MaxHeight, 0f);
            }
        }

        [Test]
        public void MapStartsAndResources_AreInsideThePlayableBasin()
        {
            var limit = 120f;

            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);

                Assert.Less(
                    math.length(map.PlayerStart),
                    map.BasinRadius,
                    $"{map.Name} player start must be on flat ground");
                Assert.Less(
                    math.length(map.AiStart),
                    map.BasinRadius,
                    $"{map.Name} AI start must be on flat ground");
                Assert.Greater(
                    math.distance(map.PlayerStart, map.AiStart),
                    48f,
                    $"{map.Name} starts must be separated");

                Assert.GreaterOrEqual(map.Obstacles.Length, 4, $"{map.Name} needs scenery");
                Assert.GreaterOrEqual(map.OrePositions.Length, 8, $"{map.Name} needs ore");
                Assert.GreaterOrEqual(map.CratePositions.Length, 4, $"{map.Name} needs crates");

                foreach (var ore in map.OrePositions)
                {
                    Assert.Less(math.length(ore), limit, $"{map.Name} ore must stay on the map");
                }

                foreach (var obstacle in map.Obstacles)
                {
                    Assert.Greater(obstacle.Radius, 0f);
                    Assert.Greater(obstacle.Height, 0f);
                    Assert.Less(math.length(obstacle.Centre), limit);
                }
            }
        }

        [Test]
        public void MapRuntime_SelectsAndClamps()
        {
            MapRuntime.Reset();

            try
            {
                Assert.AreEqual(0, MapRuntime.SelectedIndex);
                Assert.AreEqual("ash-basin", MapRuntime.Current.Id);

                MapRuntime.Select(2);
                Assert.AreEqual(2, MapRuntime.SelectedIndex);
                Assert.AreEqual("broken-ridge", MapRuntime.Current.Id);

                MapRuntime.Select(99);
                Assert.AreEqual(MapCatalog.Count - 1, MapRuntime.SelectedIndex);

                MapRuntime.Select(-4);
                Assert.AreEqual(0, MapRuntime.SelectedIndex);
            }
            finally
            {
                MapRuntime.Reset();
            }
        }
    }
}
