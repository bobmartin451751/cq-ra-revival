using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace CQRa.Sim.Content
{
    /// <summary>
    /// Deterministic generator behind the in-game map editor. Same seed, tier, water
    /// and player count always produce the same map.
    /// </summary>
    public static class CustomMapFactory
    {
        public const string CustomMapId = "custom";

        public static float WorldSizeFor(MapSizeTier tier)
        {
            switch (tier)
            {
                case MapSizeTier.Small: return 220f;
                case MapSizeTier.Medium: return 320f;
                case MapSizeTier.Large: return 420f;
                default: return 520f;
            }
        }

        public static int MaxTeamsFor(MapSizeTier tier)
        {
            switch (tier)
            {
                case MapSizeTier.Small: return 2;
                case MapSizeTier.Medium: return 4;
                case MapSizeTier.Large: return 6;
                default: return 8;
            }
        }

        public static MapDefinition Build(
            int seed,
            MapSizeTier tier,
            bool withWater,
            int teamCount)
        {
            var worldSize = WorldSizeFor(tier);
            var maxPlayers = math.clamp(teamCount, 2, MaxTeamsFor(tier));
            var random = new System.Random(seed);
            var spawnRing = worldSize * 0.37f;
            var spawns = new MapSpawn[maxPlayers];
            for (var i = 0; i < maxPlayers; i++)
            {
                var angle = -math.PI * 0.5f + i * math.PI * 2f / maxPlayers;
                spawns[i] = new MapSpawn(
                    math.cos(angle) * spawnRing,
                    math.sin(angle) * spawnRing);
            }

            WaterBody[] water;
            if (withWater)
            {
                var angle = (float)random.NextDouble() * math.PI * 2f;
                var offset = worldSize * (0.06f + (float)random.NextDouble() * 0.10f);
                water = new[]
                {
                    WaterBody.Circle(
                        "Custom Lake",
                        math.cos(angle) * offset,
                        math.sin(angle) * offset,
                        worldSize * (0.08f + (float)random.NextDouble() * 0.025f),
                        0.55f),
                };
            }
            else
            {
                water = Array.Empty<WaterBody>();
            }

            var obstacles = new List<MapObstacle>();
            var attempts = 0;
            while (obstacles.Count < 7 && attempts++ < 120)
            {
                var angle = (float)random.NextDouble() * math.PI * 2f;
                var radius = worldSize * (0.16f + (float)random.NextDouble() * 0.24f);
                var point = new float2(math.cos(angle) * radius, math.sin(angle) * radius);
                if (ContainsWater(water, point) || NearSpawn(spawns, point, worldSize * 0.12f))
                {
                    continue;
                }

                obstacles.Add(new MapObstacle(
                    point.x,
                    point.y,
                    worldSize * (0.014f + (float)random.NextDouble() * 0.012f),
                    worldSize * (0.018f + (float)random.NextDouble() * 0.018f),
                    random.Next()));
            }

            var ore = CreatePoints(random, 12, worldSize, spawns, water, 0.30f, 0.40f);
            var crates = CreatePoints(random, 6, worldSize, spawns, water, 0.16f, 0.34f);

            return new MapDefinition
            {
                Id = CustomMapId,
                Name = $"Custom {seed}",
                Description = "Player-authored map from the in-game editor.",
                SizeTier = tier,
                WorldSize = worldSize,
                MinPlayers = 2,
                MaxPlayers = maxPlayers,
                Seed = seed,
                MaxHeight = 20f + (int)tier * 3f,
                NoiseScale = 4.4f,
                BasinRadius = worldSize * 0.42f,
                BasinBlend = worldSize * 0.09f,
                PlayerStart = spawns[0].Position,
                AiStart = spawns[1].Position,
                Spawns = spawns,
                WaterBodies = water,
                Obstacles = obstacles.ToArray(),
                OrePositions = ore,
                CratePositions = crates,
            };
        }

        private static float2[] CreatePoints(
            System.Random random,
            int count,
            float worldSize,
            MapSpawn[] spawns,
            WaterBody[] water,
            float minRadius,
            float maxRadius)
        {
            var points = new List<float2>(count);
            var attempts = 0;
            while (points.Count < count && attempts++ < 200)
            {
                var angle = (float)random.NextDouble() * math.PI * 2f;
                var radius = worldSize *
                             (minRadius + (float)random.NextDouble() * (maxRadius - minRadius));
                var point = new float2(math.cos(angle) * radius, math.sin(angle) * radius);
                if (ContainsWater(water, point) || NearSpawn(spawns, point, worldSize * 0.09f))
                {
                    continue;
                }

                points.Add(point);
            }

            return points.ToArray();
        }

        private static bool NearSpawn(MapSpawn[] spawns, float2 point, float distance)
        {
            for (var i = 0; i < spawns.Length; i++)
            {
                if (math.distance(spawns[i].Position, point) < distance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWater(WaterBody[] bodies, float2 point)
        {
            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body.Kind == WaterShapeKind.Circle)
                {
                    if (math.distancesq(point, body.Centre) < body.Radius * body.Radius)
                    {
                        return true;
                    }
                }
                else if (PointInPolygon(body.Points, point))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PointInPolygon(float2[] points, float2 point)
        {
            if (points == null || points.Length < 3)
            {
                return false;
            }

            var inside = false;
            for (var i = 0; i < points.Length; i++)
            {
                var j = (i + points.Length - 1) % points.Length;
                var pi = points[i];
                var pj = points[j];
                if ((pi.y > point.y) != (pj.y > point.y) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
