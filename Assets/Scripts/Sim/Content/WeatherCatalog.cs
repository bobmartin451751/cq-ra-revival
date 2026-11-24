namespace CQRa.Sim.Content
{
    /// <summary>Visual-only weather. Weather never changes vision, movement or damage.</summary>
    public enum WeatherKind : byte
    {
        Clear = 0,
        Rain = 1,
        Dust = 2,
        Snow = 3,
    }

    /// <summary>
    /// Stable per-map weather selection. Uses its own FNV hash so results are identical
    /// across machines, sessions and builds (string.GetHashCode is not stable).
    /// </summary>
    public static class WeatherCatalog
    {
        public static WeatherKind ForMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
            {
                return WeatherKind.Clear;
            }

            var hash = 2166136261u;
            for (var i = 0; i < mapId.Length; i++)
            {
                hash ^= mapId[i];
                hash *= 16777619u;
            }

            // Roughly one quarter of maps are clear, the rest get one of three effects.
            switch (hash % 4u)
            {
                case 1: return WeatherKind.Rain;
                case 2: return WeatherKind.Dust;
                case 3: return WeatherKind.Snow;
                default: return WeatherKind.Clear;
            }
        }

        public static string DisplayName(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Rain: return "Rain";
                case WeatherKind.Dust: return "Dust Storm";
                case WeatherKind.Snow: return "Snow";
                default: return "Clear";
            }
        }

        public static WeatherKind ForCurrentMap()
        {
            return ForMap(MapRuntime.Current?.Id);
        }
    }
}
