using System.Collections.Generic;
using CQRa.Sim.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.View
{
    /// <summary>
    /// Builds one translucent water mesh per water body on the selected map.
    /// Circles use a shallow cylinder; polygons use a triangulated flat mesh.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class WaterRenderer : MonoBehaviour
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<GameObject> _instances = new List<GameObject>();
        private Material _material;

        private void Awake()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[WaterRenderer] URP Lit shader not found");
                return;
            }

            _material = new Material(shader) { name = "Water" };
            _material.SetFloat("_Surface", 1f);
            _material.SetFloat("_Blend", 0f);
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_ZWrite", 0f);
            _material.SetFloat("_Cull", 0f);
            _material.SetColor("_BaseColor", new Color(0.10f, 0.28f, 0.34f, 0.72f));
            _material.SetFloat("_Smoothness", 0.80f);
            _material.SetFloat("_Metallic", 0.10f);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.renderQueue = (int)RenderQueue.Transparent;

            Build();
        }

        public void Build()
        {
            Clear();

            var bodies = MapRuntime.Current?.WaterBodies;
            if (bodies == null)
            {
                return;
            }

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                Mesh mesh;

                if (body.Kind == WaterShapeKind.Circle)
                {
                    mesh = ProceduralMeshes.Cylinder(body.Radius, 0.08f, 48);
                }
                else
                {
                    mesh = ProceduralMeshes.FlatPolygon(body.Points, body.Level);
                }

                var go = new GameObject($"Water_{body.Name}");
                go.transform.SetParent(transform, false);
                if (body.Kind == WaterShapeKind.Circle)
                {
                    go.transform.position = new Vector3(
                        body.Centre.x,
                        body.Level,
                        body.Centre.y);
                }

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _instances.Add(go);
                _meshes.Add(mesh);
            }
        }

        private void Clear()
        {
            for (var i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    Destroy(_instances[i]);
                }
            }

            _instances.Clear();

            for (var i = 0; i < _meshes.Count; i++)
            {
                if (_meshes[i] != null)
                {
                    Destroy(_meshes[i]);
                }
            }

            _meshes.Clear();
        }

        private void OnDestroy()
        {
            Clear();

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }
    }
}
