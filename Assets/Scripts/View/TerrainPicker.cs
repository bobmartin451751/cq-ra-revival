using UnityEngine;

namespace CQRa.View
{
    /// <summary>
    /// Heightmap ray picking without colliders or physics. Marches the camera ray in
    /// fixed steps, detects the first above-to-below surface crossing, then bisects.
    /// </summary>
    public static class TerrainPicker
    {
        private const float StepMetres = 1.25f;
        private const float BisectIterations = 12;

        public static bool TryPick(
            Camera camera,
            Vector2 screenPosition,
            HeightmapTerrain terrain,
            out Vector3 point)
        {
            point = default;

            if (camera == null || terrain == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(screenPosition);
            var origin = ray.origin;
            var direction = ray.direction;
            var maxDistance = terrain.Size * 2.5f;

            var previousT = 0f;
            var previousDiff = Height(terrain, origin);

            for (var t = StepMetres; t <= maxDistance; t += StepMetres)
            {
                var sample = origin + direction * t;
                var diff = sample.y - terrain.SampleHeight(sample.x, sample.z);

                if (previousDiff > 0f && diff <= 0f)
                {
                    var low = previousT;
                    var high = t;

                    for (var i = 0; i < BisectIterations; i++)
                    {
                        var mid = (low + high) * 0.5f;
                        var at = origin + direction * mid;
                        var midDiff = at.y - terrain.SampleHeight(at.x, at.z);

                        if (midDiff > 0f)
                        {
                            low = mid;
                        }
                        else
                        {
                            high = mid;
                        }
                    }

                    var hit = origin + direction * high;
                    if (terrain.Contains(hit.x, hit.z))
                    {
                        point = new Vector3(hit.x, terrain.SampleHeight(hit.x, hit.z), hit.z);
                        return true;
                    }

                    return false;
                }

                previousT = t;
                previousDiff = diff;

                if (previousDiff < 0f && t > StepMetres * 4f)
                {
                    // Started below the surface and never rose above it.
                    return false;
                }
            }

            return false;
        }

        private static float Height(HeightmapTerrain terrain, Vector3 position)
        {
            return position.y - terrain.SampleHeight(position.x, position.z);
        }
    }
}
