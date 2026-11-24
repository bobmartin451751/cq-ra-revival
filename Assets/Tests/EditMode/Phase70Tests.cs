using CQRa.Sim.Content;
using CQRa.Sim.Navigation;
using CQRa.Sim.Bridges;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase70Tests
    {
        [Test]
        public void BridgeCatalog_CrossesWaterBody()
        {
            var map = new MapDefinition
            {
                WorldSize = 200f,
                WaterBodies = new[]
                {
                    WaterBody.Circle("Lake", 10f, -4f, 30f, 0.55f),
                },
            };

            var specs = BridgeCatalog.Build(map);
            Assert.AreEqual(1, specs.Length);
            Assert.IsTrue(BridgeCatalog.Covers(specs, new float2(10f, -4f)),
                "the bridge must cover the water centre");
            Assert.IsTrue(BridgeCatalog.Covers(specs, new float2(10f, -1f)),
                "the deck spans its half width");
            Assert.IsFalse(BridgeCatalog.Covers(specs, new float2(10f, -4f + 40f)),
                "points beyond the deck are not covered");
            Assert.Greater(specs[0].HalfLength, 30f,
                "the deck must reach past the shoreline");
        }

        [Test]
        public void BridgeCatalog_EmptyWithoutWater()
        {
            var map = new MapDefinition { WaterBodies = System.Array.Empty<WaterBody>() };
            Assert.AreEqual(0, BridgeCatalog.Build(map).Length);
        }

        [Test]
        public void NavGridBuilder_BridgeDeckOverridesWaterBlocking()
        {
            const float size = 40f;
            const float cellSize = 2f;
            var specs = new[]
            {
                new BridgeSpec
                {
                    Index = 0,
                    Centre = float2.zero,
                    Yaw = 0f,
                    HalfLength = 15f,
                    HalfWidth = 5f,
                    Level = 0.9f,
                },
            };

            // A wide water band across the middle of the map.
            bool IsWater(float2 point) => math.abs(point.y) < 12f;
            bool OnBridge(float2 point) => BridgeCatalog.Covers(specs, point);

            var walkable = NavGridBuilder.Build(
                20,
                20,
                cellSize,
                new float2(-size * 0.5f, -size * 0.5f),
                2f,
                (x, z) => 0f,
                null,
                0f,
                0f,
                IsWater,
                OnBridge);

            var info = new NavGridInfo
            {
                Origin = new float2(-size * 0.5f, -size * 0.5f),
                Size = new int2(20, 20),
                CellSize = cellSize,
            };
            var centre = NavGridMath.WorldToCell(info, float2.zero);
            var openWater = NavGridMath.WorldToCell(info, new float2(16f, 0f));

            Assert.AreEqual(1, walkable[centre.y * 20 + centre.x],
                "bridge cells must be walkable over water");
            Assert.AreEqual(0, walkable[openWater.y * 20 + openWater.x],
                "water away from the bridge stays blocked");
        }
    }
}
