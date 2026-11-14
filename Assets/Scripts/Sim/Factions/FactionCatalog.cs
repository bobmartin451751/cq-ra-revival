namespace CQRa.Sim.Factions
{
    public enum FactionId : byte
    {
        Ashfall = 0,
        Verdant = 1,
        Meridian = 2,
    }

    public readonly struct FactionModifiers
    {
        public readonly float Cost;
        public readonly float Health;
        public readonly float Damage;
        public readonly float Speed;
        public readonly float Vision;

        public FactionModifiers(float cost, float health, float damage, float speed, float vision)
        {
            Cost = cost;
            Health = health;
            Damage = damage;
            Speed = speed;
            Vision = vision;
        }
    }

    /// <summary>
    /// Three original factions. Modifiers apply equally to any team using the faction, so
    /// the AI never receives a hidden advantage. Missions assign a faction to each side.
    /// </summary>
    public static class FactionCatalog
    {
        public static readonly FactionId[] All =
        {
            FactionId.Ashfall,
            FactionId.Verdant,
            FactionId.Meridian,
        };

        public static string Name(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant: return "Verdant Combine";
                case FactionId.Meridian: return "Meridian Syndicate";
                default: return "Ashfall Compact";
            }
        }

        public static string Description(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant:
                    return "Fast, cheap, brittle. Swarm with light vehicles and infantry.";
                case FactionId.Meridian:
                    return "Expensive, long-ranged, high vision. Fewer, harder-hitting units.";
                default:
                    return "Balanced industrial baseline. No strengths, no weaknesses.";
            }
        }

        public static FactionModifiers Modifiers(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant:
                    return new FactionModifiers(0.90f, 0.92f, 0.95f, 1.10f, 0.98f);
                case FactionId.Meridian:
                    return new FactionModifiers(1.12f, 1.02f, 1.12f, 0.95f, 1.15f);
                default:
                    return new FactionModifiers(1f, 1f, 1f, 1f, 1f);
            }
        }

        public static FactionId DefaultOpponent(FactionId player)
        {
            switch (player)
            {
                case FactionId.Ashfall: return FactionId.Verdant;
                case FactionId.Verdant: return FactionId.Meridian;
                default: return FactionId.Ashfall;
            }
        }

        public static FactionId Parse(string value, FactionId fallback = FactionId.Ashfall)
        {
            return System.Enum.TryParse(value, true, out FactionId faction) ? faction : fallback;
        }
    }

    /// <summary>Per-match team-to-faction mapping.</summary>
    public static class FactionRuntime
    {
        private static readonly FactionId[] Teams = new FactionId[8];

        static FactionRuntime()
        {
            Reset();
        }

        public static FactionId Player => Teams[0];
        public static FactionId Ai => Teams[1];

        public static void Set(FactionId player, FactionId ai)
        {
            Teams[0] = player;
            for (var i = 1; i < Teams.Length; i++)
            {
                Teams[i] = ai;
            }
        }

        public static void SetAll(params FactionId[] factions)
        {
            for (var i = 0; i < Teams.Length; i++)
            {
                Teams[i] = i < factions.Length ? factions[i] : FactionId.Ashfall;
            }
        }

        public static void SetTeam(int team, FactionId faction)
        {
            if (team >= 0 && team < Teams.Length)
            {
                Teams[team] = faction;
            }
        }

        public static void Reset()
        {
            for (var i = 0; i < Teams.Length; i++)
            {
                Teams[i] = i == 0 ? FactionId.Ashfall : FactionId.Ashfall;
            }
        }

        public static FactionId ForTeam(int team)
        {
            if (team < 0 || team >= Teams.Length)
            {
                return FactionId.Ashfall;
            }

            return Teams[team];
        }

        public static FactionModifiers StatsForTeam(int team)
        {
            return FactionCatalog.Modifiers(ForTeam(team));
        }

        public static int CostFor(int team, int baseCost)
        {
            return (int)System.Math.Round(baseCost * StatsForTeam(team).Cost);
        }
    }
}
