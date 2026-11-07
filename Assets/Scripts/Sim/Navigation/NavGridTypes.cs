using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Navigation
{
    /// <summary>
    /// Deterministic grid description used by pathfinding and steering.
    /// Cells are square; <see cref="Origin"/> is the world-space minimum corner.
    /// </summary>
    public struct NavGridInfo : IComponentData
    {
        public float2 Origin;
        public int2 Size;
        public float CellSize;
        public float MaxSlope;

        public int CellCount => Size.x * Size.y;

        public bool InBounds(int2 cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;
        }
    }

    /// <summary>One walkability cell. 1 = walkable, 0 = blocked.</summary>
    public struct NavCell : IBufferElementData
    {
        public byte Walkable;
    }

    /// <summary>Bilinear terrain height samples owned by the simulation (copied from the heightmap).</summary>
    public struct TerrainHeightSample : IBufferElementData
    {
        public float Height;
    }

    /// <summary>Terrain description that pairs with <see cref="TerrainHeightSample"/>.</summary>
    public struct TerrainInfo : IComponentData
    {
        public int Resolution;
        public float Size;
    }

    /// <summary>Identifies a cached flow field and the destination cell it leads to.</summary>
    public struct FlowFieldMeta : IComponentData
    {
        public int2 Destination;
        public int Generation;
    }

    /// <summary>One direction byte per navigation cell. 0..7 = neighbour index, 255 = no direction.</summary>
    [InternalBufferCapacity(0)]
    public struct FlowFieldDirection : IBufferElementData
    {
        public byte Value;
    }

    /// <summary>A circular no-go area (rocks, ruins, later buildings).</summary>
    public struct NavBlocker
    {
        public float2 Centre;
        public float Radius;
    }

    public static class NavGridMath
    {
        public const byte NoDirection = 255;

        public static int2 WorldToCell(in NavGridInfo info, float2 world)
        {
            var local = (world - info.Origin) / info.CellSize;
            return new int2((int)math.floor(local.x), (int)math.floor(local.y));
        }

        public static float2 CellCentre(in NavGridInfo info, int2 cell)
        {
            return info.Origin + (new float2(cell) + 0.5f) * info.CellSize;
        }

        public static bool InBounds(in NavGridInfo info, int2 cell)
        {
            return info.InBounds(cell);
        }

        /// <summary>Neighbour offset for a flow-field direction byte (0..7).</summary>
        public static int2 Offset(int index)
        {
            switch (index)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(1, 1);
                case 2: return new int2(0, 1);
                case 3: return new int2(-1, 1);
                case 4: return new int2(-1, 0);
                case 5: return new int2(-1, -1);
                case 6: return new int2(0, -1);
                default: return new int2(1, -1);
            }
        }

        public static float2 Direction(int index)
        {
            return math.normalizesafe(new float2(Offset(index)));
        }

        public static int StepCost(int index)
        {
            return (index & 1) == 1 ? 14 : 10;
        }
    }
}
