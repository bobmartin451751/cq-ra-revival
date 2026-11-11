using Unity.Collections;
using Unity.Mathematics;

namespace CQRa.Sim.Navigation
{
    /// <summary>
    /// Terrain line-of-sight. A straight line is marched between an observer's eye
    /// and a target point; if the ground rises above that line, the target is hidden.
    /// Deterministic and cheap enough for the 4 m fog grid and combat acquisition.
    /// </summary>
    public static class TerrainLineOfSight
    {
        public const float EyeClearance = 0.35f;
        public const float SampleStep = 1.5f;

        public static bool IsBlocked(
            NativeArray<TerrainHeightSample> heights,
            in TerrainInfo info,
            float3 from,
            float eyeHeight,
            float2 to)
        {
            var dx = to.x - from.x;
            var dz = to.y - from.z;
            var distance = math.sqrt(dx * dx + dz * dz);
            if (distance <= SampleStep)
            {
                return false;
            }

            var sourceY = from.y + eyeHeight;
            var targetGround = TerrainSampler.Sample(heights, info, to.x, to.y);
            var targetY = targetGround + EyeClearance;

            var steps = (int)math.ceil(distance / SampleStep);
            for (var i = 1; i < steps; i++)
            {
                var t = i / (float)steps;
                var x = from.x + dx * t;
                var z = from.z + dz * t;
                var ground = TerrainSampler.Sample(heights, info, x, z);
                var sightLine = math.lerp(sourceY, targetY, t);

                if (ground > sightLine + EyeClearance)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
