using CQRa.Sim.Base;
using CQRa.Sim.Navigation;
using CQRa.View;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Scene authoring for the economy: one <see cref="EconomyState"/> singleton and a
    /// deterministic ring of ore patches. Runs in Start so the nav grid and building
    /// visuals already exist.
    /// </summary>
    public sealed class EconomyAuthoring : MonoBehaviour
    {
        [SerializeField] private HeightmapTerrain _terrain;
        [SerializeField] private int _orePerPatch = 3000;
        [SerializeField] private float _oreRadius = 7f;

        private static readonly Vector2[] CratePositions =
        {
            new Vector2(0f, 0f),
            new Vector2(-46f, 44f),
            new Vector2(52f, -34f),
            new Vector2(-64f, -46f),
            new Vector2(72f, 18f),
            new Vector2(24f, -58f),
        };

        private static readonly Vector2[] OrePositions =
        {
            new Vector2(-70f, 0f),
            new Vector2(70f, 0f),
            new Vector2(0f, 72f),
            new Vector2(0f, -72f),
            new Vector2(-96f, -58f),
            new Vector2(96f, 58f),
            new Vector2(-58f, 96f),
            new Vector2(58f, -96f),
            new Vector2(-104f, 30f),
            new Vector2(104f, -30f),
        };

        public void Configure(HeightmapTerrain terrain)
        {
            _terrain = terrain;
        }

        private void Start()
        {
            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<HeightmapTerrain>();
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            EnsureEconomy(entityManager);

            using var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<OreNode>());
            if (!existing.IsEmptyIgnoreFilter)
            {
                return;
            }

            var spawned = 0;
            for (var i = 0; i < OrePositions.Length; i++)
            {
                var point = OrePositions[i];
                if (!NavGridQuery.IsWalkable(entityManager, point))
                {
                    continue;
                }

                var y = _terrain != null ? _terrain.SampleHeight(point.x, point.y) : 0f;
                BuildingFactory.SpawnOre(
                    entityManager,
                    new float3(point.x, y, point.y),
                    _orePerPatch,
                    _oreRadius,
                    i * 0.7f);
                spawned++;
            }

            Debug.Log($"[Economy] {spawned} ore patches | credits {BuildCatalog.InitialCredits}");

            using var existingCrates = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Crate>());
            if (existingCrates.IsEmptyIgnoreFilter)
            {
                var crates = 0;
                for (var i = 0; i < CratePositions.Length; i++)
                {
                    var point = CratePositions[i];
                    if (!NavGridQuery.IsWalkable(entityManager, point))
                    {
                        continue;
                    }

                    var y = _terrain != null ? _terrain.SampleHeight(point.x, point.y) : 0f;
                    var repair = i % 3 == 2;
                    BuildingFactory.SpawnCrate(
                        entityManager,
                        new float3(point.x, y, point.y),
                        repair ? 80 : 250,
                        (byte)(repair ? 1 : 0));
                    crates++;
                }

                Debug.Log($"[Economy] {crates} crates");
            }
        }

        private static void EnsureEconomy(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(typeof(EconomyState));
            entityManager.SetComponentData(entity, new EconomyState
            {
                Team0Credits = BuildCatalog.InitialCredits,
                Team1Credits = BuildCatalog.InitialCredits,
            });
        }
    }
}
