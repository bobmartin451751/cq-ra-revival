using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase10Tests
    {
        [Test]
        public void VisionRadii_ArePositiveAndSensible()
        {
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                Assert.Greater(VisionCatalog.ForUnit(kind), 0f, $"{kind} must see something");
            }

            foreach (CQRa.Sim.Base.BuildingKind kind in System.Enum.GetValues(typeof(CQRa.Sim.Base.BuildingKind)))
            {
                Assert.Greater(VisionCatalog.ForBuilding(kind), 0f, $"{kind} must see something");
            }

            Assert.Greater(
                VisionCatalog.ForUnit(UnitKind.Tank),
                VisionCatalog.ForUnit(UnitKind.Harvester),
                "tanks should scout better than harvesters");
        }

        [Test]
        public void FogMath_ReadsVisibilityAndDecaysToExplored()
        {
            var info = new FogGridInfo
            {
                Origin = float2.zero,
                Size = new int2(4, 4),
                CellSize = 4f,
            };

            var cells = new NativeArray<FogCell>(16, Allocator.Temp);
            try
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = new FogCell { Value = FogMath.Never };
                }

                cells[1 * 4 + 1] = new FogCell { Value = FogMath.Visible };

                Assert.IsTrue(FogMath.IsVisible(cells, info, new float2(6f, 6f)));
                Assert.IsFalse(FogMath.IsExplored(cells, info, new float2(30f, 30f)));

                FogMath.Decay(cells);

                Assert.IsFalse(FogMath.IsVisible(cells, info, new float2(6f, 6f)));
                Assert.IsTrue(FogMath.IsExplored(cells, info, new float2(6f, 6f)));
            }
            finally
            {
                cells.Dispose();
            }
        }

        [Test]
        public void FogMath_OutOfBoundsIsNeverSeen()
        {
            var info = new FogGridInfo
            {
                Origin = new float2(-10f, -10f),
                Size = new int2(2, 2),
                CellSize = 4f,
            };

            var cells = new NativeArray<FogCell>(4, Allocator.Temp);
            try
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = new FogCell { Value = FogMath.Never };
                }

                Assert.AreEqual(FogMath.Never, FogMath.Read(cells, info, new float2(100f, 100f)));
            }
            finally
            {
                cells.Dispose();
            }
        }
    }
}
