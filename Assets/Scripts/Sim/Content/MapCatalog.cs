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

    /// <summary>
    /// Hand-authored skirmish map. Terrain shape, scenery, ore, crates and both
    /// starting positions are data; the lake stays at the same western site so the
    /// naval rules remain consistent across maps.
    /// </summary>
    public sealed class MapDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public int Seed;
        public float MaxHeight;
        public float NoiseScale;
        public float BasinRadius;
        public float BasinBlend;
        public float2 PlayerStart;
        public float2 AiStart;
        public MapObstacle[] Obstacles;
        public float2[] OrePositions;
        public float2[] CratePositions;
    }

    /// <summary>
    /// Three deterministic skirmish maps. Map 0 is the original Ash Basin layout so
    /// existing saves, screenshots and tests keep their familiar ground.
    /// </summary>
    public static class MapCatalog
    {
        public static readonly MapDefinition[] Maps =
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
                Description = "Wide low ground with long sight lines and a contested centre.",
                Seed = 4241,
                MaxHeight = 14f,
                NoiseScale = 5.6f,
                BasinRadius = 52f,
                BasinBlend = 18f,
                PlayerStart = new float2(-34f, -30f),
                AiStart = new float2(34f, 30f),
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
                Description = "Tight basin, high ridges and short, brutal engagement lanes.",
                Seed = 7717,
                MaxHeight = 44f,
                NoiseScale = 2.8f,
                BasinRadius = 44f,
                BasinBlend = 20f,
                PlayerStart = new float2(-30f, -32f),
                AiStart = new float2(30f, 32f),
                Obstacles = new[]
                {
                    new MapObstacle(-10f, 0f, 10f, 15f, 211),
                    new MapObstacle(14f, 6f, 8f, 13f, 227),
                    new MapObstacle(-30f, 22f, 6f, 9f, 241),
                    new MapObstacle(34f, -18f, 6f, 8f, 263),
                    new MapObstacle(-44f, -16f, 5f, 7f, 277),
                    new MapObstacle(6f, 38f, 5f, 8f, 293),
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
                    new float2(0f, 0f),
                    new float2(-38f, 32f),
                    new float2(40f, -28f),
                    new float2(-54f, -42f),
                    new float2(58f, 22f),
                    new float2(16f, -56f),
                },
            },
        };

        public static int Count => Maps.Length;

        public static MapDefinition Get(int index)
        {
            if (Maps.Length == 0)
            {
                return null;
            }

            return Maps[Clamp(index)];
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
