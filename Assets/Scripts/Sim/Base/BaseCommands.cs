using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    public enum BuildFailure : byte
    {
        None = 0,
        NoEconomy = 1,
        NotEnoughCredits = 2,
        NoConstructionYard = 3,
        OutsideBuildRadius = 4,
        BadTerrain = 5,
        Occupied = 6,
        NotAnMcv = 7,
        AlreadyDeployed = 8,
    }

    /// <summary>
    /// Player-facing base commands shared by the input layer, tests and capture tools.
    /// Everything here goes through the same deterministic entity state the simulation
    /// uses; no UnityEngine callbacks are involved.
    /// </summary>
    public static class BaseCommands
    {
        public static Entity FindConstructionYard(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ConstructionYardTag>(),
                ComponentType.ReadOnly<BuildingInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<BuildingInfo>(entities[i]).Team == team)
                {
                    return entities[i];
                }
            }

            return Entity.Null;
        }

        public static Entity FindNearestRefinery(EntityManager entityManager, int team, float3 position)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<RefineryTag>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var best = Entity.Null;
            var bestDistanceSq = float.MaxValue;

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entityManager.GetComponentData<BuildingInfo>(entity).Team != team)
                {
                    continue;
                }

                var candidate = entityManager.GetComponentData<LocalTransform>(entity).Position;
                var distanceSq = math.distancesq(position, candidate);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = entity;
                }
            }

            return best;
        }

        public static bool TryGetEconomy(EntityManager entityManager, out Entity entity, out EconomyState state)
        {
            entity = Entity.Null;
            state = default;

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            entity = query.GetSingletonEntity();
            state = entityManager.GetComponentData<EconomyState>(entity);
            return true;
        }

        /// <summary>
        /// Deploys an MCV into a construction yard. The MCV is consumed; the yard blocks
        /// the nav grid under its footprint and defines the team's build radius.
        /// </summary>
        public static bool TryDeployMcv(EntityManager entityManager, Entity mcv, out BuildFailure failure)
        {
            failure = BuildFailure.None;

            if (!entityManager.Exists(mcv) ||
                !entityManager.HasComponent<UnitInfo>(mcv) ||
                entityManager.GetComponentData<UnitInfo>(mcv).Kind != UnitKind.Mcv)
            {
                failure = BuildFailure.NotAnMcv;
                return false;
            }

            var team = entityManager.GetComponentData<UnitInfo>(mcv).Team;
            if (FindConstructionYard(entityManager, team) != Entity.Null)
            {
                failure = BuildFailure.AlreadyDeployed;
                return false;
            }

            var position = entityManager.GetComponentData<LocalTransform>(mcv).Position;
            var spec = BuildCatalog.Get(BuildingKind.ConstructionYard);

            entityManager.DestroyEntity(mcv);
            BuildingFactory.SpawnBuilding(entityManager, team, BuildingKind.ConstructionYard, position);

            NavGridQuery.BlockCircle(entityManager, new float2(position.x, position.z), spec.NavRadius);
            EvacuateUnits(entityManager, new float2(position.x, position.z), spec.NavRadius);
            NavGridQuery.InvalidateFlowFields(entityManager);

            return true;
        }

        /// <summary>
        /// Validates and places a building for a team at a world point. Spends credits only
        /// on success, and blocks the nav grid under the new footprint.
        /// </summary>
        public static bool TryPlaceBuilding(
            EntityManager entityManager,
            int team,
            BuildingKind kind,
            float3 point,
            out BuildFailure failure)
        {
            failure = BuildFailure.None;

            if (!TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                failure = BuildFailure.NoEconomy;
                return false;
            }

            var spec = BuildCatalog.Get(kind);
            if (!BuildRules.CanAfford(Economy.GetCredits(economy, team), spec.Cost))
            {
                failure = BuildFailure.NotEnoughCredits;
                return false;
            }

            var yard = FindConstructionYard(entityManager, team);
            if (yard == Entity.Null)
            {
                failure = BuildFailure.NoConstructionYard;
                return false;
            }

            var yardPosition = entityManager.GetComponentData<LocalTransform>(yard).Position;
            var point2 = new float2(point.x, point.z);
            if (!BuildRules.IsWithinBuildRadius(
                    point2,
                    new float2(yardPosition.x, yardPosition.z),
                    BuildCatalog.BuildRadius))
            {
                failure = BuildFailure.OutsideBuildRadius;
                return false;
            }

            if (IsOccupied(entityManager, team, point2, spec, out var blocked))
            {
                failure = blocked;
                return false;
            }

            Economy.TrySpend(ref economy, team, spec.Cost);
            entityManager.SetComponentData(economyEntity, economy);

            BuildingFactory.SpawnBuilding(entityManager, team, kind, point);
            NavGridQuery.BlockCircle(entityManager, point2, spec.NavRadius);
            EvacuateUnits(entityManager, point2, spec.NavRadius);
            NavGridQuery.InvalidateFlowFields(entityManager);

            return true;
        }

        /// <summary>
        /// Pushes any unit that ends up inside a newly blocked footprint to the nearest
        /// walkable cell. Without this, a unit standing under a deployed yard would be
        /// sealed inside blocked cells and could never move again.
        /// </summary>
        private static void EvacuateUnits(EntityManager entityManager, float2 centre, float radius)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var transform = entityManager.GetComponentData<LocalTransform>(entities[i]);
                var position = transform.Position;
                if (math.distance(new float2(position.x, position.z), centre) > radius)
                {
                    continue;
                }

                if (NavGridQuery.TryClampToWalkable(entityManager, position, out var safe))
                {
                    transform.Position = new float3(safe.x, position.y, safe.z);
                    entityManager.SetComponentData(entities[i], transform);
                }
            }
        }

        private static bool IsOccupied(
            EntityManager entityManager,
            int team,
            float2 point,
            in BuildingSpec spec,
            out BuildFailure failure)
        {
            failure = BuildFailure.None;

            if (!NavGridQuery.IsWalkable(entityManager, point) &&
                !IsInsideBuildingFootprint(entityManager, point))
            {
                failure = BuildFailure.BadTerrain;
                return true;
            }

            // Sample the rim too: a building centre can be flat while its edge is a cliff.
            // A rim cell blocked by a *building* is fine (buildings may abut); only
            // terrain, map edges and scenery should reject the site.
            var sampleRadius = spec.NavRadius * 0.75f;
            for (var i = 0; i < 4; i++)
            {
                var angle = i * math.PI * 0.5f;
                var sample = point + new float2(math.cos(angle), math.sin(angle)) * sampleRadius;
                if (!NavGridQuery.IsWalkable(entityManager, sample) &&
                    !IsInsideBuildingFootprint(entityManager, sample))
                {
                    failure = BuildFailure.BadTerrain;
                    return true;
                }
            }

            using (var buildings = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>()))
            using (var entities = buildings.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var info = entityManager.GetComponentData<BuildingInfo>(entities[i]);
                    var other = entityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                    if (BuildRules.FootprintsOverlap(
                            point,
                            spec.Radius,
                            new float2(other.x, other.z),
                            info.Radius))
                    {
                        failure = BuildFailure.Occupied;
                        return true;
                    }
                }
            }

            using (var units = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<LocalTransform>()))
            using (var entities = units.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(entities[i]);
                    var other = entityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                    if (BuildRules.FootprintsOverlap(
                            point,
                            spec.Radius,
                            new float2(other.x, other.z),
                            info.Radius))
                    {
                        failure = BuildFailure.Occupied;
                        return true;
                    }
                }
            }

            using (var ore = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<OreNode>(),
                ComponentType.ReadOnly<LocalTransform>()))
            using (var entities = ore.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var node = entityManager.GetComponentData<OreNode>(entities[i]);
                    var other = entityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                    if (BuildRules.FootprintsOverlap(
                            point,
                            spec.Radius,
                            new float2(other.x, other.z),
                            node.Radius))
                    {
                        failure = BuildFailure.Occupied;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsInsideBuildingFootprint(EntityManager entityManager, float2 point)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(entities[i]);
                var other = entityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                var navRadius = BuildCatalog.Get(info.Kind).NavRadius;
                if (math.distance(point, new float2(other.x, other.z)) < navRadius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Development helper: deploy the MCV and place one power plant plus one refinery
        /// around the yard. Used by the screenshot rig; returns the number of placements.
        /// </summary>
        public static int AutoBuildBase(EntityManager entityManager, int team)
        {
            var placed = 0;
            var mcv = FindMcv(entityManager, team);
            if (mcv != Entity.Null && TryDeployMcv(entityManager, mcv, out _))
            {
                placed++;
            }

            var yard = FindConstructionYard(entityManager, team);
            if (yard == Entity.Null)
            {
                return placed;
            }

            var centre = entityManager.GetComponentData<LocalTransform>(yard).Position;
            var candidates = BuildCandidates(centre);

            placed += AutoPlace(entityManager, team, BuildingKind.PowerPlant, candidates);
            placed += AutoPlace(entityManager, team, BuildingKind.Refinery, candidates);
            return placed;
        }

        private static float3[] BuildCandidates(float3 centre)
        {
            var radii = new[] { 20f, 26f, 32f, 38f };
            var candidates = new float3[radii.Length * 16];
            var index = 0;

            for (var r = 0; r < radii.Length; r++)
            {
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * math.PI / 8f;
                    candidates[index++] = new float3(
                        centre.x + math.cos(angle) * radii[r],
                        centre.y,
                        centre.z + math.sin(angle) * radii[r]);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Development helper: place a barracks and a war factory around the yard.
        /// Used by the Phase 6 capture rig.
        /// </summary>
        public static int AutoBuildProduction(EntityManager entityManager, int team)
        {
            var yard = FindConstructionYard(entityManager, team);
            if (yard == Entity.Null)
            {
                return 0;
            }

            var centre = entityManager.GetComponentData<LocalTransform>(yard).Position;
            var candidates = BuildCandidates(centre);

            var placed = 0;
            placed += AutoPlace(entityManager, team, BuildingKind.Barracks, candidates);
            placed += AutoPlace(entityManager, team, BuildingKind.WarFactory, candidates);
            return placed;
        }

        private static Entity FindMcv(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(entities[i]);
                if (info.Team == team && info.Kind == UnitKind.Mcv)
                {
                    return entities[i];
                }
            }

            return Entity.Null;
        }

        private static int AutoPlace(
            EntityManager entityManager,
            int team,
            BuildingKind kind,
            float3[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                if (TryPlaceBuilding(entityManager, team, kind, candidates[i], out _))
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
