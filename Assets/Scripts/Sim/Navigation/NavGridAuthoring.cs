using CQRa.View;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CQRa.Sim.Navigation
{
    /// <summary>
    /// Builds the deterministic navigation data at scene start: the simulation-owned
    /// height buffer, the walkability grid and its circular blockers.
    ///
    /// Runs before unit spawning (execution order) and is idempotent, so PlayMode
    /// tests can create their own grid without fighting the scene one.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class NavGridAuthoring : MonoBehaviour
    {
        [SerializeField] private HeightmapTerrain _terrain;
        [SerializeField] private Scenery _scenery;

        [Header("Grid")]
        [SerializeField] private float _cellSize = 2f;
        [SerializeField] private float _maxSlope = 0.5f;
        [SerializeField] private float _edgeMargin = 10f;
        [SerializeField] private float _obstaclePadding = 2.5f;

        public bool HasGrid { get; private set; }
        public NavGridInfo GridInfo { get; private set; }

        public void Configure(HeightmapTerrain terrain, Scenery scenery)
        {
            _terrain = terrain;
            _scenery = scenery;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<HeightmapTerrain>();
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (_terrain == null || world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NavGridInfo>()))
            {
                if (!existing.IsEmptyIgnoreFilter)
                {
                    HasGrid = true;
                    GridInfo = entityManager.GetComponentData<NavGridInfo>(
                        existing.GetSingletonEntity());
                    return;
                }
            }

            Build(entityManager);
        }

        public void Build(EntityManager entityManager)
        {
            var cellCount = Mathf.CeilToInt(_terrain.Size / _cellSize);
            var info = new NavGridInfo
            {
                Origin = new float2(-_terrain.Size * 0.5f, -_terrain.Size * 0.5f),
                Size = new int2(cellCount, cellCount),
                CellSize = _cellSize,
                MaxSlope = _maxSlope,
            };

            NavBlocker[] sceneryBlockers = System.Array.Empty<NavBlocker>();
            if (_scenery != null && _scenery.Obstacles != null)
            {
                var specs = _scenery.Obstacles;
                sceneryBlockers = new NavBlocker[specs.Length];
                for (var i = 0; i < specs.Length; i++)
                {
                    sceneryBlockers[i] = new NavBlocker
                    {
                        Centre = new float2(specs[i].Centre.x, specs[i].Centre.y),
                        Radius = specs[i].Radius,
                    };
                }
            }

            var blockers = new NavBlocker[sceneryBlockers.Length + 1];
            System.Array.Copy(sceneryBlockers, blockers, sceneryBlockers.Length);
            blockers[blockers.Length - 1] = new NavBlocker
            {
                Centre = new float2(-100f, 0f),
                Radius = 28f,
            };

            var walkable = NavGridBuilder.Build(
                cellCount,
                cellCount,
                _cellSize,
                info.Origin,
                _maxSlope,
                _terrain.SampleHeight,
                blockers,
                _edgeMargin,
                _obstaclePadding);

            var entity = entityManager.CreateEntity();

            var resolution = Mathf.Max(2, _terrain.Resolution);
            entityManager.AddComponentData(entity, new TerrainInfo
            {
                Resolution = resolution,
                Size = _terrain.Size,
            });

            var heights = entityManager.AddBuffer<TerrainHeightSample>(entity);
            heights.ResizeUninitialized(resolution * resolution);
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    heights[z * resolution + x] = new TerrainHeightSample
                    {
                        Height = _terrain.GridHeight(x, z),
                    };
                }
            }

            entityManager.AddComponentData(entity, info);

            var nav = entityManager.AddBuffer<NavCell>(entity);
            nav.ResizeUninitialized(walkable.Length);
            var walkableCount = 0;
            for (var i = 0; i < walkable.Length; i++)
            {
                nav[i] = new NavCell { Walkable = walkable[i] };
                walkableCount += walkable[i] != 0 ? 1 : 0;
            }

            GridInfo = info;
            HasGrid = true;

            Debug.Log(
                $"[NavGrid] {cellCount}x{cellCount} cells @ {_cellSize}m | " +
                $"{walkableCount}/{walkable.Length} walkable | {blockers.Length} blockers");
        }

        private void OnDestroy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NavGridInfo>(),
                ComponentType.ReadOnly<NavCell>());

            if (!query.IsEmptyIgnoreFilter)
            {
                entityManager.DestroyEntity(query.GetSingletonEntity());
            }
        }
    }
}
