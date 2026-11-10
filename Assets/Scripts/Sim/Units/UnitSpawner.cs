using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using CQRa.Sim.Factions;
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
        [SerializeField] private bool _spawnOnStart;
        [SerializeField] private int _tanksPerTeam = 4;
        [SerializeField] private int _infantryPerTeam = 6;
        [SerializeField] private int _harvestersPerTeam = 1;
        [SerializeField] private int _mcvsPerTeam = 1;
        [SerializeField] private float _spacing = 8f;

        private Mesh[] _meshes;
        private Material[] _materials;
        private RenderMeshArray _renderMeshArray;

        public int SpawnedCount { get; private set; }
        public int TeamCount => 2;
        public bool SpawnOnStart
        {
            get => _spawnOnStart;
            set => _spawnOnStart = value;
        }

        public void Configure(HeightmapTerrain terrain, Material unitTemplate)
        {
            _terrain = terrain;
            _unitTemplate = unitTemplate;
        }

        private void Start()
        {
            if (SpawnOnStart)
            {
                Spawn();
            }
        }

        private void OnDestroy()
        {
            UnitFactory.ClearDefaultVisuals();

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
                ProceduralMeshes.Mcv(),
                ProceduralMeshes.Gunship(),
                ProceduralMeshes.Engineer(),
                ProceduralMeshes.Thumper(),
                ProceduralMeshes.Mantis(),
                ProceduralMeshes.Skyfall(),
                ProceduralMeshes.Rig(),
            };

            _renderMeshArray = UnitFactory.CreateRenderMeshArray(_materials, _meshes);
            UnitFactory.SetDefaultVisuals(_renderMeshArray);

            var entityManager = world.EntityManager;
            SpawnedCount = 0;

            for (var team = 0; team < TeamCount; team++)
            {
                var direction = team == 0 ? 1f : -1f;
                var teamZ = direction * 32f;
                var yaw = team == 0 ? 0f : Mathf.PI;
                var index = 0;

                index = SpawnKind(entityManager, team, UnitKind.Harvester, _harvestersPerTeam, index, teamZ, yaw);
                index = SpawnKind(entityManager, team, UnitKind.Mcv, _mcvsPerTeam, index, teamZ, yaw);
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
                UnitKind.Mcv => new Color(0.42f, 0.42f, 0.28f),
                UnitKind.Gunship => new Color(0.34f, 0.36f, 0.40f),
                UnitKind.Engineer => new Color(0.55f, 0.50f, 0.28f),
                UnitKind.Thumper => new Color(0.46f, 0.36f, 0.24f),
                UnitKind.Mantis => new Color(0.30f, 0.45f, 0.26f),
                UnitKind.Skyfall => new Color(0.30f, 0.34f, 0.44f),
                UnitKind.Rig => new Color(0.50f, 0.44f, 0.24f),
                _ => new Color(0.43f, 0.27f, 0.18f),
            };

            var faction = FactionRuntime.ForTeam(team);
            var teamColor = faction switch
            {
                FactionId.Verdant => new Color(0.28f, 0.50f, 0.24f),
                FactionId.Meridian => new Color(0.20f, 0.42f, 0.55f),
                _ => new Color(0.64f, 0.22f, 0.14f),
            };

            return Color.Lerp(kindColor, teamColor, 0.60f);
        }
    }
}
