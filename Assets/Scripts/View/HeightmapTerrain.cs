using CQRa.Sim.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.View
{
    /// <summary>
    /// Procedurally generated heightmap terrain.
    ///
    /// The playable centre of the map is flattened into a basin so that bases and
    /// starting units always have level ground. Everything outside the basin rolls
    /// through fractal Perlin noise.
    ///
    /// The mesh is built at runtime in <see cref="Awake"/>; no serialised mesh assets.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class HeightmapTerrain : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private int _resolution = 129;
        [SerializeField] private float _size = 360f;
        [SerializeField] private float _maxHeight = 28f;
        [SerializeField] private float _noiseScale = 3.8f;
        [SerializeField] private int _seed = 1337;
        [SerializeField] private int _octaves = 4;
        [SerializeField] private float _lacunarity = 2.05f;
        [SerializeField] private float _persistence = 0.5f;

        [Header("Playable basin")]
        [SerializeField] private float _basinRadius = 46f;
        [SerializeField] private float _basinBlend = 16f;
        [SerializeField] private float _basinHeight = 0f;

        [Header("Rendering")]
        [SerializeField] private Material _material;
        [SerializeField] private float _detailMetresPerTile = 12f;

        private float[] _heights;
        private Mesh _mesh;
        private Texture2D _detailTexture;
        private int _res;

        public float Size => _size;
        public float MaxHeight => _maxHeight;
        public int Seed => _seed;

        /// <summary>Number of height samples per axis after generation.</summary>
        public int Resolution => _res;

        public Material Material
        {
            get => _material;
            set => _material = value;
        }

        private void Awake()
        {
            ApplySelectedMap();
            Generate();
        }

        /// <summary>Copies the selected skirmish map's terrain shape into this heightmap.</summary>
        public void ApplySelectedMap()
        {
            var map = MapRuntime.Current;
            if (map == null)
            {
                return;
            }

            _seed = map.Seed;
            _maxHeight = map.MaxHeight;
            _noiseScale = map.NoiseScale;
            _basinRadius = map.BasinRadius;
            _basinBlend = map.BasinBlend;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }

            if (_detailTexture != null)
            {
                Destroy(_detailTexture);
                _detailTexture = null;
            }
        }

        /// <summary>Builds the height cache and the render mesh. Safe to call more than once.</summary>
        public void Generate()
        {
            _res = Mathf.Max(2, _resolution);
            ComputeHeights();
            BuildMesh();
        }

        /// <summary>World-space height at the given XZ coordinate, clamped to the map edge.</summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            if (_heights == null)
            {
                return 0f;
            }

            var fx = Mathf.Clamp01(worldX / _size + 0.5f) * (_res - 1);
            var fz = Mathf.Clamp01(worldZ / _size + 0.5f) * (_res - 1);

            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var x1 = Mathf.Min(x0 + 1, _res - 1);
            var z1 = Mathf.Min(z0 + 1, _res - 1);
            var tx = fx - x0;
            var tz = fz - z0;

            var h00 = _heights[z0 * _res + x0];
            var h10 = _heights[z0 * _res + x1];
            var h01 = _heights[z1 * _res + x0];
            var h11 = _heights[z1 * _res + x1];

            var a = Mathf.Lerp(h00, h10, tx);
            var b = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(a, b, tz);
        }

        /// <summary>Raw height sample (no interpolation). Used to hand terrain data to the sim.</summary>
        public float GridHeight(int x, int z)
        {
            if (_heights == null || _res < 2)
            {
                return 0f;
            }

            x = Mathf.Clamp(x, 0, _res - 1);
            z = Mathf.Clamp(z, 0, _res - 1);
            return _heights[z * _res + x];
        }

        /// <summary>Approximate surface normal from finite differences.</summary>
        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            const float e = 1.5f;
            var hl = SampleHeight(worldX - e, worldZ);
            var hr = SampleHeight(worldX + e, worldZ);
            var hd = SampleHeight(worldX, worldZ - e);
            var hu = SampleHeight(worldX, worldZ + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        public bool Contains(float worldX, float worldZ)
        {
            var half = _size * 0.5f;
            return worldX >= -half && worldX <= half && worldZ >= -half && worldZ <= half;
        }

        private void ComputeHeights()
        {
            _heights = new float[_res * _res];

            var offsetX = (_seed % 997) * 0.173f + 13.7f;
            var offsetZ = ((_seed / 7) % 991) * 0.291f + 41.3f;
            var baseFrequency = _noiseScale / _size;

            for (var z = 0; z < _res; z++)
            {
                var nz = (float)z / (_res - 1);
                var wz = (nz - 0.5f) * _size;

                for (var x = 0; x < _res; x++)
                {
                    var nx = (float)x / (_res - 1);
                    var wx = (nx - 0.5f) * _size;

                    var amplitude = 1f;
                    var frequency = baseFrequency;
                    var sum = 0f;
                    var weight = 0f;

                    for (var o = 0; o < _octaves; o++)
                    {
                        sum += Mathf.PerlinNoise(wx * frequency + offsetX, wz * frequency + offsetZ) * amplitude;
                        weight += amplitude;
                        amplitude *= _persistence;
                        frequency *= _lacunarity;
                    }

                    var h = weight > 0f ? sum / weight : 0f;
                    h = Mathf.Pow(Mathf.Clamp01(h), 1.4f) * _maxHeight;

                    // Flatten the centre into a buildable basin, then blend into the hills.
                    var distance = Mathf.Sqrt(wx * wx + wz * wz);
                    var blend = Mathf.Clamp01((distance - _basinRadius) / _basinBlend);
                    blend = Mathf.SmoothStep(0f, 1f, blend);
                    h = Mathf.Lerp(_basinHeight, h, blend);

                    // Gentle low-frequency undulation keeps the buildable basin from
                    // reading as a perfectly flat disc, while staying easy to build on.
                    if (blend < 1f)
                    {
                        var undulation = (Mathf.PerlinNoise(wx * 0.02f + 5.3f, wz * 0.02f + 9.1f) - 0.5f) * 2.4f;
                        h += undulation * (1f - blend);
                    }

                    // Carve the naval lake on the western edge of the basin.
                    var lakeDistance = Mathf.Sqrt(
                        (wx + 100f) * (wx + 100f) + wz * wz);
                    if (lakeDistance < 36f)
                    {
                        var lakeBlend = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.Clamp01((lakeDistance - 26f) / 10f));
                        h = Mathf.Lerp(-3f, h, lakeBlend);
                    }

                    _heights[z * _res + x] = h;
                }
            }
        }

        private void BuildMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "HeightmapTerrain" };
            }

            _mesh.Clear();
            _mesh.indexFormat = _res * _res > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            var vertexCount = _res * _res;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var step = _size / (_res - 1);
            var half = _size * 0.5f;

            for (var z = 0; z < _res; z++)
            {
                for (var x = 0; x < _res; x++)
                {
                    var i = z * _res + x;
                    var px = x * step - half;
                    var pz = z * step - half;
                    vertices[i] = new Vector3(px, _heights[i], pz);
                    uvs[i] = new Vector2(px / _detailMetresPerTile, pz / _detailMetresPerTile);
                }
            }

            var triangles = new int[(_res - 1) * (_res - 1) * 6];
            var t = 0;
            for (var z = 0; z < _res - 1; z++)
            {
                for (var x = 0; x < _res - 1; x++)
                {
                    var i0 = z * _res + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + _res;
                    var i3 = i2 + 1;

                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;

                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            var filter = GetComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            var renderer = GetComponent<MeshRenderer>();
            if (_material != null)
            {
                renderer.sharedMaterial = _material;
            }

            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            ApplyDetailTexture(renderer);
        }

        /// <summary>
        /// Generates a tiling noise texture so the ground reads as dry industrial
        /// earth rather than flat brown plastic.
        /// </summary>
        private void ApplyDetailTexture(MeshRenderer renderer)
        {
            // Instantiating a material in edit mode logs an error and leaks it.
            if (!Application.isPlaying)
            {
                return;
            }

            if (_detailTexture == null)
            {
                _detailTexture = BuildDetailTexture(128, _seed);
            }

            var material = renderer.material;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", _detailTexture);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.06f);
            }
        }

        private static Texture2D BuildDetailTexture(int resolution, int seed)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false)
            {
                name = "TerrainDetail",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var ox = (seed % 431) * 0.37f + 7.1f;
            var oz = ((seed / 11) % 419) * 0.53f + 21.9f;

            var dark = new Color(0.36f, 0.30f, 0.23f);
            var mid = new Color(0.41f, 0.34f, 0.26f);
            var light = new Color(0.46f, 0.39f, 0.30f);

            var pixels = new Color[resolution * resolution];

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var u = (float)x / resolution;
                    var v = (float)y / resolution;

                    var coarse = Mathf.PerlinNoise(u * 4.0f + ox, v * 4.0f + oz);
                    var fine = Mathf.PerlinNoise(u * 29.0f + ox * 1.7f, v * 29.0f + oz * 1.3f);

                    var n = Mathf.Clamp01(coarse * 0.40f + fine * 0.60f);
                    var color = Color.Lerp(dark, mid, n);

                    if (n > 0.62f)
                    {
                        color = Color.Lerp(mid, light, (n - 0.62f) / 0.38f);
                    }

                    // Sparse tar-like flecks.
                    var fleck = Mathf.PerlinNoise(u * 53.0f + ox * 2.3f, v * 53.0f + oz * 2.1f);
                    if (fleck > 0.90f)
                    {
                        color *= 0.93f;
                    }

                    pixels[y * resolution + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
