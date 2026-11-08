using System.Collections.Generic;
using CQRa.Sim.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.View
{
    /// <summary>
    /// Draws the player's fog as two translucent meshes: dark unexplored shroud and a
    /// lighter explored-but-not-visible layer. Rebuilt at a low rate from the fog grid.
    /// Purely presentational; the simulation owns the fog data.
    /// </summary>
    public sealed class FogRenderer : MonoBehaviour
    {
        private const float UpdateInterval = 0.12f;

        [SerializeField] private HeightmapTerrain _terrain;

        private MeshFilter _unexploredFilter;
        private MeshFilter _exploredFilter;
        private Mesh _unexplored;
        private Mesh _explored;
        private Material _unexploredMaterial;
        private Material _exploredMaterial;
        private float _nextUpdate;
        private int _lastRevision = -1;

        public void Configure(HeightmapTerrain terrain)
        {
            _terrain = terrain;
        }

        private void Awake()
        {
            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<HeightmapTerrain>();
            }

            _unexploredMaterial = CreateFogMaterial("Fog_Never", 0.92f);
            _exploredMaterial = CreateFogMaterial("Fog_Explored", 0.52f);

            _unexploredFilter = CreateLayer("FogNever", _unexploredMaterial);
            _exploredFilter = CreateLayer("FogExplored", _exploredMaterial);

            _unexplored = new Mesh { name = "FogNever" };
            _explored = new Mesh { name = "FogExplored" };
            _unexploredFilter.sharedMesh = _unexplored;
            _exploredFilter.sharedMesh = _explored;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextUpdate)
            {
                return;
            }

            _nextUpdate = Time.unscaledTime + UpdateInterval;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            if (!FogQuery.TryGet(entityManager, 0, out var fog, out var info))
            {
                return;
            }

            var revision = entityManager.GetComponentData<FogState>(fog).Revision;
            if (revision == _lastRevision)
            {
                return;
            }

            _lastRevision = revision;
            var cells = FogQuery.GetCells(entityManager, fog);
            Build(_unexplored, cells, info, FogMath.Never);
            Build(_explored, cells, info, FogMath.Explored);
        }

        private void Build(Mesh mesh, NativeArray<FogCell> cells, in FogGridInfo info, byte state)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (var z = 0; z < info.Size.y; z++)
            {
                for (var x = 0; x < info.Size.x; x++)
                {
                    if (cells[z * info.Size.x + x].Value != state)
                    {
                        continue;
                    }

                    var minX = info.Origin.x + x * info.CellSize;
                    var minZ = info.Origin.y + z * info.CellSize;
                    var maxX = minX + info.CellSize;
                    var maxZ = minZ + info.CellSize;
                    const float lift = 0.35f;

                    var start = vertices.Count;
                    vertices.Add(new Vector3(minX, HeightAt(minX, minZ) + lift, minZ));
                    vertices.Add(new Vector3(minX, HeightAt(minX, maxZ) + lift, maxZ));
                    vertices.Add(new Vector3(maxX, HeightAt(maxX, maxZ) + lift, maxZ));
                    vertices.Add(new Vector3(maxX, HeightAt(maxX, minZ) + lift, minZ));

                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 1);
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private float HeightAt(float x, float z)
        {
            return _terrain != null ? _terrain.SampleHeight(x, z) : 0f;
        }

        private static MeshFilter CreateLayer(string name, Material material)
        {
            var go = new GameObject(name);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return filter;
        }

        private static Material CreateFogMaterial(string name, float alpha)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = name };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetColor("_BaseColor", new Color(0.015f, 0.012f, 0.010f, alpha));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void OnDestroy()
        {
            if (_unexplored != null)
            {
                Destroy(_unexplored);
            }

            if (_explored != null)
            {
                Destroy(_explored);
            }

            if (_unexploredMaterial != null)
            {
                Destroy(_unexploredMaterial);
            }

            if (_exploredMaterial != null)
            {
                Destroy(_exploredMaterial);
            }
        }
    }
}
