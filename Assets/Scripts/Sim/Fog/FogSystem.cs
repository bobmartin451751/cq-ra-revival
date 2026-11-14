using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Fog
{
    /// <summary>
    /// Maintains one fog grid per team at 4 m resolution. Visible cells decay to explored
    /// each tick, then every living unit and building stamps its vision radius. Combat and
    /// the AI read the same grids, so neither side can target through the fog.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(CombatSystem))]
    [UpdateBefore(typeof(AiSystem))]
    public partial class FogSystem : SystemBase
    {
        private const float CellSize = 4f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var terrainQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainInfo>());
            if (terrainQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var terrain = entityManager.GetComponentData<TerrainInfo>(terrainQuery.GetSingletonEntity());
            if (terrain.Size <= 0f)
            {
                return;
            }

            EnsureFog(entityManager, terrain);

            var terrainEntity = terrainQuery.GetSingletonEntity();
            var heights = entityManager.GetBuffer<TerrainHeightSample>(terrainEntity).AsNativeArray();

            for (var team = 0; team < MatchRuntime.TeamCount; team++)
            {
                if (!FogQuery.TryGet(entityManager, team, out var fog, out _))
                {
                    continue;
                }

                var cells = FogQuery.GetCells(entityManager, fog);
                FogMath.Decay(cells);
            }

            StampUnits(entityManager, terrain, heights);
            StampBuildings(entityManager, terrain, heights);

            for (var team = 0; team < MatchRuntime.TeamCount; team++)
            {
                if (FogQuery.TryGet(entityManager, team, out var fog, out _))
                {
                    BumpRevision(entityManager, fog);
                }
            }
        }

        private static void EnsureFog(EntityManager entityManager, in TerrainInfo terrain)
        {
            var cells = math.max(1, (int)math.ceil(terrain.Size / CellSize));
            var actualCell = terrain.Size / cells;

            var info = new FogGridInfo
            {
                Origin = new float2(-terrain.Size * 0.5f, -terrain.Size * 0.5f),
                Size = new int2(cells, cells),
                CellSize = actualCell,
            };

            for (var team = 0; team < MatchRuntime.TeamCount; team++)
            {
                if (FogQuery.TryGet(entityManager, team, out _, out _))
                {
                    continue;
                }

                var fog = entityManager.CreateEntity(
                    typeof(FogState),
                    typeof(FogTeam),
                    typeof(FogGridInfo));

                entityManager.SetComponentData(fog, new FogState { Revision = 0 });
                entityManager.SetComponentData(fog, new FogTeam { Team = team });
                entityManager.SetComponentData(fog, info);

                var buffer = entityManager.AddBuffer<FogCell>(fog);
                buffer.ResizeUninitialized(cells * cells);
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = new FogCell { Value = FogMath.Never };
                }
            }
        }

        private static void BumpRevision(EntityManager entityManager, Entity fog)
        {
            var state = entityManager.GetComponentData<FogState>(fog);
            state.Revision++;
            entityManager.SetComponentData(fog, state);
        }

        private static void StampUnits(
            EntityManager entityManager,
            in TerrainInfo terrain,
            NativeArray<TerrainHeightSample> heights)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<VisionSource>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var units = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < units.Length; i++)
            {
                if (entityManager.GetComponentData<Health>(units[i]).Current <= 0f)
                {
                    continue;
                }

                var unitInfo = entityManager.GetComponentData<UnitInfo>(units[i]);
                if (!FogQuery.TryGet(entityManager, unitInfo.Team, out var fog, out var info))
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(units[i]).Position;
                var radius = entityManager.GetComponentData<VisionSource>(units[i]).Radius;
                var cells = FogQuery.GetCells(entityManager, fog);
                var elevated = entityManager.HasComponent<AirUnit>(units[i]) ||
                               entityManager.HasComponent<NavalUnit>(units[i]);
                var eyeHeight = NavalRules.IsNaval(unitInfo.Kind) ? 5f : 2.2f;

                StampCells(
                    cells,
                    terrain,
                    heights,
                    info,
                    position,
                    radius,
                    !elevated,
                    eyeHeight);
            }
        }

        private static void StampBuildings(
            EntityManager entityManager,
            in TerrainInfo terrain,
            NativeArray<TerrainHeightSample> heights)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingTag>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<VisionSource>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var buildings = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (entityManager.GetComponentData<Health>(buildings[i]).Current <= 0f)
                {
                    continue;
                }

                var buildingInfo = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                if (!FogQuery.TryGet(entityManager, buildingInfo.Team, out var fog, out var info))
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(buildings[i]).Position;
                var radius = entityManager.GetComponentData<VisionSource>(buildings[i]).Radius;
                var cells = FogQuery.GetCells(entityManager, fog);

                StampCells(cells, terrain, heights, info, position, radius, true, 8f);
            }
        }

        private static void StampCells(
            NativeArray<FogCell> cells,
            in TerrainInfo terrain,
            NativeArray<TerrainHeightSample> heights,
            in FogGridInfo info,
            float3 position,
            float radius,
            bool useLineOfSight,
            float eyeHeight)
        {
            var centre = new float2(position.x, position.z);
            var min = FogMath.WorldToCell(info, centre - radius);
            var max = FogMath.WorldToCell(info, centre + radius);
            var radiusSq = radius * radius;

            for (var z = math.max(0, min.y); z <= math.min(info.Size.y - 1, max.y); z++)
            {
                for (var x = math.max(0, min.x); x <= math.min(info.Size.x - 1, max.x); x++)
                {
                    var cellCentre = info.Origin + (new float2(x, z) + 0.5f) * info.CellSize;
                    if (math.distancesq(cellCentre, centre) > radiusSq)
                    {
                        continue;
                    }

                    if (useLineOfSight &&
                        TerrainLineOfSight.IsBlocked(heights, terrain, position, eyeHeight, cellCentre))
                    {
                        continue;
                    }

                    var index = z * info.Size.x + x;
                    if (cells[index].Value < FogMath.Visible)
                    {
                        cells[index] = new FogCell { Value = FogMath.Visible };
                    }
                }
            }
        }
    }
}
