using Unity.Collections;
using Unity.Mathematics;

namespace CQRa.Sim.Navigation
{
    /// <summary>
    /// Bilinear height sampling over the simulation-owned height buffer.
    /// Mirrors <c>HeightmapTerrain.SampleHeight</c> but works without MonoBehaviours
    /// so systems and jobs can use it.
    /// </summary>
    public static class TerrainSampler
    {
        public static float Sample(
            NativeArray<TerrainHeightSample> heights,
            in TerrainInfo info,
            float worldX,
            float worldZ)
        {
            var res = info.Resolution;
            if (res < 2 || heights.Length < res * res || info.Size <= 0f)
            {
                return 0f;
            }

            var fx = math.clamp(worldX / info.Size + 0.5f, 0f, 1f) * (res - 1);
            var fz = math.clamp(worldZ / info.Size + 0.5f, 0f, 1f) * (res - 1);

            var x0 = (int)math.floor(fx);
            var z0 = (int)math.floor(fz);
            var x1 = math.min(x0 + 1, res - 1);
            var z1 = math.min(z0 + 1, res - 1);

            var tx = fx - x0;
            var tz = fz - z0;

            var h00 = heights[z0 * res + x0].Height;
            var h10 = heights[z0 * res + x1].Height;
            var h01 = heights[z1 * res + x0].Height;
            var h11 = heights[z1 * res + x1].Height;

            var a = math.lerp(h00, h10, tx);
            var b = math.lerp(h01, h11, tx);
            return math.lerp(a, b, tz);
        }
    }
}
