using CQRa.Sim.Units;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Fog
{
    /// <summary>Fog grid resolution. Coarser than navigation for cheap reveal + rendering.</summary>
    public struct FogGridInfo : IComponentData
    {
        public float2 Origin;
        public int2 Size;
        public float CellSize;

        public bool InBounds(int2 cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;
        }
    }

    /// <summary>Singleton marker plus a change counter used by the renderer.</summary>
    public struct FogState : IComponentData
    {
        public int Revision;
    }

    /// <summary>Which team owns this fog entity.</summary>
    public struct FogTeam : IComponentData
    {
        public int Team;
    }

    /// <summary>0 = never seen, 1 = explored, 2 = currently visible.</summary>
    [InternalBufferCapacity(0)]
    public struct FogCell : IBufferElementData
    {
        public byte Value;
    }

    /// <summary>Vision radius in metres, attached to units and buildings.</summary>
    public struct VisionSource : IComponentData
    {
        public float Radius;
    }

    public static class VisionCatalog
    {
        public static float ForUnit(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Tank: return 40f;
                case UnitKind.Harvester: return 22f;
                case UnitKind.Mcv: return 26f;
                case UnitKind.Gunship: return 44f;
                default: return 32f;
            }
        }

        public static float ForBuilding(CQRa.Sim.Base.BuildingKind kind)
        {
            switch (kind)
            {
                case CQRa.Sim.Base.BuildingKind.ConstructionYard: return 38f;
                case CQRa.Sim.Base.BuildingKind.WarFactory: return 32f;
                case CQRa.Sim.Base.BuildingKind.Refinery: return 28f;
                case CQRa.Sim.Base.BuildingKind.Barracks: return 28f;
                case CQRa.Sim.Base.BuildingKind.GunTower: return 24f;
                case CQRa.Sim.Base.BuildingKind.Wall: return 6f;
                default: return 24f;
            }
        }
    }

    public static class FogMath
    {
        public const byte Never = 0;
        public const byte Explored = 1;
        public const byte Visible = 2;

        public static int2 WorldToCell(in FogGridInfo info, float2 world)
        {
            var local = (world - info.Origin) / info.CellSize;
            return new int2((int)math.floor(local.x), (int)math.floor(local.y));
        }

        public static byte Read(Unity.Collections.NativeArray<FogCell> cells, in FogGridInfo info, float2 world)
        {
            var cell = WorldToCell(info, world);
            if (!info.InBounds(cell))
            {
                return Never;
            }

            return cells[cell.y * info.Size.x + cell.x].Value;
        }

        public static bool IsVisible(Unity.Collections.NativeArray<FogCell> cells, in FogGridInfo info, float2 world)
        {
            return Read(cells, info, world) == Visible;
        }

        public static bool IsExplored(Unity.Collections.NativeArray<FogCell> cells, in FogGridInfo info, float2 world)
        {
            return Read(cells, info, world) >= Explored;
        }

        public static void Decay(Unity.Collections.NativeArray<FogCell> cells)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i].Value == Visible)
                {
                    cells[i] = new FogCell { Value = Explored };
                }
            }
        }
    }
}
