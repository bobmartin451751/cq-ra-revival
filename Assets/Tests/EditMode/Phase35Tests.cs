using System.Collections.Generic;
using CQRa.Sim.Content;
using CQRa.Sim.Navigation;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase35Tests
    {
        [Test]
        public void MapCatalog_HasFiftyDistinctMaps()
        {
            Assert.AreEqual(50, MapCatalog.Count);

            var byTier = new int[4];
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
                Assert.Greater(map.WorldSize, 0f);
                byTier[(int)map.SizeTier]++;
            }

            Assert.AreEqual(8, byTier[(int)MapSizeTier.Small]);
            Assert.AreEqual(12, byTier[(int)MapSizeTier.Medium]);
            Assert.AreEqual(12, byTier[(int)MapSizeTier.Large]);
            Assert.AreEqual(18, byTier[(int)MapSizeTier.Huge]);
        }

        [Test]
        public void MapStartsAndResources_AreInsideThePlayableBasin()
        {
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                MapRuntime.Select(i);
                var map = MapCatalog.Get(i);
                var limit = map.WorldSize * 0.48f;

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

                Assert.IsFalse(MapWater.ContainsAny(map.PlayerStart), $"{map.Name} player start is in water");
                Assert.IsFalse(MapWater.ContainsAny(map.AiStart), $"{map.Name} AI start is in water");

                foreach (var ore in map.OrePositions)
                {
                    Assert.Less(math.length(ore), limit, $"{map.Name} ore must stay on the map");
                    Assert.IsFalse(MapWater.ContainsAny(ore), $"{map.Name} ore is in water");
                }

                foreach (var crate in map.CratePositions)
                {
                    Assert.IsFalse(MapWater.ContainsAny(crate), $"{map.Name} crate is in water");
                }

                foreach (var obstacle in map.Obstacles)
                {
                    Assert.Greater(obstacle.Radius, 0f);
                    Assert.Greater(obstacle.Height, 0f);
                    Assert.Less(math.length(obstacle.Centre), limit);
                    Assert.IsFalse(
                        MapWater.ContainsAny(obstacle.Centre),
                        $"{map.Name} scenery is in water");
                }
            }

            MapRuntime.Reset();
        }

        [Test]
        public void MapWater_DescribesEachMap()
        {
            try
            {
                MapRuntime.Select(0);
                Assert.IsTrue(MapWater.HasWater);
                Assert.AreEqual(1, MapWater.BodyCount);
                Assert.AreEqual(WaterShapeKind.Circle, MapWater.Body(0).Kind);
                Assert.IsTrue(MapWater.Contains(0, MapWater.Centre(0)));

                MapRuntime.Select(1);
                Assert.IsFalse(MapWater.HasWater);
                Assert.AreEqual(0, MapWater.BodyCount);

                MapRuntime.Select(2);
                Assert.AreEqual(WaterShapeKind.Polygon, MapWater.Body(0).Kind);
                Assert.GreaterOrEqual(MapWater.Body(0).Points.Length, 3);
                Assert.IsTrue(MapWater.Contains(0, new float2(8f, 0f)));

                MapRuntime.Select(3);
                Assert.AreEqual(2, MapWater.BodyCount);
                Assert.IsTrue(MapWater.Contains(0, MapWater.Centre(0)));
                Assert.IsTrue(MapWater.Contains(1, MapWater.Centre(1)));

                MapRuntime.Select(4);
                Assert.AreEqual(WaterShapeKind.Polygon, MapWater.Body(0).Kind);
                Assert.IsTrue(MapWater.Contains(0, MapWater.Centre(0)));
            }
            finally
            {
                MapRuntime.Reset();
            }
        }

        [Test]
        public void MapWater_ClampsShipsAndFindsDockSites()
        {
            try
            {
                MapRuntime.Select(0);
                var circleClamped = MapWater.ClampInside(0, new float2(20f, 0f), 4f);
                Assert.IsTrue(MapWater.Contains(0, circleClamped));
                Assert.LessOrEqual(
                    math.distance(circleClamped, MapWater.Centre(0)),
                    MapWater.Body(0).Radius);

                var circleSite = MapWater.Centre(0) +
                                 new float2(MapWater.Body(0).Radius + 6f, 0f);
                Assert.IsTrue(
                    MapWater.TryFindDockSite(circleSite, 20f, 4f, out var bodyIndex, out var spawn),
                    "a point just outside the shoreline should accept a dock");
                Assert.AreEqual(0, bodyIndex);
                Assert.IsTrue(MapWater.Contains(bodyIndex, new float2(spawn.x, spawn.z)));

                MapRuntime.Select(1);
                Assert.IsFalse(
                    MapWater.TryFindDockSite(float2.zero, 20f, 4f, out _, out _),
                    "dry maps cannot host a dock");

                MapRuntime.Select(2);
                var polygonClamped = MapWater.ClampInside(0, new float2(90f, 90f), 4f);
                Assert.IsTrue(MapWater.Contains(0, polygonClamped));

                var polygonSite = new float2(50f, 0f);
                Assert.IsTrue(
                    MapWater.TryFindDockSite(polygonSite, 22f, 4f, out var polygonBody, out var polygonSpawn),
                    "the polygon shoreline should accept a dock");
                Assert.AreEqual(0, polygonBody);
                Assert.IsTrue(MapWater.Contains(polygonBody, new float2(polygonSpawn.x, polygonSpawn.z)));
            }
            finally
            {
                MapRuntime.Reset();
            }
        }

        [Test]
        public void NavGrid_BlocksEveryWaterShape()
        {
            var origin = new float2(-120f, -120f);
            const int cells = 120;
            const float cellSize = 2f;

            try
            {
                MapRuntime.Select(0);
                var circle = NavGridBuilder.Build(
                    cells, cells, cellSize, origin, 2f,
                    (x, z) => 0f,
                    System.Array.Empty<NavBlocker>(),
                    0f, 0f, MapWater.ContainsAny);
                Assert.AreEqual(0, circle[CellIndex(origin, cellSize, new float2(-100f, 0f))], "circle water is blocked");

                MapRuntime.Select(2);
                var polygon = NavGridBuilder.Build(
                    cells, cells, cellSize, origin, 2f,
                    (x, z) => 0f,
                    System.Array.Empty<NavBlocker>(),
                    0f, 0f, MapWater.ContainsAny);
                Assert.AreEqual(0, polygon[CellIndex(origin, cellSize, new float2(8f, 0f))], "polygon water is blocked");
                Assert.AreEqual(1, polygon[CellIndex(origin, cellSize, new float2(70f, 70f))], "land stays walkable");

                MapRuntime.Select(1);
                var dry = NavGridBuilder.Build(
                    cells, cells, cellSize, origin, 2f,
                    (x, z) => 0f,
                    System.Array.Empty<NavBlocker>(),
                    0f, 0f, MapWater.ContainsAny);
                Assert.AreEqual(1, dry[CellIndex(origin, cellSize, new float2(0f, 0f))], "dry maps have no water blockers");
            }
            finally
            {
                MapRuntime.Reset();
            }
        }

        private static int CellIndex(float2 origin, float cellSize, float2 world)
        {
            var x = (int)math.floor((world.x - origin.x) / cellSize);
            var z = (int)math.floor((world.y - origin.y) / cellSize);
            return z * 120 + x;
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
