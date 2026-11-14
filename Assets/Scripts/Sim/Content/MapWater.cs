using System;
using Unity.Mathematics;

namespace CQRa.Sim.Content
{
    /// <summary>
    /// Deterministic helpers for the selected map's water. Water bodies are either
    /// circles or simple polygons; nav blocking, terrain carving, ship clamping and
    /// dock placement all use the same geometry.
    /// </summary>
    public static class MapWater
    {
        public static WaterBody[] Bodies =>
            MapRuntime.Current?.WaterBodies ?? Array.Empty<WaterBody>();

        public static bool HasWater => Bodies.Length > 0;

        public static int BodyCount => Bodies.Length;

        public static WaterBody Body(int index)
        {
            var bodies = Bodies;
            if (bodies.Length == 0)
            {
                return null;
            }

            index = math.clamp(index, 0, bodies.Length - 1);
            return bodies[index];
        }

        public static bool ContainsAny(float2 point)
        {
            var bodies = Bodies;
            for (var i = 0; i < bodies.Length; i++)
            {
                if (Contains(bodies[i], point))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Contains(int index, float2 point)
        {
            var body = Body(index);
            return body != null && Contains(body, point);
        }

        /// <summary>Positive outside the water, negative inside.</summary>
        public static float SignedDistance(int index, float2 point)
        {
            var body = Body(index);
            if (body == null)
            {
                return float.MaxValue;
            }

            return SignedDistance(body, point);
        }

        /// <summary>Distance from the shoreline regardless of side.</summary>
        public static float DistanceToBoundary(int index, float2 point)
        {
            return math.abs(SignedDistance(index, point));
        }

        public static float Level(int index)
        {
            var body = Body(index);
            return body?.Level ?? 0f;
        }

        public static float2 Centre(int index)
        {
            var body = Body(index);
            if (body == null)
            {
                return float2.zero;
            }

            if (body.Kind == WaterShapeKind.Circle)
            {
                return body.Centre;
            }

            var points = body.Points ?? Array.Empty<float2>();
            var sum = float2.zero;
            for (var i = 0; i < points.Length; i++)
            {
                sum += points[i];
            }

            return points.Length > 0 ? sum / points.Length : float2.zero;
        }

        public static float3 ClampPoint(int index, float3 point, float clearance)
        {
            var clamped = ClampInside(index, new float2(point.x, point.z), clearance);
            return new float3(clamped.x, Level(index), clamped.y);
        }

        /// <summary>
        /// Returns the nearest point inside the water at least <paramref name="clearance"/>
        /// from the shoreline. Used by ship movement, rallies and dock launch points.
        /// </summary>
        public static float2 ClampInside(int index, float2 point, float clearance)
        {
            var body = Body(index);
            if (body == null)
            {
                return point;
            }

            var signed = SignedDistance(body, point);
            if (signed <= -clearance)
            {
                return point;
            }

            if (body.Kind == WaterShapeKind.Circle)
            {
                var direction = math.normalizesafe(point - body.Centre, new float2(1f, 0f));
                var radius = math.max(0f, body.Radius - clearance);
                return body.Centre + direction * radius;
            }

            ClosestOnPolygon(body.Points, point, out var closest, out var segment);
            var inward = InwardNormal(body, closest, segment);
            var candidate = closest + inward * clearance;
            if (Contains(body, candidate))
            {
                return candidate;
            }

            for (var reduced = clearance * 0.5f; reduced > 0.25f; reduced *= 0.5f)
            {
                candidate = closest + inward * reduced;
                if (Contains(body, candidate))
                {
                    return candidate;
                }
            }

            var centre = Centre(index);
            return Contains(body, centre) ? centre : closest;
        }

        /// <summary>
        /// Finds the closest shoreline where a dock can sit on land. The launch point is
        /// pushed into the water by <paramref name="waterClearance"/>.
        /// </summary>
        public static bool TryFindDockSite(
            float2 site,
            float maxShoreDistance,
            float waterClearance,
            out int bodyIndex,
            out float3 spawnPoint)
        {
            var bodies = Bodies;
            var bestDistance = float.MaxValue;
            bodyIndex = -1;
            spawnPoint = default;

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                var signed = SignedDistance(body, site);
                if (signed <= 1f || signed > maxShoreDistance || signed >= bestDistance)
                {
                    continue;
                }

                bestDistance = signed;
                bodyIndex = i;
                var inside = ClampInside(i, site, waterClearance);
                spawnPoint = new float3(inside.x, body.Level, inside.y);
            }

            return bodyIndex >= 0;
        }

        public static bool TryGetSpawn(int bodyIndex, float2 near, float clearance, out float3 spawnPoint)
        {
            var body = Body(bodyIndex);
            if (body == null)
            {
                spawnPoint = default;
                return false;
            }

            var inside = ClampInside(bodyIndex, near, clearance);
            spawnPoint = new float3(inside.x, body.Level, inside.y);
            return true;
        }

        private static bool Contains(WaterBody body, float2 point)
        {
            if (body.Kind == WaterShapeKind.Circle)
            {
                return math.distancesq(point, body.Centre) < body.Radius * body.Radius;
            }

            return PointInPolygon(body.Points, point);
        }

        private static float SignedDistance(WaterBody body, float2 point)
        {
            if (body.Kind == WaterShapeKind.Circle)
            {
                return math.distance(point, body.Centre) - body.Radius;
            }

            ClosestOnPolygon(body.Points, point, out var closest, out _);
            var distance = math.distance(point, closest);
            return PointInPolygon(body.Points, point) ? -distance : distance;
        }

        private static float2 InwardNormal(WaterBody body, float2 point, int segment)
        {
            var points = body.Points;
            if (points == null || points.Length < 2)
            {
                return float2.zero;
            }

            var next = (segment + 1) % points.Length;
            var edge = points[next] - points[segment];
            var normal = math.normalizesafe(new float2(-edge.y, edge.x), new float2(1f, 0f));

            if (Contains(body, point + normal * 0.5f))
            {
                return normal;
            }

            return -normal;
        }

        private static bool PointInPolygon(float2[] points, float2 point)
        {
            if (points == null || points.Length < 3)
            {
                return false;
            }

            var inside = false;
            for (var i = 0; i < points.Length; i++)
            {
                var j = (i + points.Length - 1) % points.Length;
                var pi = points[i];
                var pj = points[j];

                if ((pi.y > point.y) != (pj.y > point.y) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) /
                    (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static void ClosestOnPolygon(
            float2[] points,
            float2 point,
            out float2 closest,
            out int segment)
        {
            closest = point;
            segment = 0;

            if (points == null || points.Length < 2)
            {
                return;
            }

            var best = float.MaxValue;
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                var ab = b - a;
                var lengthSq = math.dot(ab, ab);
                var t = lengthSq > 0f
                    ? math.saturate(math.dot(point - a, ab) / lengthSq)
                    : 0f;
                var candidate = a + ab * t;
                var distanceSq = math.distancesq(candidate, point);

                if (distanceSq < best)
                {
                    best = distanceSq;
                    closest = candidate;
                    segment = i;
                }
            }
        }
    }
}
