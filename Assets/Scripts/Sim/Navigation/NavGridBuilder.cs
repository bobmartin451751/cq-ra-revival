using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace CQRa.Sim.Navigation
{
    /// <summary>
    /// Builds the walkability grid from a height sampler. Pure data, no ECS and no
    /// MonoBehaviours, so it can be unit tested and later moved into a job.
    /// </summary>
    public static class NavGridBuilder
    {
        /// <summary>
        /// Returns one byte per cell: 1 = walkable, 0 = blocked.
        /// Cells are blocked when they are inside the map margin, on a slope steeper
        /// than <paramref name="maxSlope"/>, or inside a circular blocker.
        /// </summary>
        public static byte[] Build(
            int sizeX,
            int sizeZ,
            float cellSize,
            float2 origin,
            float maxSlope,
            Func<float, float, float> sampleHeight,
            IReadOnlyList<NavBlocker> blockers,
            float edgeMargin,
            float obstaclePadding)
        {
            if (sizeX <= 0 || sizeZ <= 0 || cellSize <= 0f)
            {
                return Array.Empty<byte>();
            }

            var cells = new byte[sizeX * sizeZ];
            var minX = origin.x + edgeMargin;
            var maxX = origin.x + sizeX * cellSize - edgeMargin;
            var minZ = origin.y + edgeMargin;
            var maxZ = origin.y + sizeZ * cellSize - edgeMargin;
            var half = cellSize * 0.5f;

            for (var z = 0; z < sizeZ; z++)
            {
                for (var x = 0; x < sizeX; x++)
                {
                    var cell = z * sizeX + x;
                    var cx = origin.x + (x + 0.5f) * cellSize;
                    var cz = origin.y + (z + 0.5f) * cellSize;

                    if (cx < minX || cx > maxX || cz < minZ || cz > maxZ)
                    {
                        cells[cell] = 0;
                        continue;
                    }

                    if (blockers != null)
                    {
                        var blocked = false;
                        for (var b = 0; b < blockers.Count; b++)
                        {
                            var blocker = blockers[b];
                            var dx = cx - blocker.Centre.x;
                            var dz = cz - blocker.Centre.y;
                            var radius = blocker.Radius + obstaclePadding;
                            if (dx * dx + dz * dz < radius * radius)
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                        {
                            cells[cell] = 0;
                            continue;
                        }
                    }

                    var h = sampleHeight(cx, cz);
                    var hx0 = sampleHeight(cx - half, cz);
                    var hx1 = sampleHeight(cx + half, cz);
                    var hz0 = sampleHeight(cx, cz - half);
                    var hz1 = sampleHeight(cx, cz + half);

                    var slopeX = math.max(math.abs(h - hx0), math.abs(hx1 - h)) / half;
                    var slopeZ = math.max(math.abs(h - hz0), math.abs(hz1 - h)) / half;

                    cells[cell] = math.max(slopeX, slopeZ) > maxSlope ? (byte)0 : (byte)1;
                }
            }

            return cells;
        }

        /// <summary>
        /// Finds the closest walkable cell to <paramref name="cell"/>. Ties are broken
        /// by lower linear index, which keeps the result deterministic.
        /// </summary>
        public static int2 NearestWalkable(
            NativeArray<byte> walkable,
            int2 size,
            int2 cell,
            int maxRadius)
        {
            cell = math.clamp(cell, int2.zero, size - 1);
            if (walkable[cell.y * size.x + cell.x] != 0)
            {
                return cell;
            }

            var best = cell;
            var bestDistance = float.MaxValue;
            var bestIndex = int.MaxValue;

            for (var dz = -maxRadius; dz <= maxRadius; dz++)
            {
                var nz = cell.y + dz;
                if (nz < 0 || nz >= size.y)
                {
                    continue;
                }

                for (var dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    var nx = cell.x + dx;
                    if (nx < 0 || nx >= size.x)
                    {
                        continue;
                    }

                    var index = nz * size.x + nx;
                    if (walkable[index] == 0)
                    {
                        continue;
                    }

                    var distance = dx * dx + dz * dz;
                    if (distance < bestDistance || (distance == bestDistance && index < bestIndex))
                    {
                        bestDistance = distance;
                        bestIndex = index;
                        best = new int2(nx, nz);
                    }
                }
            }

            return best;
        }

        public static int CountWalkable(NativeArray<byte> walkable)
        {
            var count = 0;
            for (var i = 0; i < walkable.Length; i++)
            {
                count += walkable[i] != 0 ? 1 : 0;
            }

            return count;
        }
    }
}
