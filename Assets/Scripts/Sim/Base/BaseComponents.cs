using CQRa.Sim.Content;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Base
{
    public enum BuildingKind : byte
    {
        ConstructionYard = 0,
        PowerPlant = 1,
        Refinery = 2,
        Barracks = 3,
        WarFactory = 4,
        GunTower = 5,
        Wall = 6,
        NavalYard = 7,
    }

    /// <summary>Marks a static player building.</summary>
    public struct BuildingTag : IComponentData
    {
    }

    /// <summary>Gameplay identity of a building. Buildings do not move and are not units.</summary>
    public struct BuildingInfo : IComponentData
    {
        public int Team;
        public BuildingKind Kind;
        public float Radius;
        public int PowerProduced;
        public int PowerUsed;
    }

    /// <summary>Marks the building that defines a team's build radius.</summary>
    public struct ConstructionYardTag : IComponentData
    {
    }

    /// <summary>Marks a drop-off point for harvesters.</summary>
    public struct RefineryTag : IComponentData
    {
    }

    /// <summary>Toggles continuous repair on a damaged building.</summary>
    public struct RepairState : IComponentData
    {
        public byte Active;
    }

    /// <summary>Hitscan defensive weapon mounted on a building.</summary>
    public struct DefenseWeapon : IComponentData
    {
        public float Damage;
        public float Range;
        public float Cooldown;
        public float CooldownRemaining;
    }

    public static class DefenseCatalog
    {
        public const float GunTowerDamage = 18f;
        public const float GunTowerRange = 28f;
        public const float GunTowerCooldown = 0.7f;
    }

    /// <summary>Repair and sell tuning shared by the systems and HUD.</summary>
    public static class RepairRules
    {
        public const float HealPerSecond = 40f;
        public const float CreditsPerSecond = 30f;

        /// <summary>Selling refunds half the original cost; the yard is not sellable.</summary>
        public static int SellRefund(BuildingKind kind)
        {
            return BuildCatalog.Get(kind).Cost / 2;
        }

        public static bool CanSell(BuildingKind kind)
        {
            return kind != BuildingKind.ConstructionYard;
        }
    }

    /// <summary>A finite ore patch in the world.</summary>
    public struct OreNode : IComponentData
    {
        public int Amount;
        public float Radius;
    }

    /// <summary>Per-team credits and power, owned by one singleton entity.</summary>
    public struct EconomyState : IComponentData
    {
        public int Team0Credits;
        public int Team1Credits;
        public int Team0PowerMade;
        public int Team0PowerUsed;
        public int Team1PowerMade;
        public int Team1PowerUsed;
    }

    public enum HarvesterPhase : byte
    {
        Idle = 0,
        ToOre = 1,
        Harvesting = 2,
        ToRefinery = 3,
    }

    /// <summary>Autonomous ore loop state. A player move order temporarily overrides it.</summary>
    public struct HarvesterJob : IComponentData
    {
        public HarvesterPhase Phase;
        public Entity Ore;
        public float Cargo;

        /// <summary>1 while the current move order was issued by the harvester loop itself.</summary>
        public byte AutoOrder;
    }

    public readonly struct BuildingSpec
    {
        public readonly float Radius;
        public readonly float NavRadius;
        public readonly int Cost;
        public readonly int PowerProduced;
        public readonly int PowerUsed;

        public BuildingSpec(float radius, float navRadius, int cost, int powerProduced, int powerUsed)
        {
            Radius = radius;
            NavRadius = navRadius;
            Cost = cost;
            PowerProduced = powerProduced;
            PowerUsed = powerUsed;
        }
    }

    public static class BuildCatalog
    {
        public const float BuildRadius = 45f;
        public const int InitialCredits = 3000;
        public const int PlayerTeam = 0;

        public static readonly BuildingKind[] PlayerBuildable =
        {
            BuildingKind.PowerPlant,
            BuildingKind.Refinery,
            BuildingKind.Barracks,
            BuildingKind.WarFactory,
            BuildingKind.GunTower,
            BuildingKind.Wall,
            BuildingKind.NavalYard,
        };

        public static BuildingSpec Get(BuildingKind kind)
        {
            var definition = ContentDatabase.Building(kind);
            return new BuildingSpec(
                definition.Radius,
                definition.NavRadius,
                definition.Cost,
                definition.PowerProduced,
                definition.PowerUsed);
        }

        public static string DisplayName(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.PowerPlant:
                    return "Power Plant";
                case BuildingKind.Refinery:
                    return "Refinery";
                case BuildingKind.Barracks:
                    return "Barracks";
                case BuildingKind.WarFactory:
                    return "War Factory";
                case BuildingKind.GunTower:
                    return "Gun Tower";
                case BuildingKind.Wall:
                    return "Wall";
                case BuildingKind.NavalYard:
                    return "Naval Yard";
                default:
                    return "Construction Yard";
            }
        }
    }

    public static class BuildRules
    {
        public static bool IsWithinBuildRadius(float2 point, float2 yard, float radius)
        {
            return math.distance(point, yard) <= radius;
        }

        /// <summary>Circular footprints overlap when their centres are closer than the sum.</summary>
        public static bool FootprintsOverlap(float2 a, float radiusA, float2 b, float radiusB)
        {
            return math.distance(a, b) < radiusA + radiusB;
        }

        public static bool CanAfford(int credits, int cost)
        {
            return credits >= cost;
        }
    }

    /// <summary>Pure helpers for the singleton economy state.</summary>
    public static class Economy
    {
        public const int CargoCapacity = 700;
        public const float HarvestRate = 60f;

        public static int GetCredits(in EconomyState state, int team)
        {
            return team == 0 ? state.Team0Credits : state.Team1Credits;
        }

        public static void SetCredits(ref EconomyState state, int team, int value)
        {
            if (team == 0)
            {
                state.Team0Credits = value;
            }
            else
            {
                state.Team1Credits = value;
            }
        }

        public static void AddCredits(ref EconomyState state, int team, int amount)
        {
            SetCredits(ref state, team, GetCredits(state, team) + amount);
        }

        public static bool TrySpend(ref EconomyState state, int team, int cost)
        {
            if (!BuildRules.CanAfford(GetCredits(state, team), cost))
            {
                return false;
            }

            AddCredits(ref state, team, -cost);
            return true;
        }

        public static int GetPowerMade(in EconomyState state, int team)
        {
            return team == 0 ? state.Team0PowerMade : state.Team1PowerMade;
        }

        public static int GetPowerUsed(in EconomyState state, int team)
        {
            return team == 0 ? state.Team0PowerUsed : state.Team1PowerUsed;
        }
    }
}
