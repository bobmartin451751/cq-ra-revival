using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using Unity.Entities;

namespace CQRa.Sim.Base
{
    /// <summary>Marks a building that can build units.</summary>
    public struct ProductionTag : IComponentData
    {
    }

    /// <summary>One queued unit. The front element is the item currently being built.</summary>
    public struct ProductionOrder : IBufferElementData
    {
        public UnitKind Kind;
        public float Remaining;
        public float Total;
    }

    /// <summary>Pickup crate. Kind 0 = credits, kind 1 = repair.</summary>
    public struct Crate : IComponentData
    {
        public int Reward;
        public byte Kind;
    }

    /// <summary>Where completed units gather. Set by right-clicking with the building selected.</summary>
    public struct RallyPoint : IComponentData
    {
        public Unity.Mathematics.float3 Value;
        public byte HasValue;
    }

    public readonly struct ProductionSpec
    {
        public readonly int Cost;
        public readonly float Seconds;
        public readonly BuildingKind Producer;
        public readonly string Label;

        public ProductionSpec(int cost, float seconds, BuildingKind producer, string label)
        {
            Cost = cost;
            Seconds = seconds;
            Producer = producer;
            Label = label;
        }
    }

    /// <summary>Unit costs, build times and which building produces each kind.</summary>
    public static class ProductionCatalog
    {
        public const int QueueLimit = 5;

        public static readonly UnitKind[] Buildable =
        {
            UnitKind.Infantry,
            UnitKind.Tank,
            UnitKind.Harvester,
            UnitKind.Mcv,
            UnitKind.Gunship,
            UnitKind.Engineer,
        };

        private static readonly UnitKind[] AshfallRoster = WithUnique(UnitKind.Thumper, UnitKind.Rig);
        private static readonly UnitKind[] VerdantRoster = WithUnique(UnitKind.Mantis, UnitKind.Sower);
        private static readonly UnitKind[] MeridianRoster = WithUnique(UnitKind.Skyfall);

        public static UnitKind UniqueFor(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant: return UnitKind.Mantis;
                case FactionId.Meridian: return UnitKind.Skyfall;
                default: return UnitKind.Thumper;
            }
        }

        public static UnitKind[] RosterFor(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant: return VerdantRoster;
                case FactionId.Meridian: return MeridianRoster;
                default: return AshfallRoster;
            }
        }

        public static UnitKind[] RosterForTeam(int team)
        {
            return RosterFor(FactionRuntime.ForTeam(team));
        }

        public static string UnitName(FactionId faction, UnitKind kind)
        {
            switch (faction)
            {
                case FactionId.Verdant:
                    if (kind == UnitKind.Infantry) return "Thorn";
                    if (kind == UnitKind.Tank) return "Stinger";
                    if (kind == UnitKind.Gunship) return "Wasp";
                    break;
                case FactionId.Meridian:
                    if (kind == UnitKind.Infantry) return "Aegis";
                    if (kind == UnitKind.Tank) return "Lumen";
                    if (kind == UnitKind.Gunship) return "Falcon";
                    break;
                default:
                    if (kind == UnitKind.Infantry) return "Sapper";
                    if (kind == UnitKind.Tank) return "Bulwark";
                    if (kind == UnitKind.Gunship) return "Vulture";
                    break;
            }

            return Get(kind).Label;
        }

        private static UnitKind[] WithUnique(params UnitKind[] uniques)
        {
            var roster = new UnitKind[Buildable.Length + uniques.Length];
            for (var i = 0; i < Buildable.Length; i++)
            {
                roster[i] = Buildable[i];
            }

            for (var i = 0; i < uniques.Length; i++)
            {
                roster[Buildable.Length + i] = uniques[i];
            }

            return roster;
        }

        public static ProductionSpec Get(UnitKind kind)
        {
            var definition = ContentDatabase.Unit(kind);
            return new ProductionSpec(
                definition.Cost,
                definition.BuildSeconds,
                definition.Producer,
                definition.Label.ToString());
        }

        /// <summary>Hotkeys shown in the production panel, in display order.</summary>
        public static string Hotkey(int index)
        {
            switch (index)
            {
                case 0: return "Z";
                case 1: return "X";
                case 2: return "C";
                case 3: return "V";
                case 4: return "B";
                case 5: return "G";
                case 6: return "Y";
                default: return "U";
            }
        }
    }
}
