using System.Collections.Generic;
using CQRa.Sim.Content;
using Unity.Mathematics;
using UnityEngine;

namespace CQRa.View
{
    /// <summary>Small top-down map cards generated from the map data.</summary>
    public static class MapPreviewCache
    {
        private const int Resolution = 128;
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<string, Texture2D> CustomCache =
            new Dictionary<string, Texture2D>();

        public static Texture2D Get(int mapIndex)
        {
            if (Cache.TryGetValue(mapIndex, out var texture))
            {
                return texture;
            }

            var map = MapCatalog.Get(mapIndex);
            if (map == null)
            {
                return null;
            }

            texture = Build(map);
            Cache[mapIndex] = texture;
            return texture;
        }

        public static Texture2D GetCustom(MapDefinition map)
        {
            if (map == null)
            {
                return null;
            }

            var key = $"{map.Seed}_{map.SizeTier}_{map.WaterBodies?.Length ?? 0}";
            if (CustomCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var texture = Build(map);
            CustomCache[key] = texture;
            return texture;
        }

        private static Texture2D Build(MapDefinition map)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = $"MapPreview_{map.Id}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color[Resolution * Resolution];
            var heights = new float[Resolution * Resolution];
            var half = map.WorldSize * 0.5f;
            var low = new Color(0.36f, 0.30f, 0.23f);
            var high = new Color(0.55f, 0.42f, 0.28f);
            var seaDeep = new Color(0.10f, 0.26f, 0.34f);
            var seaShallow = new Color(0.20f, 0.42f, 0.48f);

            for (var py = 0; py < Resolution; py++)
            {
                for (var px = 0; px < Resolution; px++)
                {
                    var wx = (px + 0.5f) / Resolution * map.WorldSize - half;
                    var wz = (py + 0.5f) / Resolution * map.WorldSize - half;
                    heights[py * Resolution + px] = SampleHeight(map, wx, wz);
                }
            }

            for (var py = 0; py < Resolution; py++)
            {
                for (var px = 0; px < Resolution; px++)
                {
                    var wx = (px + 0.5f) / Resolution * map.WorldSize - half;
                    var wz = (py + 0.5f) / Resolution * map.WorldSize - half;
                    var point = new float2(wx, wz);
                    var index = py * Resolution + px;

                    if (ContainsWater(map.WaterBodies, point))
                    {
                        var depth = Mathf.Clamp01((0.55f - heights[index]) / 6f);
                        pixels[index] = Color.Lerp(seaShallow, seaDeep, depth);
                        continue;
                    }

                    var h = heights[index];
                    var left = heights[py * Resolution + Mathf.Max(0, px - 1)];
                    var right = heights[py * Resolution + Mathf.Min(Resolution - 1, px + 1)];
                    var down = heights[Mathf.Max(0, py - 1) * Resolution + px];
                    var up = heights[Mathf.Min(Resolution - 1, py + 1) * Resolution + px];
                    var normalX = (left - right) * 0.5f;
                    var normalZ = (down - up) * 0.5f;
                    var shade = Mathf.Clamp(
                        0.55f + normalX * 0.18f + normalZ * 0.10f, 0.30f, 0.95f);

                    var relief = Mathf.Clamp01(h / Mathf.Max(1f, map.MaxHeight));
                    var color = Color.Lerp(low, high, relief) * shade;
                    color.a = 1f;
                    pixels[index] = color;
                }
            }

            if (map.Obstacles != null)
            {
                for (var i = 0; i < map.Obstacles.Length; i++)
                {
                    DrawCircle(
                        pixels,
                        map.WorldSize,
                        map.Obstacles[i].Centre,
                        map.Obstacles[i].Radius,
                        new Color(0.22f, 0.20f, 0.18f));
                }
            }

            if (map.OrePositions != null)
            {
                for (var i = 0; i < map.OrePositions.Length; i++)
                {
                    DrawDot(pixels, map.WorldSize, map.OrePositions[i], new Color(0.85f, 0.64f, 0.16f));
                }
            }

            if (map.Spawns != null)
            {
                for (var i = 0; i < map.Spawns.Length; i++)
                {
                    DrawDot(
                        pixels,
                        map.WorldSize,
                        map.Spawns[i].Position,
                        CQRa.Sim.Units.TeamPalette.For(i));
                }
            }
            else
            {
                DrawDot(pixels, map.WorldSize, map.PlayerStart, CQRa.Sim.Units.TeamPalette.For(0));
                DrawDot(pixels, map.WorldSize, map.AiStart, CQRa.Sim.Units.TeamPalette.For(1));
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float SampleHeight(MapDefinition map, float wx, float wz)
        {
            var offsetX = (map.Seed % 997) * 0.173f + 13.7f;
            var offsetZ = ((map.Seed / 7) % 991) * 0.291f + 41.3f;
            var baseFrequency = map.NoiseScale / Mathf.Max(1f, map.WorldSize);
            var amplitude = 1f;
            var frequency = baseFrequency;
            var sum = 0f;
            var weight = 0f;

            for (var o = 0; o < 4; o++)
            {
                sum += Mathf.PerlinNoise(wx * frequency + offsetX, wz * frequency + offsetZ) * amplitude;
                weight += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.05f;
            }

            var h = weight > 0f ? sum / weight : 0f;
            h = Mathf.Pow(Mathf.Clamp01(h), 1.4f) * map.MaxHeight;

            var distance = Mathf.Sqrt(wx * wx + wz * wz);
            var blend = Mathf.Clamp01((distance - map.BasinRadius) / Mathf.Max(1f, map.BasinBlend));
            blend = Mathf.SmoothStep(0f, 1f, blend);
            h = Mathf.Lerp(0f, h, blend);

            if (blend < 1f)
            {
                var undulation = (Mathf.PerlinNoise(wx * 0.02f + 5.3f, wz * 0.02f + 9.1f) - 0.5f) * 2.4f;
                h += undulation * (1f - blend);
            }

            return h;
        }

        private static void DrawCircle(
            Color[] pixels,
            float worldSize,
            float2 centre,
            float worldRadius,
            Color color)
        {
            var half = worldSize * 0.5f;
            var radius = worldRadius / worldSize * Resolution;
            var cx = (centre.x + half) / worldSize * Resolution;
            var cy = (centre.y + half) / worldSize * Resolution;
            var r = Mathf.Max(1f, radius);

            for (var y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
            {
                for (var x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    if (x < 0 || y < 0 || x >= Resolution || y >= Resolution)
                    {
                        continue;
                    }

                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r)
                    {
                        continue;
                    }

                    pixels[y * Resolution + x] = color;
                }
            }
        }

        private static void DrawDot(Color[] pixels, float worldSize, float2 point, Color color)
        {
            DrawCircle(pixels, worldSize, point, worldSize * 0.012f, color);
        }

        private static bool ContainsWater(WaterBody[] bodies, float2 point)
        {
            if (bodies == null)
            {
                return false;
            }

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body.Kind == WaterShapeKind.Circle)
                {
                    if (math.distancesq(point, body.Centre) < body.Radius * body.Radius)
                    {
                        return true;
                    }

                    continue;
                }

                if (PointInPolygon(body.Points, point))
                {
                    return true;
                }
            }

            return false;
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
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
