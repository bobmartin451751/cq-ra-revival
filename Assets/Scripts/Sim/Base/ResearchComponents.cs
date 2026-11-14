using Unity.Entities;

namespace CQRa.Sim.Base
{
    public enum UpgradeKind : byte
    {
        Weapons = 0,
        Armor = 1,
        Logistics = 2,
    }

    public enum ResearchFailure : byte
    {
        None = 0,
        AlreadyResearched = 1,
        AlreadyResearching = 2,
        NotEnoughCredits = 3,
        NoEconomy = 4,
    }

    public readonly struct ResearchSpec
    {
        public readonly int Cost;
        public readonly float Seconds;
        public readonly string Label;
        public readonly string Description;

        public ResearchSpec(int cost, float seconds, string label, string description)
        {
            Cost = cost;
            Seconds = seconds;
            Label = label;
            Description = description;
        }
    }

    public static class ResearchCatalog
    {
        public const int Count = 3;

        public static ResearchSpec Get(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Armor:
                    return new ResearchSpec(650, 24f, "Armour Plating", "+10% health");
                case UpgradeKind.Logistics:
                    return new ResearchSpec(600, 20f, "Logistics", "+10% move speed");
                default:
                    return new ResearchSpec(700, 26f, "Weapon Tuning", "+10% damage");
            }
        }
    }

    /// <summary>Per-team upgrade completion masks and one active research job.</summary>
    public struct ResearchState : IComponentData
    {
        public const int TeamCount = 8;

        public int Team0Mask;
        public int Team1Mask;
        public int Team2Mask;
        public int Team3Mask;
        public int Team4Mask;
        public int Team5Mask;
        public int Team6Mask;
        public int Team7Mask;
        public float Team0Timer;
        public float Team1Timer;
        public float Team2Timer;
        public float Team3Timer;
        public float Team4Timer;
        public float Team5Timer;
        public float Team6Timer;
        public float Team7Timer;
        public byte Team0Active;
        public byte Team1Active;
        public byte Team2Active;
        public byte Team3Active;
        public byte Team4Active;
        public byte Team5Active;
        public byte Team6Active;
        public byte Team7Active;
        public UpgradeKind Team0Kind;
        public UpgradeKind Team1Kind;
        public UpgradeKind Team2Kind;
        public UpgradeKind Team3Kind;
        public UpgradeKind Team4Kind;
        public UpgradeKind Team5Kind;
        public UpgradeKind Team6Kind;
        public UpgradeKind Team7Kind;

        public int MaskFor(int team)
        {
            switch (team)
            {
                case 0: return Team0Mask;
                case 1: return Team1Mask;
                case 2: return Team2Mask;
                case 3: return Team3Mask;
                case 4: return Team4Mask;
                case 5: return Team5Mask;
                case 6: return Team6Mask;
                case 7: return Team7Mask;
                default: return 0;
            }
        }

        public void SetMask(int team, int mask)
        {
            switch (team)
            {
                case 0: Team0Mask = mask; break;
                case 1: Team1Mask = mask; break;
                case 2: Team2Mask = mask; break;
                case 3: Team3Mask = mask; break;
                case 4: Team4Mask = mask; break;
                case 5: Team5Mask = mask; break;
                case 6: Team6Mask = mask; break;
                case 7: Team7Mask = mask; break;
            }
        }

        public bool Has(int team, UpgradeKind kind)
        {
            return (MaskFor(team) & (1 << (int)kind)) != 0;
        }

        public void Mark(int team, UpgradeKind kind)
        {
            SetMask(team, MaskFor(team) | (1 << (int)kind));
        }

        public float TimerFor(int team)
        {
            switch (team)
            {
                case 0: return Team0Timer;
                case 1: return Team1Timer;
                case 2: return Team2Timer;
                case 3: return Team3Timer;
                case 4: return Team4Timer;
                case 5: return Team5Timer;
                case 6: return Team6Timer;
                case 7: return Team7Timer;
                default: return 0f;
            }
        }

        public void SetTimer(int team, float value)
        {
            switch (team)
            {
                case 0: Team0Timer = value; break;
                case 1: Team1Timer = value; break;
                case 2: Team2Timer = value; break;
                case 3: Team3Timer = value; break;
                case 4: Team4Timer = value; break;
                case 5: Team5Timer = value; break;
                case 6: Team6Timer = value; break;
                case 7: Team7Timer = value; break;
            }
        }

        public bool IsActive(int team)
        {
            switch (team)
            {
                case 0: return Team0Active != 0;
                case 1: return Team1Active != 0;
                case 2: return Team2Active != 0;
                case 3: return Team3Active != 0;
                case 4: return Team4Active != 0;
                case 5: return Team5Active != 0;
                case 6: return Team6Active != 0;
                case 7: return Team7Active != 0;
                default: return false;
            }
        }

        public void SetActive(int team, bool active)
        {
            var value = (byte)(active ? 1 : 0);
            switch (team)
            {
                case 0: Team0Active = value; break;
                case 1: Team1Active = value; break;
                case 2: Team2Active = value; break;
                case 3: Team3Active = value; break;
                case 4: Team4Active = value; break;
                case 5: Team5Active = value; break;
                case 6: Team6Active = value; break;
                case 7: Team7Active = value; break;
            }
        }

        public UpgradeKind KindFor(int team)
        {
            switch (team)
            {
                case 0: return Team0Kind;
                case 1: return Team1Kind;
                case 2: return Team2Kind;
                case 3: return Team3Kind;
                case 4: return Team4Kind;
                case 5: return Team5Kind;
                case 6: return Team6Kind;
                case 7: return Team7Kind;
                default: return UpgradeKind.Weapons;
            }
        }

        public void SetKind(int team, UpgradeKind kind)
        {
            switch (team)
            {
                case 0: Team0Kind = kind; break;
                case 1: Team1Kind = kind; break;
                case 2: Team2Kind = kind; break;
                case 3: Team3Kind = kind; break;
                case 4: Team4Kind = kind; break;
                case 5: Team5Kind = kind; break;
                case 6: Team6Kind = kind; break;
                case 7: Team7Kind = kind; break;
            }
        }
    }
}
