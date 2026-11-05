using Unity.Entities;

namespace CQRa.Sim.Units
{
    public enum UnitKind : byte
    {
        Infantry = 0,
        Tank = 1,
        Harvester = 2,
    }

    /// <summary>Marks an entity as a mobile unit.</summary>
    public struct UnitTag : IComponentData
    {
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
        public const int TeamCount = 2;
        public const int KindCount = 3;
        public const int HighlightIndex = TeamCount * KindCount;
        public const int Count = HighlightIndex + 1;

        public static int BaseIndex(int team, UnitKind kind)
        {
            return team * KindCount + (int)kind;
        }
    }
}
