using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// A single move order. The field entity points into the shared flow-field cache;
    /// it is resolved by <see cref="FlowFieldSystem"/> on the next fixed tick.
    /// </summary>
    public struct MoveTarget : IComponentData
    {
        /// <summary>This unit's own arrival point (formation slot).</summary>
        public float3 Destination;

        /// <summary>Shared group destination used to build the flow field.</summary>
        public float3 PathDestination;

        public Entity Field;
        public int2 DestinationCell;
        public float ArriveRadius;

        /// <summary>Radius of the formation the unit belongs to (0 for a solo order).</summary>
        public float FormationRadius;
        public byte HasOrder;
    }

    /// <summary>Per-unit motion state. Velocity is XZ only; Y follows the terrain.</summary>
    public struct UnitMotion : IComponentData
    {
        public float2 Velocity;
        public float Yaw;
        public float Speed;
    }
}
