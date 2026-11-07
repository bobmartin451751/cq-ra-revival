using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace CQRa.Sim.Navigation
{
    /// <summary>
    /// Builds a flow field with an 8-connected Dijkstra pass.
    ///
    /// The integration field stores the cost-to-destination for every walkable cell;
    /// the direction field stores the neighbour that decreases that cost. Steering
    /// only has to read one byte per unit per tick, which keeps movement cheap even
    /// when many units share a destination.
    ///
    /// Burst-compiled and fully deterministic: the priority heap breaks ties by cell
    /// index, so replays produce identical fields.
    /// </summary>
    [BurstCompile]
    public struct BuildFlowFieldJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Walkable;
        public int2 Size;
        public int2 Destination;

        public NativeArray<byte> Directions;
        public NativeArray<int> Integration;

        public void Execute()
        {
            var count = Size.x * Size.y;
            for (var i = 0; i < count; i++)
            {
                Integration[i] = int.MaxValue;
                Directions[i] = NavGridMath.NoDirection;
            }

            if (Destination.x < 0 || Destination.y < 0 ||
                Destination.x >= Size.x || Destination.y >= Size.y)
            {
                return;
            }

            var start = Destination.y * Size.x + Destination.x;
            if (Walkable[start] == 0)
            {
                return;
            }

            var heap = new NativeArray<int>(count, Allocator.Temp);
            var heapPositions = new NativeArray<int>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                heapPositions[i] = -1;
            }

            var heapCount = 0;
            Integration[start] = 0;
            HeapPush(heap, heapPositions, ref heapCount, Integration, start);

            while (heapCount > 0)
            {
                var cell = HeapPop(heap, heapPositions, ref heapCount, Integration);
                var cx = cell % Size.x;
                var cy = cell / Size.x;
                var baseCost = Integration[cell];

                for (var direction = 0; direction < 8; direction++)
                {
                    var offset = NavGridMath.Offset(direction);
                    var nx = cx + offset.x;
                    var ny = cy + offset.y;

                    if (nx < 0 || ny < 0 || nx >= Size.x || ny >= Size.y)
                    {
                        continue;
                    }

                    var neighbour = ny * Size.x + nx;
                    if (Walkable[neighbour] == 0)
                    {
                        continue;
                    }

                    // Do not cut diagonal corners through blocked cells.
                    if (offset.x != 0 && offset.y != 0)
                    {
                        if (Walkable[cy * Size.x + nx] == 0 || Walkable[ny * Size.x + cx] == 0)
                        {
                            continue;
                        }
                    }

                    var candidate = baseCost + NavGridMath.StepCost(direction);
                    if (candidate < Integration[neighbour])
                    {
                        Integration[neighbour] = candidate;
                        HeapPush(heap, heapPositions, ref heapCount, Integration, neighbour);
                    }
                }
            }

            // Direction pass: point every cell at the neighbour with a strictly lower
            // integration cost. Equal costs are impossible along a shortest path, so
            // following the field always terminates at the destination.
            for (var y = 0; y < Size.y; y++)
            {
                for (var x = 0; x < Size.x; x++)
                {
                    var cell = y * Size.x + x;
                    if (Walkable[cell] == 0 || Integration[cell] == int.MaxValue)
                    {
                        continue;
                    }

                    var bestCost = int.MaxValue;
                    var bestDirection = NavGridMath.NoDirection;

                    for (var direction = 0; direction < 8; direction++)
                    {
                        var offset = NavGridMath.Offset(direction);
                        var nx = x + offset.x;
                        var ny = y + offset.y;

                        if (nx < 0 || ny < 0 || nx >= Size.x || ny >= Size.y)
                        {
                            continue;
                        }

                        var neighbour = ny * Size.x + nx;
                        if (Walkable[neighbour] == 0)
                        {
                            continue;
                        }

                        if (offset.x != 0 && offset.y != 0)
                        {
                            if (Walkable[y * Size.x + nx] == 0 || Walkable[ny * Size.x + x] == 0)
                            {
                                continue;
                            }
                        }

                        var neighbourCost = Integration[neighbour];
                        if (neighbourCost < Integration[cell] && neighbourCost < bestCost)
                        {
                            bestCost = neighbourCost;
                            bestDirection = (byte)direction;
                        }
                    }

                    Directions[cell] = (byte)bestDirection;
                }
            }

            heap.Dispose();
            heapPositions.Dispose();
        }

        private static bool Less(NativeArray<int> key, int a, int b)
        {
            var ka = key[a];
            var kb = key[b];
            return ka < kb || (ka == kb && a < b);
        }

        private static void HeapPush(
            NativeArray<int> heap,
            NativeArray<int> positions,
            ref int count,
            NativeArray<int> key,
            int node)
        {
            var index = positions[node];
            if (index < 0)
            {
                index = count;
                count++;
                heap[index] = node;
                positions[node] = index;
            }

            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (!Less(key, heap[index], heap[parent]))
                {
                    break;
                }

                Swap(heap, positions, index, parent);
                index = parent;
            }
        }

        private static int HeapPop(
            NativeArray<int> heap,
            NativeArray<int> positions,
            ref int count,
            NativeArray<int> key)
        {
            var root = heap[0];
            positions[root] = -1;
            count--;

            if (count > 0)
            {
                heap[0] = heap[count];
                positions[heap[0]] = 0;

                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= count)
                    {
                        break;
                    }

                    var right = left + 1;
                    var smallest = left;
                    if (right < count && Less(key, heap[right], heap[left]))
                    {
                        smallest = right;
                    }

                    if (!Less(key, heap[smallest], heap[index]))
                    {
                        break;
                    }

                    Swap(heap, positions, index, smallest);
                    index = smallest;
                }
            }

            return root;
        }

        private static void Swap(NativeArray<int> heap, NativeArray<int> positions, int a, int b)
        {
            var temp = heap[a];
            heap[a] = heap[b];
            heap[b] = temp;
            positions[heap[a]] = a;
            positions[heap[b]] = b;
        }
    }

    public static class FlowFieldBuilder
    {
        /// <summary>Convenience wrapper for tests and tools.</summary>
        public static void Build(
            NativeArray<byte> walkable,
            int2 size,
            int2 destination,
            NativeArray<byte> directions,
            NativeArray<int> integration)
        {
            new BuildFlowFieldJob
            {
                Walkable = walkable,
                Size = size,
                Destination = destination,
                Directions = directions,
                Integration = integration,
            }.Run();
        }
    }
}
