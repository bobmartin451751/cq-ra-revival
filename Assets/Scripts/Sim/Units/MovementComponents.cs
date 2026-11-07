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
        public float3 Destination;
        public Entity Field;
        public int2 DestinationCell;
        public float ArriveRadius;
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
