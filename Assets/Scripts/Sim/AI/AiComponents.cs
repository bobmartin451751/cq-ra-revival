using Unity.Entities;

namespace CQRa.Sim.AI
{
    public enum AiDifficulty : byte
    {
        Recruit = 0,
        Veteran = 1,
        Hardened = 2,
        Brutal = 3,
    }

    /// <summary>
    /// One AI-controlled team. The commander is the only AI-specific state; all actions
    /// go through the same build/production/combat commands the player uses.
    /// </summary>
    public struct AiCommander : IComponentData
    {
        public int Team;
        public AiDifficulty Difficulty;
        public float ThinkTimer;
        public byte Attacking;
    }

    /// <summary>
    /// Difficulty scales decisions only. There are no credit, vision or stat bonuses;
    /// higher tiers think faster, attack with smaller armies and build more production.
    /// </summary>
    public readonly struct AiProfile
    {
        public readonly float ThinkInterval;
        public readonly int AttackArmySize;
        public readonly int MaxBarracks;
        public readonly int MaxFactories;
        public readonly int MaxRefineries;
        public readonly int MaxPowerPlants;
        public readonly int HarvesterTarget;
        public readonly float TankRatio;
        public readonly int ArmyCap;

        private AiProfile(
            float thinkInterval,
            int attackArmySize,
            int maxBarracks,
            int maxFactories,
            int maxRefineries,
            int maxPowerPlants,
            int harvesterTarget,
            float tankRatio,
            int armyCap)
        {
            ThinkInterval = thinkInterval;
            AttackArmySize = attackArmySize;
            MaxBarracks = maxBarracks;
            MaxFactories = maxFactories;
            MaxRefineries = maxRefineries;
            MaxPowerPlants = maxPowerPlants;
            HarvesterTarget = harvesterTarget;
            TankRatio = tankRatio;
            ArmyCap = armyCap;
        }

        public static AiProfile For(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Brutal:
                    return new AiProfile(0.75f, 4, 2, 2, 3, 3, 4, 0.70f, 18);
                case AiDifficulty.Hardened:
                    return new AiProfile(1.0f, 5, 2, 2, 2, 2, 3, 0.55f, 14);
                case AiDifficulty.Veteran:
                    return new AiProfile(1.5f, 6, 1, 1, 2, 2, 3, 0.40f, 11);
                default:
                    return new AiProfile(2.0f, 8, 1, 1, 1, 1, 2, 0.25f, 8);
            }
        }
    }
}
