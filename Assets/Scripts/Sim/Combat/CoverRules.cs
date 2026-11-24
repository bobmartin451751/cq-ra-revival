using CQRa.Sim.Content;
using CQRa.Sim.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Combat
{
    /// <summary>
    /// Deterministic infantry cover. Cover is purely positional: a nearby ridge or
    /// depression on the line to the attacker, or the edge of a ruined outcrop, reduces
    /// incoming damage. Vehicles never benefit, and the AI reads the same values.
    /// </summary>
    public static class CoverRules
    {
        public const float RidgeHeavy = 1.6f;
        public const float RidgeLight = 0.8f;
        public const float RidgeHeavyMultiplier = 0.70f;
        public const float RidgeLightMultiplier = 0.85f;
        public const float ObstacleMultiplier = 0.78f;
        public const float ObstaclePadding = 3f;

        public static float Multiplier(
            EntityManager entityManager,
            float3 targetPosition,
            float3 attackerPosition,
            bool targetIsInfantry)
        {
            if (!targetIsInfantry)
            {
                return 1f;
            }

            var multiplier = 1f;

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<TerrainInfo>(),
                       ComponentType.ReadOnly<TerrainHeightSample>()))
            {
                if (!query.IsEmptyIgnoreFilter)
                {
                    var terrain = query.GetSingletonEntity();
                    var info = entityManager.GetComponentData<TerrainInfo>(terrain);
                    var heights = entityManager.GetBuffer<TerrainHeightSample>(terrain, true);
                    multiplier = math.min(
                        multiplier,
                        TerrainMultiplier(
                            heights.AsNativeArray(),
                            info,
                            targetPosition,
                            attackerPosition));
                }
            }

            var map = MapRuntime.Current;
            if (map?.Obstacles != null)
            {
                var point = new float2(targetPosition.x, targetPosition.z);
                for (var i = 0; i < map.Obstacles.Length; i++)
                {
                    var obstacle = map.Obstacles[i];
                    if (math.distance(point, obstacle.Centre) <=
                        obstacle.Radius + ObstaclePadding)
                    {
                        multiplier = math.min(multiplier, ObstacleMultiplier);
                        break;
                    }
                }
            }

            return multiplier;
        }

        public static float TerrainMultiplier(
            NativeArray<TerrainHeightSample> heights,
            in TerrainInfo info,
            float3 targetPosition,
            float3 attackerPosition)
        {
            if (heights.Length == 0 || info.Resolution < 2)
            {
                return 1f;
            }

            var targetHeight = TerrainSampler.Sample(
                heights, info, targetPosition.x, targetPosition.z);
            var toAttacker = new float2(
                attackerPosition.x - targetPosition.x,
                attackerPosition.z - targetPosition.z);
            var distance = math.length(toAttacker);
            var direction = distance > 0.01f
                ? toAttacker / distance
                : new float2(1f, 0f);
            var reach = math.min(distance, 6f);

            var bestRise = 0f;
            for (var step = 1; step <= 4; step++)
            {
                var offset = reach * (step / 4f);
                var point = new float2(targetPosition.x, targetPosition.z) + direction * offset;
                var height = TerrainSampler.Sample(heights, info, point.x, point.y);
                bestRise = math.max(bestRise, height - targetHeight);
            }

            // A dip works as cover from every direction, not only toward the attacker.
            bestRise = math.max(bestRise, RingRise(heights, info, targetPosition, targetHeight));

            if (bestRise >= RidgeHeavy)
            {
                return RidgeHeavyMultiplier;
            }

            if (bestRise >= RidgeLight)
            {
                return RidgeLightMultiplier;
            }

            return 1f;
        }

        private static float RingRise(
            NativeArray<TerrainHeightSample> heights,
            in TerrainInfo info,
            float3 targetPosition,
            float targetHeight)
        {
            var best = 0f;
            for (var i = 0; i < 4; i++)
            {
                var angle = i * math.PI * 0.5f;
                var offset = new float2(math.cos(angle), math.sin(angle)) * 3f;
                var point = new float2(targetPosition.x, targetPosition.z) + offset;
                var height = TerrainSampler.Sample(heights, info, point.x, point.y);
                best = math.max(best, height - targetHeight);
            }

            return best;
        }
    }
}
