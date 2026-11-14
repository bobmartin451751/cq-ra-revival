using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace CQRa.Sim.Content
{
    /// <summary>A circular ruined outcrop that blocks navigation and sight lines.</summary>
    public struct MapObstacle
    {
        public float2 Centre;
        public float Radius;
        public float Height;
        public int Seed;

        public MapObstacle(float x, float z, float radius, float height, int seed)
        {
            Centre = new float2(x, z);
            Radius = radius;
            Height = height;
            Seed = seed;
        }
    }

    public enum WaterShapeKind : byte
    {
        Circle = 0,
        Polygon = 1,
    }

    public enum MapSizeTier : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        Huge = 3,
    }

    public struct MapSpawn
    {
        public float2 Position;
        public float Yaw;

        public MapSpawn(float x, float z, float yaw = 0f)
        {
            Position = new float2(x, z);
            Yaw = yaw;
        }
    }

    /// <summary>
    /// One connected water area. A body is either a circle or a simple polygon.
    /// Polygon points are world-space XZ and must not self-intersect.
    /// </summary>
    public sealed class WaterBody
    {
        public string Name;
        public WaterShapeKind Kind;
        public float2 Centre;
        public float Radius;
        public float2[] Points;
        public float Level;

        public static WaterBody Circle(string name, float x, float z, float radius, float level)
        {
            return new WaterBody
            {
                Name = name,
                Kind = WaterShapeKind.Circle,
                Centre = new float2(x, z),
                Radius = radius,
                Level = level,
            };
        }

        public static WaterBody Polygon(string name, float level, params float2[] points)
        {
            return new WaterBody
            {
                Name = name,
                Kind = WaterShapeKind.Polygon,
                Points = points,
                Level = level,
            };
        }
    }

    /// <summary>
    /// Hand-authored skirmish map. Terrain shape, scenery, water, ore, crates and both
    /// starting positions are data; water bodies are either circles or simple polygons.
    /// </summary>
    public sealed class MapDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public MapSizeTier SizeTier;
        public float WorldSize = 360f;
        public int MinPlayers = 2;
        public int MaxPlayers = 2;
        public int Seed;
        public float MaxHeight;
        public float NoiseScale;
        public float BasinRadius;
        public float BasinBlend;
        public float2 PlayerStart;
        public float2 AiStart;
        public MapSpawn[] Spawns;
        public MapObstacle[] Obstacles;
        public WaterBody[] WaterBodies;
        public float2[] OrePositions;
        public float2[] CratePositions;

        public bool HasWater => WaterBodies != null && WaterBodies.Length > 0;

        public MapSpawn SpawnFor(int team)
        {
            if (Spawns != null && team >= 0 && team < Spawns.Length)
            {
                return Spawns[team];
            }

            if (team == 0)
            {
                return new MapSpawn(PlayerStart.x, PlayerStart.y, 0f);
            }

            if (team == 1)
            {
                return new MapSpawn(AiStart.x, AiStart.y, 3.14159265f);
            }

            var angle = team * 6.2831853f / 8f;
            var radius = BasinRadius * 0.72f;
            return new MapSpawn(
                math.cos(angle) * radius,
                math.sin(angle) * radius);
        }
    }

    /// <summary>
    /// Three deterministic skirmish maps. Map 0 is the original Ash Basin layout so
    /// existing saves, screenshots and tests keep their familiar ground.
    /// </summary>
    public static class MapCatalog
    {
        private static readonly MapDefinition[] Authored =
        {
            new MapDefinition
            {
                Id = "ash-basin",
                Name = "Ash Basin",
                Description = "Open central basin, scattered ruins and a western lake.",
                Seed = 1337,
                MaxHeight = 28f,
                NoiseScale = 3.8f,
                BasinRadius = 46f,
                BasinBlend = 16f,
                PlayerStart = new float2(0f, -32f),
                AiStart = new float2(0f, 32f),
                WaterBodies = new[]
                {
                    WaterBody.Circle("West Lake", -100f, 0f, 28f, 0.55f),
                },
                Obstacles = new[]
                {
                    new MapObstacle(-24f, 6f, 7f, 9f, 11),
                    new MapObstacle(28f, 14f, 6f, 7f, 23),
                    new MapObstacle(6f, -26f, 8f, 11f, 37),
                    new MapObstacle(-34f, -20f, 5f, 6f, 51),
                    new MapObstacle(40f, -38f, 4f, 5f, 67),
                },
                OrePositions = new[]
                {
                    new float2(-70f, 0f),
                    new float2(70f, 0f),
                    new float2(0f, 72f),
                    new float2(0f, -72f),
                    new float2(-96f, -58f),
                    new float2(96f, 58f),
                    new float2(-58f, 96f),
                    new float2(58f, -96f),
                    new float2(-104f, 30f),
                    new float2(104f, -30f),
                },
                CratePositions = new[]
                {
                    new float2(0f, 0f),
                    new float2(-46f, 44f),
                    new float2(52f, -34f),
                    new float2(-64f, -46f),
                    new float2(72f, 18f),
                    new float2(24f, -58f),
                },
            },
            new MapDefinition
            {
                Id = "rusted-flats",
                Name = "Rusted Flats",
                Description = "Wide dry low ground with long sight lines and no naval access.",
                Seed = 4241,
                MaxHeight = 14f,
                NoiseScale = 5.6f,
                BasinRadius = 52f,
                BasinBlend = 18f,
                PlayerStart = new float2(-34f, -30f),
                AiStart = new float2(34f, 30f),
                WaterBodies = System.Array.Empty<WaterBody>(),
                Obstacles = new[]
                {
                    new MapObstacle(-16f, 0f, 9f, 7f, 103),
                    new MapObstacle(20f, 4f, 7f, 8f, 119),
                    new MapObstacle(-48f, 28f, 6f, 5f, 131),
                    new MapObstacle(48f, -26f, 6f, 6f, 149),
                    new MapObstacle(0f, 44f, 5f, 5f, 163),
                    new MapObstacle(-4f, -42f, 5f, 4f, 179),
                },
                OrePositions = new[]
                {
                    new float2(-62f, -44f),
                    new float2(62f, 44f),
                    new float2(-62f, 44f),
                    new float2(62f, -44f),
                    new float2(-70f, 0f),
                    new float2(70f, 0f),
                    new float2(0f, 78f),
                    new float2(0f, -78f),
                    new float2(-104f, 24f),
                    new float2(104f, -24f),
                },
                CratePositions = new[]
                {
                    new float2(0f, 0f),
                    new float2(-40f, 36f),
                    new float2(42f, -30f),
                    new float2(-60f, -48f),
                    new float2(68f, 20f),
                    new float2(18f, -62f),
                },
            },
            new MapDefinition
            {
                Id = "broken-ridge",
                Name = "Broken Ridge",
                Description = "Tight basin around a large polygonal central lake.",
                Seed = 7717,
                MaxHeight = 44f,
                NoiseScale = 2.8f,
                BasinRadius = 54f,
                BasinBlend = 24f,
                PlayerStart = new float2(-30f, -32f),
                AiStart = new float2(30f, 32f),
                WaterBodies = new[]
                {
                    WaterBody.Polygon(
                        "Ridge Lake",
                        0.55f,
                        new float2(-8f, -32f),
                        new float2(14f, -34f),
                        new float2(34f, -18f),
                        new float2(36f, 4f),
                        new float2(26f, 24f),
                        new float2(8f, 34f),
                        new float2(-12f, 30f),
                        new float2(-26f, 14f),
                        new float2(-30f, -6f),
                        new float2(-22f, -24f)),
                },
                Obstacles = new[]
                {
                    new MapObstacle(-48f, -18f, 6f, 9f, 211),
                    new MapObstacle(48f, 8f, 6f, 8f, 227),
                    new MapObstacle(-32f, 42f, 5f, 7f, 241),
                    new MapObstacle(38f, -40f, 5f, 8f, 263),
                    new MapObstacle(-58f, 25f, 4f, 6f, 277),
                    new MapObstacle(55f, -22f, 4f, 7f, 293),
                },
                OrePositions = new[]
                {
                    new float2(-56f, -48f),
                    new float2(56f, 48f),
                    new float2(-58f, 46f),
                    new float2(58f, -46f),
                    new float2(-72f, 0f),
                    new float2(72f, 0f),
                    new float2(0f, 72f),
                    new float2(0f, -74f),
                    new float2(-104f, 28f),
                    new float2(104f, -28f),
                },
                CratePositions = new[]
                {
                    new float2(50f, 0f),
                    new float2(-38f, 32f),
                    new float2(40f, -28f),
                    new float2(-54f, -42f),
                    new float2(58f, 22f),
                    new float2(16f, -56f),
                },
            },
            new MapDefinition
            {
                Id = "twin-shallows",
                Name = "Twin Shallows",
                Description = "Two separate lakes on opposite flanks, each with its own dock.",
                Seed = 5153,
                MaxHeight = 22f,
                NoiseScale = 4.2f,
                BasinRadius = 50f,
                BasinBlend = 18f,
                PlayerStart = new float2(-34f, -30f),
                AiStart = new float2(34f, 30f),
                WaterBodies = new[]
                {
                    WaterBody.Circle("South-West Shallows", -78f, -44f, 24f, 0.55f),
                    WaterBody.Circle("North-East Shallows", 78f, 44f, 24f, 0.55f),
                },
                Obstacles = new[]
                {
                    new MapObstacle(0f, 0f, 7f, 8f, 331),
                    new MapObstacle(-14f, 22f, 6f, 7f, 347),
                    new MapObstacle(18f, -22f, 6f, 7f, 359),
                    new MapObstacle(-50f, 38f, 5f, 6f, 373),
                    new MapObstacle(50f, -38f, 5f, 6f, 389),
                },
                OrePositions = new[]
                {
                    new float2(-56f, 10f),
                    new float2(56f, -10f),
                    new float2(-20f, -70f),
                    new float2(20f, 70f),
                    new float2(-110f, 20f),
                    new float2(110f, -20f),
                    new float2(-30f, 60f),
                    new float2(30f, -60f),
                    new float2(100f, 80f),
                    new float2(-100f, -80f),
                },
                CratePositions = new[]
                {
                    new float2(0f, 0f),
                    new float2(-46f, 44f),
                    new float2(52f, -34f),
                    new float2(-64f, 10f),
                    new float2(72f, 18f),
                    new float2(24f, -58f),
                },
            },
            new MapDefinition
            {
                Id = "flooded-delta",
                Name = "Flooded Delta",
                Description = "A huge irregular western delta with deep inlets and open water.",
                Seed = 8929,
                MaxHeight = 30f,
                NoiseScale = 3.4f,
                BasinRadius = 54f,
                BasinBlend = 20f,
                PlayerStart = new float2(-30f, -32f),
                AiStart = new float2(30f, 32f),
                WaterBodies = new[]
                {
                    WaterBody.Polygon(
                        "Delta",
                        0.55f,
                        new float2(-125f, -30f),
                        new float2(-95f, -48f),
                        new float2(-55f, -44f),
                        new float2(-38f, -20f),
                        new float2(-44f, 10f),
                        new float2(-28f, 32f),
                        new float2(-52f, 52f),
                        new float2(-92f, 48f),
                        new float2(-118f, 24f),
                        new float2(-132f, 0f)),
                },
                Obstacles = new[]
                {
                    new MapObstacle(0f, 0f, 7f, 9f, 401),
                    new MapObstacle(20f, -26f, 6f, 8f, 419),
                    new MapObstacle(-20f, 50f, 6f, 9f, 431),
                    new MapObstacle(52f, 26f, 5f, 7f, 443),
                    new MapObstacle(-58f, 82f, 5f, 7f, 457),
                },
                OrePositions = new[]
                {
                    new float2(40f, -60f),
                    new float2(40f, 60f),
                    new float2(20f, 0f),
                    new float2(70f, -20f),
                    new float2(70f, 30f),
                    new float2(100f, 60f),
                    new float2(100f, -60f),
                    new float2(0f, -80f),
                    new float2(0f, 80f),
                    new float2(-145f, 50f),
                },
                CratePositions = new[]
                {
                    new float2(0f, 0f),
                    new float2(20f, 50f),
                    new float2(40f, -34f),
                    new float2(-20f, -62f),
                    new float2(60f, 18f),
                    new float2(24f, -58f),
                },
            },
        };

        private static readonly string[] FamilyNames =
        {
            "Ash Basin",
            "Rusted Flats",
            "Broken Ridge",
            "Twin Shallows",
            "Flooded Delta",
        };

        private static readonly string[] FamilyDescriptions =
        {
            "Industrial basin with ruins and a small-to-medium lake.",
            "Open dry ground with long sight lines and little or no water.",
            "High ridges and a central polygonal lake with short lanes.",
            "Two separate lakes with split naval theaters.",
            "Large irregular delta with deep inlets and heavy naval play.",
        };

        public static readonly MapDefinition[] Maps = BuildAll();

        private static MapDefinition[] BuildAll()
        {
            var maps = new List<MapDefinition>(50);
            maps.AddRange(Authored);
            AddTier(maps, MapSizeTier.Small, 3, 5);
            AddTier(maps, MapSizeTier.Medium, 12, 8);
            AddTier(maps, MapSizeTier.Large, 12, 20);
            AddTier(maps, MapSizeTier.Huge, 18, 32);
            return maps.ToArray();
        }

        private static void AddTier(
            List<MapDefinition> maps,
            MapSizeTier tier,
            int count,
            int startIndex)
        {
            for (var i = 0; i < count; i++)
            {
                var index = startIndex + i;
                var family = index % FamilyNames.Length;
                maps.Add(CreateGeneratedMap(family, tier, index));
            }
        }

        private static MapDefinition CreateGeneratedMap(int family, MapSizeTier tier, int index)
        {
            var worldSize = tier switch
            {
                MapSizeTier.Small => 220f,
                MapSizeTier.Medium => 320f,
                MapSizeTier.Large => 420f,
                _ => 520f,
            };

            var maxPlayers = tier switch
            {
                MapSizeTier.Small => 2,
                MapSizeTier.Medium => 4,
                MapSizeTier.Large => 6,
                _ => 8,
            };

            var minPlayers = tier switch
            {
                MapSizeTier.Small => 2,
                MapSizeTier.Medium => 3,
                MapSizeTier.Large => 5,
                _ => 7,
            };

            var seed = 1200 + index * 137 + family * 29;
            var spawnRing = worldSize * 0.37f;
            var basinRadius = worldSize * 0.42f;
            var spawns = new MapSpawn[maxPlayers];
            for (var i = 0; i < maxPlayers; i++)
            {
                var angle = -math.PI * 0.5f + i * math.PI * 2f / maxPlayers;
                spawns[i] = new MapSpawn(
                    math.cos(angle) * spawnRing,
                    math.sin(angle) * spawnRing);
            }

            var water = CreateWater(family, index, worldSize);
            var obstacles = CreateObstacles(seed + 1, worldSize, spawns, water);
            var ore = CreatePoints(seed + 2, 10 + (index % 3), worldSize, spawns, water, 0.30f, 0.40f);
            var crates = CreatePoints(seed + 3, 5 + (index % 2), worldSize, spawns, water, 0.16f, 0.34f);

            return new MapDefinition
            {
                Id = $"{FamilyNames[family].ToLowerInvariant().Replace(' ', '-')}-{index + 1:00}",
                Name = $"{FamilyNames[family]} {index + 1:00}",
                Description = FamilyDescriptions[family],
                SizeTier = tier,
                WorldSize = worldSize,
                MinPlayers = minPlayers,
                MaxPlayers = maxPlayers,
                Seed = seed,
                MaxHeight = 16f + family * 5f + (int)tier * 4f,
                NoiseScale = 4.8f - family * 0.35f,
                BasinRadius = basinRadius,
                BasinBlend = worldSize * 0.09f,
                PlayerStart = spawns[0].Position,
                AiStart = spawns[1].Position,
                Spawns = spawns,
                WaterBodies = water,
                Obstacles = obstacles,
                OrePositions = ore,
                CratePositions = crates,
            };
        }

        private static WaterBody[] CreateWater(int family, int index, float worldSize)
        {
            switch (family)
            {
                case 1:
                    if (index % 10 < 6)
                    {
                        return System.Array.Empty<WaterBody>();
                    }

                    return new[]
                    {
                        WaterBody.Circle(
                            "Salt Pan",
                            worldSize * 0.16f,
                            -worldSize * 0.10f,
                            worldSize * 0.075f,
                            0.55f),
                    };

                case 2:
                    return new[]
                    {
                        WaterBody.Polygon(
                            "Ridge Lake",
                            0.55f,
                            PolygonPoints(
                                0f,
                                0f,
                                worldSize * (0.125f + (index % 3) * 0.008f),
                                10,
                                index * 37)),
                    };

                case 3:
                    return new[]
                    {
                        WaterBody.Circle(
                            "South Lake",
                            -worldSize * 0.20f,
                            -worldSize * 0.15f,
                            worldSize * (0.070f + (index % 2) * 0.008f),
                            0.55f),
                        WaterBody.Circle(
                            "North Lake",
                            worldSize * 0.20f,
                            worldSize * 0.15f,
                            worldSize * (0.070f + ((index + 1) % 2) * 0.008f),
                            0.55f),
                    };

                case 4:
                    return new[]
                    {
                        WaterBody.Polygon(
                            "Delta",
                            0.55f,
                            PolygonPoints(
                                -worldSize * 0.08f,
                                0f,
                                worldSize * (0.19f + (index % 3) * 0.012f),
                                12,
                                index * 53)),
                    };

                default:
                    return new[]
                    {
                        WaterBody.Circle(
                            "West Lake",
                            -worldSize * 0.20f,
                            0f,
                            worldSize * (0.095f + (index % 3) * 0.008f),
                            0.55f),
                    };
            }
        }

        private static float2[] PolygonPoints(
            float centreX,
            float centreZ,
            float radius,
            int count,
            int seed)
        {
            var random = new System.Random(seed);
            var points = new float2[count];
            for (var i = 0; i < count; i++)
            {
                var angle = i * math.PI * 2f / count;
                var jitter = 0.86f + (float)random.NextDouble() * 0.28f;
                points[i] = new float2(
                    centreX + math.cos(angle) * radius * jitter,
                    centreZ + math.sin(angle) * radius * jitter);
            }

            return points;
        }

        private static MapObstacle[] CreateObstacles(
            int seed,
            float worldSize,
            MapSpawn[] spawns,
            WaterBody[] water)
        {
            var random = new System.Random(seed);
            var obstacles = new List<MapObstacle>();
            var attempts = 0;
            while (obstacles.Count < 7 && attempts++ < 80)
            {
                var angle = (float)random.NextDouble() * math.PI * 2f;
                var radius = worldSize * (0.16f + (float)random.NextDouble() * 0.24f);
                var point = new float2(math.cos(angle) * radius, math.sin(angle) * radius);
                if (ContainsWater(water, point) || TooCloseToSpawn(spawns, point, worldSize * 0.12f))
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

            return obstacles.ToArray();
        }

        private static float2[] CreatePoints(
            int seed,
            int count,
            float worldSize,
            MapSpawn[] spawns,
            WaterBody[] water,
            float minRadius,
            float maxRadius)
        {
            var random = new System.Random(seed);
            var points = new List<float2>();
            var attempts = 0;
            while (points.Count < count && attempts++ < 160)
            {
                var angle = (float)random.NextDouble() * math.PI * 2f;
                var radius = worldSize *
                             (minRadius + (float)random.NextDouble() * (maxRadius - minRadius));
                var point = new float2(math.cos(angle) * radius, math.sin(angle) * radius);
                if (ContainsWater(water, point) || TooCloseToSpawn(spawns, point, worldSize * 0.09f))
                {
                    continue;
                }

                points.Add(point);
            }

            return points.ToArray();
        }

        private static bool TooCloseToSpawn(MapSpawn[] spawns, float2 point, float distance)
        {
            var distanceSq = distance * distance;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (math.distancesq(spawns[i].Position, point) < distanceSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWater(WaterBody[] bodies, float2 point)
        {
            if (bodies == null)
            {
                return false;
            }

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body.Kind == WaterShapeKind.Circle)
                {
                    if (math.distancesq(point, body.Centre) < body.Radius * body.Radius)
                    {
                        return true;
                    }

                    continue;
                }

                if (PointInPolygon(body.Points, point))
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

        public static int Count => Maps.Length;

        public static MapDefinition Get(int index)
        {
            if (Maps.Length == 0)
            {
                return null;
            }

            return Maps[Clamp(index)];
        }

        public static int FindIndex(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            for (var i = 0; i < Maps.Length; i++)
            {
                if (string.Equals(Maps[i].Id, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int Clamp(int index)
        {
            return index < 0 ? 0 : index >= Maps.Length ? Maps.Length - 1 : index;
        }
    }

    /// <summary>Selected map for the next scene load. Survives scene restarts.</summary>
    public static class MapRuntime
    {
        private static int _selectedIndex;

        public static int SelectedIndex => _selectedIndex;

        public static MapDefinition Current => MapCatalog.Get(_selectedIndex);

        public static void Select(int index)
        {
            _selectedIndex = MapCatalog.Clamp(index);
        }

        public static void Reset()
        {
            _selectedIndex = 0;
        }
    }
}
