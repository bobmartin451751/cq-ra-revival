using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.View
{
    [Serializable]
    public struct ObstacleSpec
    {
        public Vector2 Centre;
        public float Radius;
        public float Height;
        public int Seed;
    }

    /// <summary>
    /// Hand-placed ruined outcrops. They break sight lines, give the pathfinder
    /// something to route around, and anchor the post-collapse industrial look.
    /// Only the circular footprint is gameplay-relevant; the visuals are procedural.
    /// </summary>
    public sealed class Scenery : MonoBehaviour
    {
        [SerializeField] private ObstacleSpec[] _obstacles = Array.Empty<ObstacleSpec>();
        [SerializeField] private Material _template;

        private readonly List<GameObject> _instances = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();

        public ObstacleSpec[] Obstacles => _obstacles;

        public void Configure(ObstacleSpec[] obstacles, Material template)
        {
            _obstacles = obstacles ?? Array.Empty<ObstacleSpec>();
            _template = template;
        }

        private void Awake()
        {
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            if (_obstacles.Length == 0)
            {
                return;
            }

            var shader = _template != null
                ? _template.shader
                : Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                return;
            }

            var terrain = FindFirstObjectByType<HeightmapTerrain>();

            for (var i = 0; i < _obstacles.Length; i++)
            {
                var spec = _obstacles[i];
                var x = spec.Centre.x;
                var z = spec.Centre.y;
                var y = terrain != null ? terrain.SampleHeight(x, z) : 0f;

                var mesh = ProceduralMeshes.RockCluster(spec.Radius, spec.Height, spec.Seed);
                var material = new Material(shader)
                {
                    name = $"Scenery_Ruin_{i}",
                };
                material.SetColor("_BaseColor", new Color(0.30f, 0.27f, 0.23f));
                material.SetFloat("_Smoothness", 0.10f);
                material.SetFloat("_Metallic", 0.22f);

                var go = new GameObject($"Ruin_{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(x, y - 0.35f, z);

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                _instances.Add(go);
                _meshes.Add(mesh);
                _materials.Add(material);

                CreateDebris(terrain, x, z, spec, shader, i);
            }
        }

        /// <summary>Small non-blocking rubble so the footprint does not read as a disc.</summary>
        private void CreateDebris(
            HeightmapTerrain terrain,
            float x,
            float z,
            ObstacleSpec spec,
            Shader shader,
            int index)
        {
            var random = new System.Random(spec.Seed * 977 + 13);

            for (var d = 0; d < 7; d++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var distance = spec.Radius * (0.75f + 0.55f * (float)random.NextDouble());
                var size = spec.Radius * (0.10f + 0.12f * (float)random.NextDouble());

                var px = x + Mathf.Cos(angle) * distance;
                var pz = z + Mathf.Sin(angle) * distance;
                var py = terrain != null ? terrain.SampleHeight(px, pz) : 0f;

                var mesh = ProceduralMeshes.Box(new Vector3(
                    size,
                    size * (0.45f + (float)random.NextDouble() * 0.5f),
                    size * (0.6f + (float)random.NextDouble() * 0.6f)));

                var material = new Material(shader)
                {
                    name = $"Scenery_Debris_{index}_{d}",
                };
                material.SetColor("_BaseColor", new Color(0.26f, 0.24f, 0.21f));
                material.SetFloat("_Smoothness", 0.08f);
                material.SetFloat("_Metallic", 0.18f);

                var go = new GameObject($"Debris_{index}_{d}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(px, py + size * 0.2f, pz);
                go.transform.rotation = Quaternion.Euler(
                    (float)random.NextDouble() * 8f,
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 8f);

                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                _instances.Add(go);
                _meshes.Add(mesh);
                _materials.Add(material);
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            foreach (var material in _materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _meshes.Clear();
            _materials.Clear();
            _instances.Clear();
        }
    }
}
