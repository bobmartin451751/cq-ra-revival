using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    public enum UnitKind : byte
    {
        Infantry = 0,
        Tank = 1,
        Harvester = 2,
        Mcv = 3,
        Gunship = 4,
        Engineer = 5,
        Thumper = 6,
        Mantis = 7,
        Skyfall = 8,
        Rig = 9,
        Sower = 10,
        PatrolBoat = 11,
        Battleship = 12,
        AshfallFighter = 13,
        AshfallBomber = 14,
        AshfallTransport = 15,
        VerdantFighter = 16,
        VerdantBomber = 17,
        VerdantAttackHeli = 18,
        VerdantTransport = 19,
        MeridianBomber = 20,
        MeridianAttackHeli = 21,
        MeridianTransport = 22,
    }

    /// <summary>Marks an entity as a mobile unit.</summary>
    public struct UnitTag : IComponentData
    {
    }

    /// <summary>Air units ignore nav blockers and fly at a fixed altitude.</summary>
    public struct AirUnit : IComponentData
    {
        public float Altitude;
    }

    /// <summary>Deployable artillery state (Thumper).</summary>
    public struct DeployState : IComponentData
    {
        public byte Deployed;
        public float NormalRange;
        public float DeployedRange;
        public float NormalCooldown;
        public float DeployedCooldown;
    }

    /// <summary>Regenerating energy shield (Meridian Aegis infantry).</summary>
    public struct Shield : IComponentData
    {
        public float Max;
        public float Current;
        public float RegenDelay;
        public float TimeSinceHit;
    }

    /// <summary>Cloak state for the Verdant Mantis.</summary>
    public struct Cloak : IComponentData
    {
        public byte Cloaked;
        public float Delay;
        public float Timer;
    }

    /// <summary>Kill-based veterancy rank (0..3).</summary>
    public struct Veterancy : IComponentData
    {
        public int Kills;
        public int Rank;
    }

    /// <summary>Heals nearby friendly mechanical units (Ashfall Rig).</summary>
    public struct RepairAura : IComponentData
    {
        public float Radius;
        public float HealPerSecond;
    }

    /// <summary>Mine-laying cooldown (Verdant Sower).</summary>
    public struct MineLayer : IComponentData
    {
        public float Cooldown;
        public float CooldownRemaining;
    }

    /// <summary>A deployed mine. Team 0/1, trigger radius and blast damage.</summary>
    public struct Mine : IComponentData
    {
        public int Team;
        public float TriggerRadius;
        public float BlastRadius;
        public float Damage;
    }

    /// <summary>Naval unit constrained to the water body it launched into.</summary>
    public struct NavalUnit : IComponentData
    {
        public int BodyIndex;
        public float Level;
    }

    public static class NavalRules
    {
        public static bool IsNaval(UnitKind kind)
        {
            return kind == UnitKind.PatrolBoat || kind == UnitKind.Battleship;
        }
    }

    [System.Flags]
    public enum TargetClass : byte
    {
        None = 0,
        Ground = 1,
        Air = 2,
        Both = Ground | Air,
    }

    public struct WeaponTargeting : IComponentData
    {
        public TargetClass Mask;
    }

    public struct AircraftAmmo : IComponentData
    {
        public int Max;
        public int Current;
    }

    public struct AircraftHome : IComponentData
    {
        public Entity Airfield;
        public byte Returning;
        public byte Landed;
        public float RearmTimer;
    }

    public struct TransportCargo : IComponentData
    {
        public int InfantryCapacity;
        public int TankCapacity;
        public int InfantryWithTank;
        public byte IsTransport;
    }

    public struct CargoUnit : IBufferElementData
    {
        public UnitKind Kind;
        public float Health;
    }

    public static class AirCatalog
    {
        public static bool IsAircraft(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Gunship:
                case UnitKind.Skyfall:
                case UnitKind.AshfallFighter:
                case UnitKind.AshfallBomber:
                case UnitKind.AshfallTransport:
                case UnitKind.VerdantFighter:
                case UnitKind.VerdantBomber:
                case UnitKind.VerdantAttackHeli:
                case UnitKind.VerdantTransport:
                case UnitKind.MeridianBomber:
                case UnitKind.MeridianAttackHeli:
                case UnitKind.MeridianTransport:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsPlane(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Skyfall:
                case UnitKind.AshfallFighter:
                case UnitKind.AshfallBomber:
                case UnitKind.VerdantFighter:
                case UnitKind.VerdantBomber:
                case UnitKind.MeridianBomber:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsTransport(UnitKind kind)
        {
            return kind == UnitKind.AshfallTransport ||
                   kind == UnitKind.VerdantTransport ||
                   kind == UnitKind.MeridianTransport;
        }

        public static bool IsAttackHelicopter(UnitKind kind)
        {
            return kind == UnitKind.Gunship ||
                   kind == UnitKind.VerdantAttackHeli ||
                   kind == UnitKind.MeridianAttackHeli;
        }

        public static bool UsesAmmo(UnitKind kind)
        {
            return kind == UnitKind.Skyfall ||
                   kind == UnitKind.AshfallFighter ||
                   kind == UnitKind.AshfallBomber ||
                   kind == UnitKind.VerdantFighter ||
                   kind == UnitKind.VerdantBomber ||
                   kind == UnitKind.MeridianBomber;
        }

        public static int AmmoFor(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.AshfallBomber:
                case UnitKind.VerdantBomber:
                    return 3;
                case UnitKind.MeridianBomber:
                    return 2;
                default:
                    return 6;
            }
        }

        public static bool TryGetCargoRules(
            UnitKind kind,
            out int infantryCapacity,
            out int tankCapacity,
            out int infantryWithTank,
            out bool isTransport)
        {
            switch (kind)
            {
                case UnitKind.Gunship:
                case UnitKind.VerdantAttackHeli:
                case UnitKind.MeridianAttackHeli:
                    infantryCapacity = 5;
                    tankCapacity = 0;
                    infantryWithTank = 0;
                    isTransport = true;
                    return true;

                case UnitKind.AshfallTransport:
                case UnitKind.VerdantTransport:
                case UnitKind.MeridianTransport:
                    infantryCapacity = 20;
                    tankCapacity = 1;
                    infantryWithTank = 5;
                    isTransport = true;
                    return true;

                default:
                    infantryCapacity = 0;
                    tankCapacity = 0;
                    infantryWithTank = 0;
                    isTransport = false;
                    return false;
            }
        }

        public static TargetClass DefaultTargeting(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Skyfall:
                case UnitKind.AshfallFighter:
                case UnitKind.VerdantFighter:
                    return TargetClass.Air;
                case UnitKind.AshfallBomber:
                case UnitKind.VerdantBomber:
                case UnitKind.MeridianBomber:
                    return TargetClass.Ground;
                case UnitKind.Gunship:
                case UnitKind.VerdantAttackHeli:
                case UnitKind.MeridianAttackHeli:
                    return TargetClass.Both;
                case UnitKind.AshfallTransport:
                case UnitKind.VerdantTransport:
                case UnitKind.MeridianTransport:
                    return TargetClass.None;
                default:
                    return TargetClass.Ground;
            }
        }
    }

    public static class VeterancyMath
    {
        public static int RankForKills(int kills)
        {
            if (kills >= 9) return 3;
            if (kills >= 5) return 2;
            if (kills >= 2) return 1;
            return 0;
        }
    }

    /// <summary>Static gameplay identity. Grows as later phases add movement and combat.</summary>
    public struct UnitInfo : IComponentData
    {
        public int Team;
        public UnitKind Kind;
        public float Radius;
        public float MaxSpeed;
    }

    /// <summary>Enableable selection flag; toggled by the selection controller.</summary>
    public struct Selected : IComponentData, IEnableableComponent
    {
    }

    /// <summary>Remembers the last applied highlight state so material swaps happen once.</summary>
    public struct HighlightState : IComponentData
    {
        public byte IsSelected;
    }

    /// <summary>Layout of the shared material array used by every unit.</summary>
    public static class UnitMaterials
    {
        public const int TeamCount = 8;
        public const int KindCount = 23;
        public const int HighlightIndex = TeamCount * KindCount;
        public const int Count = HighlightIndex + 1;

        public static int BaseIndex(int team, UnitKind kind)
        {
            return team * KindCount + (int)kind;
        }
    }
}
