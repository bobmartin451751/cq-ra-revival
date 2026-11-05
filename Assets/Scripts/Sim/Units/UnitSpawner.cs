using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using CQRa.View;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Phase 2 runtime spawner. Fills the basin with two opposing groups so the
    /// camera and selection systems have something real to work with.
    /// Replaced by map-driven spawning in Phase 9.
    /// </summary>
    public sealed class UnitSpawner : MonoBehaviour
    {
        [SerializeField] private HeightmapTerrain _terrain;
        [SerializeField] private Material _unitTemplate;
        [SerializeField] private bool _spawnOnStart = true;
        [SerializeField] private int _tanksPerTeam = 4;
        [SerializeField] private int _infantryPerTeam = 6;
        [SerializeField] private int _harvestersPerTeam = 1;
        [SerializeField] private float _spacing = 8f;

        private Mesh[] _meshes;
        private Material[] _materials;
        private RenderMeshArray _renderMeshArray;

        public int SpawnedCount { get; private set; }
        public int TeamCount => 2;

        public void Configure(HeightmapTerrain terrain, Material unitTemplate)
        {
            _terrain = terrain;
            _unitTemplate = unitTemplate;
        }

        private void Start()
        {
            if (_spawnOnStart)
            {
                Spawn();
            }
        }

        private void OnDestroy()
        {
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

        public void Spawn()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[UnitSpawner] no default ECS world; skipping spawn");
                return;
            }

            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<HeightmapTerrain>();
            }

            if (!BuildMaterials())
            {
                return;
            }

            _meshes = new[]
            {
                ProceduralMeshes.Infantry(),
                ProceduralMeshes.Tank(),
                ProceduralMeshes.Harvester(),
            };

            _renderMeshArray = UnitFactory.CreateRenderMeshArray(_materials, _meshes);

            var entityManager = world.EntityManager;
            SpawnedCount = 0;

            for (var team = 0; team < TeamCount; team++)
            {
                var direction = team == 0 ? 1f : -1f;
                var teamZ = direction * 42f;
                var yaw = team == 0 ? 0f : Mathf.PI;
                var index = 0;

                index = SpawnKind(entityManager, team, UnitKind.Harvester, _harvestersPerTeam, index, teamZ, yaw);
                index = SpawnKind(entityManager, team, UnitKind.Tank, _tanksPerTeam, index, teamZ, yaw);
                SpawnKind(entityManager, team, UnitKind.Infantry, _infantryPerTeam, index, teamZ, yaw);
            }

            Debug.Log($"[UnitSpawner] spawned {SpawnedCount} units across {TeamCount} teams");
        }

        private int SpawnKind(
            EntityManager entityManager,
            int team,
            UnitKind kind,
            int count,
            int slot,
            float teamZ,
            float yaw)
        {
            for (var i = 0; i < count; i++)
            {
                var column = slot % 6;
                var row = slot / 6;
                var x = (column - 2.5f) * _spacing;
                var z = teamZ - Mathf.Sign(teamZ) * row * _spacing;

                if (_terrain != null)
                {
                    z = Mathf.Clamp(z, -_terrain.Size * 0.5f + 6f, _terrain.Size * 0.5f - 6f);
                }

                var y = _terrain != null ? _terrain.SampleHeight(x, z) : 0f;
                var meshIndex = (int)kind;
                var materialIndex = UnitMaterials.BaseIndex(team, kind);

                UnitFactory.Spawn(
                    entityManager,
                    _renderMeshArray,
                    meshIndex,
                    materialIndex,
                    new float3(x, y, z),
                    yaw,
                    kind,
                    team);

                SpawnedCount++;
                slot++;
            }

            return slot;
        }

        private bool BuildMaterials()
        {
            var shader = _unitTemplate != null
                ? _unitTemplate.shader
                : Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                Debug.LogError("[UnitSpawner] URP Lit shader not found");
                return false;
            }

            _materials = new Material[UnitMaterials.Count];

            for (var team = 0; team < UnitMaterials.TeamCount; team++)
            {
                for (var kind = 0; kind < UnitMaterials.KindCount; kind++)
                {
                    var material = new Material(shader)
                    {
                        name = $"Unit_T{team}_{(UnitKind)kind}",
                    };

                    material.SetColor("_BaseColor", ColorFor((UnitKind)kind, team));
                    material.SetFloat("_Smoothness", 0.30f);
                    material.SetFloat("_Metallic", 0.18f);

                    _materials[UnitMaterials.BaseIndex(team, (UnitKind)kind)] = material;
                }
            }

            var highlight = new Material(shader)
            {
                name = "Unit_Highlight",
            };
            highlight.SetColor("_BaseColor", new Color(0.98f, 0.94f, 0.74f));
            highlight.SetFloat("_Smoothness", 0.55f);
            highlight.SetFloat("_Metallic", 0.05f);
            highlight.EnableKeyword("_EMISSION");
            highlight.SetColor("_EmissionColor", new Color(0.34f, 0.28f, 0.12f));
            _materials[UnitMaterials.HighlightIndex] = highlight;

            return true;
        }

        private static Color ColorFor(UnitKind kind, int team)
        {
            var kindColor = kind switch
            {
                UnitKind.Tank => new Color(0.32f, 0.33f, 0.34f),
                UnitKind.Harvester => new Color(0.58f, 0.44f, 0.20f),
                _ => new Color(0.43f, 0.27f, 0.18f),
            };

            var teamColor = team == 0
                ? new Color(0.62f, 0.18f, 0.12f)
                : new Color(0.24f, 0.40f, 0.52f);

            return Color.Lerp(kindColor, teamColor, 0.60f);
        }
    }
}
