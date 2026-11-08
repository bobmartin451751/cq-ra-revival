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
        };

        public static ProductionSpec Get(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Infantry:
                    return new ProductionSpec(100, 4f, BuildingKind.Barracks, "Infantry");
                case UnitKind.Tank:
                    return new ProductionSpec(350, 8f, BuildingKind.WarFactory, "Tank");
                case UnitKind.Harvester:
                    return new ProductionSpec(500, 10f, BuildingKind.WarFactory, "Harvester");
                case UnitKind.Mcv:
                    return new ProductionSpec(1000, 12f, BuildingKind.WarFactory, "MCV");
                case UnitKind.Gunship:
                    return new ProductionSpec(900, 10f, BuildingKind.WarFactory, "Gunship");
                default:
                    return default;
            }
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
                default: return "B";
            }
        }
    }
}
