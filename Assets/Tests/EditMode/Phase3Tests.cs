using System;
using CQRa.Sim.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase3Tests
    {
        [Test]
        public void Walkability_FlatGround_InteriorWalkable_BorderBlocked()
        {
            var cells = NavGridBuilder.Build(
                32, 32, 2f, float2.zero, 0.5f,
                (x, z) => 0f,
                Array.Empty<NavBlocker>(),
                8f,
                2f);

            Assert.AreEqual(32 * 32, cells.Length);
            Assert.AreEqual(1, cells[16 * 32 + 16], "map centre must be walkable");
            Assert.AreEqual(0, cells[0], "margin cells are outside the playable area");
            Assert.AreEqual(0, cells[31 * 32 + 31], "margin cells are outside the playable area");
        }

        [Test]
        public void Walkability_SteepCliff_IsBlocked()
        {
            var cells = NavGridBuilder.Build(
                32, 32, 2f, float2.zero, 0.5f,
                (x, z) => x > 32f ? 20f : 0f,
                Array.Empty<NavBlocker>(),
                0f,
                0f);

            Assert.AreEqual(0, cells[16 * 32 + 16], "cell straddling the cliff is too steep");
            Assert.AreEqual(1, cells[16 * 32 + 4], "flat ground remains walkable");
        }

        [Test]
        public void Walkability_Obstacle_BlocksDiscPlusPadding()
        {
            var blockers = new[]
            {
                new NavBlocker { Centre = new float2(32f, 32f), Radius = 6f },
            };

            var cells = NavGridBuilder.Build(
                32, 32, 2f, float2.zero, 0.5f,
                (x, z) => 0f,
                blockers,
                0f,
                2f);

            Assert.AreEqual(0, cells[16 * 32 + 16], "obstacle centre is blocked");
            Assert.AreEqual(1, cells[2 * 32 + 2], "far ground is unaffected");
        }

        [Test]
        public void NearestWalkable_ReturnsClosestFreeCell()
        {
            var walkable = new NativeArray<byte>(16 * 16, Allocator.Temp);
            try
            {
                for (var i = 0; i < walkable.Length; i++)
                {
                    walkable[i] = 1;
                }

                walkable[8 * 16 + 8] = 0;

                var nearest = NavGridBuilder.NearestWalkable(
                    walkable,
                    new int2(16, 16),
                    new int2(8, 8),
                    4);

                Assert.IsFalse(nearest.Equals(new int2(8, 8)));
                Assert.AreEqual(1, walkable[nearest.y * 16 + nearest.x]);
                Assert.LessOrEqual(math.distance(nearest, new int2(8, 8)), 1.5f);
            }
            finally
            {
                walkable.Dispose();
            }
        }

        [Test]
        public void FlowField_PointsAroundWall_AndReachesDestination()
        {
            const int size = 40;
            var walkable = new NativeArray<byte>(size * size, Allocator.TempJob);
            var directions = new NativeArray<byte>(size * size, Allocator.TempJob);
            var integration = new NativeArray<int>(size * size, Allocator.TempJob);

            try
            {
                for (var i = 0; i < walkable.Length; i++)
                {
                    walkable[i] = 1;
                }

                // Vertical wall with a gap near the bottom.
                for (var z = 0; z < size; z++)
                {
                    if (z < 4)
                    {
                        continue;
                    }

                    walkable[z * size + 20] = 0;
                }

                var destination = new int2(34, 2);
                FlowFieldBuilder.Build(walkable, new int2(size, size), destination, directions, integration);

                Assert.AreEqual(
                    NavGridMath.NoDirection,
                    directions[destination.y * size + destination.x],
                    "the destination cell has no outgoing direction");

                // Walk the field from the far side of the wall.
                var current = new int2(3, 30);
                var steps = 0;
                while (!current.Equals(destination) && steps < 400)
                {
                    var index = current.y * size + current.x;
                    var direction = directions[index];
                    Assert.AreNotEqual(NavGridMath.NoDirection, direction, "field should be complete");
                    current += NavGridMath.Offset(direction);
                    Assert.AreEqual(1, walkable[current.y * size + current.x], "path never enters a blocked cell");
                    steps++;
                }

                Assert.AreEqual(destination, current, "following the field reaches the destination");
            }
            finally
            {
                walkable.Dispose();
                directions.Dispose();
                integration.Dispose();
            }
        }

        [Test]
        public void FlowField_IsDeterministic()
        {
            const int size = 32;
            var walkable = new NativeArray<byte>(size * size, Allocator.TempJob);
            var first = new NativeArray<byte>(size * size, Allocator.TempJob);
            var second = new NativeArray<byte>(size * size, Allocator.TempJob);
            var integration = new NativeArray<int>(size * size, Allocator.TempJob);

            try
            {
                for (var i = 0; i < walkable.Length; i++)
                {
                    walkable[i] = 1;
                }

                for (var z = 6; z < 26; z++)
                {
                    walkable[z * size + 16] = 0;
                }

                var destination = new int2(24, 12);
                FlowFieldBuilder.Build(walkable, new int2(size, size), destination, first, integration);
                FlowFieldBuilder.Build(walkable, new int2(size, size), destination, second, integration);

                for (var i = 0; i < first.Length; i++)
                {
                    if (first[i] != second[i])
                    {
                        Assert.Fail($"direction mismatch at cell {i}");
                    }
                }
            }
            finally
            {
                walkable.Dispose();
                first.Dispose();
                second.Dispose();
                integration.Dispose();
            }
        }

        [Test]
        public void FlowField_UnreachableRegion_HasNoDirectionsAndTerminates()
        {
            const int size = 24;
            var walkable = new NativeArray<byte>(size * size, Allocator.TempJob);
            var directions = new NativeArray<byte>(size * size, Allocator.TempJob);
            var integration = new NativeArray<int>(size * size, Allocator.TempJob);

            try
            {
                for (var i = 0; i < walkable.Length; i++)
                {
                    walkable[i] = 1;
                }

                // Complete wall: no gap.
                for (var z = 0; z < size; z++)
                {
                    walkable[z * size + 12] = 0;
                }

                FlowFieldBuilder.Build(walkable, new int2(size, size), new int2(18, 12), directions, integration);

                Assert.AreEqual(
                    NavGridMath.NoDirection,
                    directions[4 * size + 4],
                    "cells on the far side of an unbroken wall have no direction");
                Assert.AreEqual(
                    int.MaxValue,
                    integration[4 * size + 4],
                    "unreachable cells stay at max cost");
            }
            finally
            {
                walkable.Dispose();
                directions.Dispose();
                integration.Dispose();
            }
        }

        [Test]
        public void FlowField_BlockedDestination_ProducesEmptyField()
        {
            const int size = 8;
            var walkable = new NativeArray<byte>(size * size, Allocator.TempJob);
            var directions = new NativeArray<byte>(size * size, Allocator.TempJob);
            var integration = new NativeArray<int>(size * size, Allocator.TempJob);

            try
            {
                for (var i = 0; i < walkable.Length; i++)
                {
                    walkable[i] = 1;
                }

                walkable[4 * size + 4] = 0;
                FlowFieldBuilder.Build(walkable, new int2(size, size), new int2(4, 4), directions, integration);

                for (var i = 0; i < directions.Length; i++)
                {
                    Assert.AreEqual(
                        NavGridMath.NoDirection,
                        directions[i],
                        "a blocked destination must not produce directions");
                }
            }
            finally
            {
                walkable.Dispose();
                directions.Dispose();
                integration.Dispose();
            }
        }

        [Test]
        public void TerrainSampler_MatchesFlatAndSlope()
        {
            var heights = new NativeArray<TerrainHeightSample>(9, Allocator.Temp);
            try
            {
                for (var z = 0; z < 3; z++)
                {
                    for (var x = 0; x < 3; x++)
                    {
                        heights[z * 3 + x] = new TerrainHeightSample { Height = x };
                    }
                }

                var info = new TerrainInfo { Resolution = 3, Size = 20f };

                Assert.AreEqual(0f, TerrainSampler.Sample(heights, info, -10f, 0f), 0.001f);
                Assert.AreEqual(1f, TerrainSampler.Sample(heights, info, 0f, 0f), 0.001f);
                Assert.AreEqual(2f, TerrainSampler.Sample(heights, info, 10f, 10f), 0.001f);
            }
            finally
            {
                heights.Dispose();
            }
        }
    }
}
