using UnityEngine;

namespace CQRa.Sim.Units
{
    /// <summary>Eight readable team accents. Faction colour still drives the silhouette tint.</summary>
    public static class TeamPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.78f, 0.22f, 0.14f), // red
            new Color(0.20f, 0.42f, 0.72f), // blue
            new Color(0.28f, 0.58f, 0.22f), // green
            new Color(0.82f, 0.66f, 0.16f), // amber
            new Color(0.55f, 0.28f, 0.68f), // violet
            new Color(0.86f, 0.46f, 0.16f), // orange
            new Color(0.18f, 0.64f, 0.64f), // teal
            new Color(0.82f, 0.36f, 0.58f), // magenta
        };

        public static Color For(int team)
        {
            if (team < 0 || team >= Colors.Length)
            {
                return Colors[0];
            }

            return Colors[team];
        }
    }
}
