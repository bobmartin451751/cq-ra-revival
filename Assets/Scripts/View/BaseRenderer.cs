using CQRa.Sim.Base;
using CQRa.View;
using UnityEngine;

namespace CQRa.View
{
    /// <summary>
    /// Builds the shared building and ore meshes/materials at scene start and hands them
    /// to <see cref="BuildingFactory"/>. Code-first: no imported art, no prefabs.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public sealed class BaseRenderer : MonoBehaviour
    {
        private Mesh[] _meshes;
        private Material[] _materials;

        private void Awake()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[BaseRenderer] URP Lit shader not found");
                return;
            }

            _meshes = new[]
            {
                ProceduralMeshes.ConstructionYard(),
                ProceduralMeshes.PowerPlant(),
                ProceduralMeshes.Refinery(),
                ProceduralMeshes.Barracks(),
                ProceduralMeshes.WarFactory(),
                ProceduralMeshes.GunTower(),
                ProceduralMeshes.Wall(),
                ProceduralMeshes.OreCluster(17),
                ProceduralMeshes.Crate(),
            };

            _materials = new Material[BuildingFactory.MaterialCount];
            var structure = new Color(0.36f, 0.35f, 0.31f);
            var teamColors = new[]
            {
                new Color(0.62f, 0.18f, 0.12f),
                new Color(0.24f, 0.40f, 0.52f),
            };

            for (var team = 0; team < 2; team++)
            {
                for (var kind = 0; kind < BuildingFactory.BuildingKindCount; kind++)
                {
                    var material = new Material(shader)
                    {
                        name = $"Building_T{team}_{(BuildingKind)kind}",
                    };

                    var tint = kind switch
                    {
                        (int)BuildingKind.PowerPlant => new Color(0.34f, 0.38f, 0.36f),
                        (int)BuildingKind.Barracks => new Color(0.40f, 0.36f, 0.28f),
                        (int)BuildingKind.WarFactory => new Color(0.30f, 0.32f, 0.33f),
                        (int)BuildingKind.GunTower => new Color(0.28f, 0.30f, 0.31f),
                        (int)BuildingKind.Wall => new Color(0.44f, 0.42f, 0.38f),
                        _ => structure,
                    };

                    material.SetColor("_BaseColor", Color.Lerp(tint, teamColors[team], 0.45f));
                    material.SetFloat("_Smoothness", 0.22f);
                    material.SetFloat("_Metallic", 0.25f);

                    _materials[team * BuildingFactory.BuildingKindCount + kind] = material;
                }
            }

            var ore = new Material(shader) { name = "Ore" };
            ore.SetColor("_BaseColor", new Color(0.72f, 0.55f, 0.16f));
            ore.SetFloat("_Smoothness", 0.55f);
            ore.SetFloat("_Metallic", 0.35f);
            ore.EnableKeyword("_EMISSION");
            ore.SetColor("_EmissionColor", new Color(0.16f, 0.10f, 0.01f));
            _materials[BuildingFactory.OreMaterialIndex] = ore;

            var crate = new Material(shader) { name = "Crate" };
            crate.SetColor("_BaseColor", new Color(0.46f, 0.38f, 0.22f));
            crate.SetFloat("_Smoothness", 0.30f);
            crate.SetFloat("_Metallic", 0.20f);
            _materials[BuildingFactory.CrateMaterialIndex] = crate;

            var highlight = new Material(shader) { name = "Building_Highlight" };
            highlight.SetColor("_BaseColor", new Color(0.98f, 0.94f, 0.74f));
            highlight.SetFloat("_Smoothness", 0.55f);
            highlight.SetFloat("_Metallic", 0.05f);
            highlight.EnableKeyword("_EMISSION");
            highlight.SetColor("_EmissionColor", new Color(0.34f, 0.28f, 0.12f));
            _materials[BuildingFactory.HighlightMaterialIndex] = highlight;

            BuildingFactory.Initialize(_materials, _meshes);
            Debug.Log($"[BaseRenderer] ready ({BuildingFactory.MaterialCount} materials, {_meshes.Length} meshes)");
        }

        private void OnDestroy()
        {
            BuildingFactory.Shutdown();

            if (_meshes != null)
            {
                foreach (var mesh in _meshes)
                {
                    if (mesh != null)
                    {
                        Destroy(mesh);
                    }
                }

                _meshes = null;
            }

            if (_materials != null)
            {
                foreach (var material in _materials)
                {
                    if (material != null)
                    {
                        Destroy(material);
                    }
                }

                _materials = null;
            }
        }
    }
}
