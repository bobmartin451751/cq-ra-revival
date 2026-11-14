using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase38Tests
    {
        [Test]
        public void Harvester_IsProducedByRefinery()
        {
            Assert.AreEqual(
                BuildingKind.Refinery,
                ProductionCatalog.Get(UnitKind.Harvester).Producer,
                "harvesters should come from refineries");
        }

        [Test]
        public void EverySpawn_HasRoomForABase()
        {
            try
            {
                for (var mapIndex = 0; mapIndex < MapCatalog.Count; mapIndex++)
                {
                    MapRuntime.Select(mapIndex);
                    var map = MapCatalog.Get(mapIndex);
                    var spawnCount = map.Spawns != null && map.Spawns.Length > 0
                        ? map.Spawns.Length
                        : 2;

                    for (var team = 0; team < spawnCount; team++)
                    {
                        var spawn = map.SpawnFor(team);
                        Assert.IsFalse(
                            MapWater.ContainsAny(spawn.Position),
                            $"{map.Name} team {team} start is in water");

                        var buildSites = 0;
                        for (var ring = 0; ring < 4; ring++)
                        {
                            var radius = 20f + ring * 6f;
                            for (var step = 0; step < 16; step++)
                            {
                                var angle = step * math.PI / 8f;
                                var point = spawn.Position +
                                            new float2(math.cos(angle), math.sin(angle)) * radius;

                                if (math.length(point) > map.WorldSize * 0.5f - 3f ||
                                    MapWater.ContainsAny(point) ||
                                    InsideObstacle(map, point))
                                {
                                    continue;
                                }

                                buildSites++;
                            }
                        }

                        Assert.GreaterOrEqual(
                            buildSites,
                            8,
                            $"{map.Name} team {team} needs enough free base sites");
                    }
                }
            }
            finally
            {
                MapRuntime.Reset();
            }
        }

        [Test]
        public void EveryWaterBody_CanHostADock()
        {
            try
            {
                for (var mapIndex = 0; mapIndex < MapCatalog.Count; mapIndex++)
                {
                    MapRuntime.Select(mapIndex);
                    var map = MapCatalog.Get(mapIndex);
                    if (!map.HasWater)
                    {
                        continue;
                    }

                    for (var bodyIndex = 0; bodyIndex < map.WaterBodies.Length; bodyIndex++)
                    {
                        var body = map.WaterBodies[bodyIndex];
                        var centre = MapWater.Centre(bodyIndex);
                        var extent = body.Kind == WaterShapeKind.Circle
                            ? body.Radius
                            : PolygonRadius(body.Points, centre);

                        var found = false;
                        for (var step = 0; step < 32 && !found; step++)
                        {
                            var angle = step * math.PI / 16f;
                            var site = centre +
                                       new float2(math.cos(angle), math.sin(angle)) * (extent + 8f);

                            if (!MapWater.TryFindDockSite(site, 24f, 8f, out var foundBody, out var spawn))
                            {
                                continue;
                            }

                            if (foundBody != bodyIndex)
                            {
                                continue;
                            }

                            Assert.IsTrue(
                                MapWater.Contains(foundBody, new float2(spawn.x, spawn.z)),
                                $"{map.Name} dock spawn must be in water");
                            found = true;
                        }

                        Assert.IsTrue(
                            found,
                            $"{map.Name} water body {bodyIndex} has no dock site");
                    }
                }
            }
            finally
            {
                MapRuntime.Reset();
            }
        }

        private static bool InsideObstacle(MapDefinition map, float2 point)
        {
            if (map.Obstacles == null)
            {
                return false;
            }

            for (var i = 0; i < map.Obstacles.Length; i++)
            {
                var obstacle = map.Obstacles[i];
                if (math.distance(point, obstacle.Centre) < obstacle.Radius + 9f)
                {
                    return true;
                }
            }

            return false;
        }

        private static float PolygonRadius(float2[] points, float2 centre)
        {
            var max = 0f;
            for (var i = 0; i < points.Length; i++)
            {
                max = math.max(max, math.distance(points[i], centre));
            }

            return max;
        }
    }
}
