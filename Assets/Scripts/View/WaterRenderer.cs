using System.Collections.Generic;
using CQRa.Sim.Content;
using CQRa.Sim.Bridges;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.View
{
    /// <summary>
    /// Builds one translucent water mesh per water body on the selected map, plus the
    /// neutral bridge decks that cross it. Destroyed bridges are shown as rubble.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class WaterRenderer : MonoBehaviour
    {
        private sealed class BridgeVisual
        {
            public int Index;
            public Transform Root;
            public Renderer[] Renderers;
            public bool Destroyed;
        }

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<GameObject> _instances = new List<GameObject>();
        private readonly List<BridgeVisual> _bridges = new List<BridgeVisual>();
        private Material _material;
        private Material _bridgeMaterial;
        private Material _rubbleMaterial;

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

            _bridgeMaterial = new Material(shader) { name = "BridgeDeck" };
            _bridgeMaterial.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.24f));
            _bridgeMaterial.SetFloat("_Smoothness", 0.18f);
            _bridgeMaterial.SetFloat("_Metallic", 0.10f);

            _rubbleMaterial = new Material(shader) { name = "BridgeRubble" };
            _rubbleMaterial.SetColor("_BaseColor", new Color(0.20f, 0.18f, 0.16f));
            _rubbleMaterial.SetFloat("_Smoothness", 0.08f);
            _rubbleMaterial.SetFloat("_Metallic", 0.25f);

            Build();
        }

        public void Build()
        {
            Clear();

            var bodies = MapRuntime.Current?.WaterBodies;
            if (bodies != null)
            {
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

            BuildBridges();
        }

        private void BuildBridges()
        {
            var specs = BridgeCatalog.ForCurrentMap();
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var root = new GameObject($"Bridge_{spec.Index}");
                root.transform.SetParent(transform, false);
                root.transform.position = new Vector3(spec.Centre.x, spec.Level, spec.Centre.y);
                root.transform.rotation = Quaternion.Euler(0f, spec.Yaw * Mathf.Rad2Deg, 0f);

                var deckMesh = ProceduralMeshes.Box(
                    new Vector3(spec.HalfLength * 2f, 0.5f, spec.HalfWidth * 2f));
                var deck = new GameObject("Deck");
                deck.transform.SetParent(root.transform, false);
                deck.AddComponent<MeshFilter>().sharedMesh = deckMesh;
                var deckRenderer = deck.AddComponent<MeshRenderer>();
                deckRenderer.sharedMaterial = _bridgeMaterial;
                deckRenderer.shadowCastingMode = ShadowCastingMode.On;
                deckRenderer.receiveShadows = true;

                var railMesh = ProceduralMeshes.Box(
                    new Vector3(spec.HalfLength * 2f, 1.1f, 0.45f));
                var railLeft = new GameObject("RailLeft");
                railLeft.transform.SetParent(root.transform, false);
                railLeft.transform.localPosition = new Vector3(0f, 0.8f, -spec.HalfWidth + 0.25f);
                railLeft.AddComponent<MeshFilter>().sharedMesh = railMesh;
                var railLeftRenderer = railLeft.AddComponent<MeshRenderer>();
                railLeftRenderer.sharedMaterial = _bridgeMaterial;

                var railRight = new GameObject("RailRight");
                railRight.transform.SetParent(root.transform, false);
                railRight.transform.localPosition = new Vector3(0f, 0.8f, spec.HalfWidth - 0.25f);
                railRight.AddComponent<MeshFilter>().sharedMesh = railMesh;
                var railRightRenderer = railRight.AddComponent<MeshRenderer>();
                railRightRenderer.sharedMaterial = _bridgeMaterial;

                _meshes.Add(deckMesh);
                _meshes.Add(railMesh);
                _instances.Add(root);
                _bridges.Add(new BridgeVisual
                {
                    Index = spec.Index,
                    Root = root.transform,
                    Renderers = new Renderer[] { deckRenderer, railLeftRenderer, railRightRenderer },
                    Destroyed = false,
                });
            }
        }

        private void Update()
        {
            if (_bridges.Count == 0)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            using var query = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BridgeTag>(),
                ComponentType.ReadOnly<Bridge>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var bridge = world.EntityManager.GetComponentData<Bridge>(entities[i]);
                for (var b = 0; b < _bridges.Count; b++)
                {
                    var visual = _bridges[b];
                    if (visual.Index != bridge.Index || visual.Destroyed == (bridge.Destroyed != 0))
                    {
                        continue;
                    }

                    visual.Destroyed = bridge.Destroyed != 0;
                    for (var r = 0; r < visual.Renderers.Length; r++)
                    {
                        visual.Renderers[r].sharedMaterial =
                            visual.Destroyed ? _rubbleMaterial : _bridgeMaterial;
                    }

                    visual.Root.localScale = visual.Destroyed
                        ? new Vector3(1f, 0.18f, 1f)
                        : Vector3.one;
                    visual.Root.localPosition = visual.Destroyed
                        ? new Vector3(0f, -0.9f, 0f)
                        : Vector3.zero;
                }
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
            _bridges.Clear();
        }

        private void OnDestroy()
        {
            Clear();

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }

            if (_bridgeMaterial != null)
            {
                Destroy(_bridgeMaterial);
                _bridgeMaterial = null;
            }

            if (_rubbleMaterial != null)
            {
                Destroy(_rubbleMaterial);
                _rubbleMaterial = null;
            }
        }
    }
}
