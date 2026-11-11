using CQRa.Sim.Factions;
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
        TooFar = 9,
        NotNearWater = 10,
    }

    public enum BuildingCommandFailure : byte
    {
        None = 0,
        NotABuilding = 1,
        NoEconomy = 2,
        AlreadyFull = 3,
        CannotSell = 4,
    }

    public enum CaptureFailure : byte
    {
        None = 0,
        NotAnEngineer = 1,
        NotABuilding = 2,
        AlreadyOwned = 3,
        TooFar = 4,
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
        /// Deploys an MCV into a construction yard at its current position.
        /// </summary>
        public static bool TryDeployMcv(EntityManager entityManager, Entity mcv, out BuildFailure failure)
        {
            if (!entityManager.Exists(mcv) ||
                !entityManager.HasComponent<LocalTransform>(mcv))
            {
                failure = BuildFailure.NotAnMcv;
                return false;
            }

            var position = entityManager.GetComponentData<LocalTransform>(mcv).Position;
            return TryDeployMcvAt(entityManager, mcv, position, out failure, ignoreUnits: true);
        }

        /// <summary>
        /// Deploys an MCV at a chosen site. Uses the same terrain/overlap rules as building
        /// placement, ignores the MCV itself, and limits the deploy distance so the yard
        /// cannot be teleported across the map.
        /// </summary>
        public static bool TryDeployMcvAt(
            EntityManager entityManager,
            Entity mcv,
            float3 site,
            out BuildFailure failure,
            bool ignoreUnits = false)
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

            var mcvPosition = entityManager.GetComponentData<LocalTransform>(mcv).Position;
            if (math.distance(new float2(site.x, site.z), new float2(mcvPosition.x, mcvPosition.z)) > 50f)
            {
                failure = BuildFailure.TooFar;
                return false;
            }

            var spec = BuildCatalog.Get(BuildingKind.ConstructionYard);
            var point = new float2(site.x, site.z);
            if (IsOccupied(entityManager, team, point, spec, out failure, mcv, ignoreUnits))
            {
                return false;
            }

            entityManager.DestroyEntity(mcv);
            BuildingFactory.SpawnBuilding(entityManager, team, BuildingKind.ConstructionYard, site);

            NavGridQuery.BlockCircle(entityManager, point, spec.NavRadius);
            EvacuateUnits(entityManager, point, spec.NavRadius);
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
            var cost = FactionRuntime.CostFor(team, spec.Cost);
            if (!BuildRules.CanAfford(Economy.GetCredits(economy, team), cost))
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
            if (kind == BuildingKind.NavalYard)
            {
                var lakeDistance = math.distance(point2, WaterLake.Centre);
                if (lakeDistance > WaterLake.Radius + 28f)
                {
                    failure = BuildFailure.NotNearWater;
                    return false;
                }
            }
            else if (!BuildRules.IsWithinBuildRadius(
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

            Economy.TrySpend(ref economy, team, cost);
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
            out BuildFailure failure,
            Entity ignoreUnit = default,
            bool ignoreUnits = false)
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

            if (!ignoreUnits)
            {
                using (var units = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<UnitTag>(),
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<LocalTransform>()))
                using (var entities = units.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < entities.Length; i++)
                    {
                        if (entities[i] == ignoreUnit)
                        {
                            continue;
                        }

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
        /// Engineers capture an enemy building within 10 m. The building changes team,
        /// its production queue is cleared, and the engineer is consumed.
        /// </summary>
        public static bool TryCaptureBuilding(
            EntityManager entityManager,
            Entity engineer,
            Entity building,
            out CaptureFailure failure)
        {
            failure = CaptureFailure.None;

            if (!entityManager.Exists(engineer) ||
                !entityManager.HasComponent<UnitInfo>(engineer) ||
                entityManager.GetComponentData<UnitInfo>(engineer).Kind != UnitKind.Engineer)
            {
                failure = CaptureFailure.NotAnEngineer;
                return false;
            }

            if (!IsBuilding(entityManager, building))
            {
                failure = CaptureFailure.NotABuilding;
                return false;
            }

            var engineerInfo = entityManager.GetComponentData<UnitInfo>(engineer);
            var buildingInfo = entityManager.GetComponentData<BuildingInfo>(building);
            if (engineerInfo.Team == buildingInfo.Team)
            {
                failure = CaptureFailure.AlreadyOwned;
                return false;
            }

            var engineerPosition = entityManager.GetComponentData<LocalTransform>(engineer).Position;
            var buildingPosition = entityManager.GetComponentData<LocalTransform>(building).Position;
            if (math.distance(
                    new float2(engineerPosition.x, engineerPosition.z),
                    new float2(buildingPosition.x, buildingPosition.z)) > 10f)
            {
                failure = CaptureFailure.TooFar;
                return false;
            }

            buildingInfo.Team = engineerInfo.Team;
            entityManager.SetComponentData(building, buildingInfo);

            if (entityManager.HasComponent<ProductionOrder>(building))
            {
                entityManager.GetBuffer<ProductionOrder>(building).Clear();
            }

            if (entityManager.HasComponent<RallyPoint>(building))
            {
                var direction = math.normalizesafe(
                    new float2(-buildingPosition.x, -buildingPosition.z),
                    new float2(0f, 1f));
                entityManager.SetComponentData(building, new RallyPoint
                {
                    Value = new float3(
                        buildingPosition.x + direction.x * 22f,
                        buildingPosition.y,
                        buildingPosition.z + direction.y * 22f),
                    HasValue = 1,
                });
            }

            if (entityManager.HasComponent<RepairState>(building))
            {
                entityManager.SetComponentData(building, new RepairState { Active = 0 });
            }

            if (entityManager.HasComponent<Unity.Rendering.MaterialMeshInfo>(building))
            {
                entityManager.SetComponentData(
                    building,
                    Unity.Rendering.MaterialMeshInfo.FromRenderMeshArrayIndices(
                        engineerInfo.Team * BuildingFactory.BuildingKindCount + (int)buildingInfo.Kind,
                        (int)buildingInfo.Kind));
            }

            entityManager.DestroyEntity(engineer);
            return true;
        }

        /// <summary>
        /// Toggles deployable artillery between mobile and emplaced modes. Deployed mode
        /// trades movement for longer range and a slower reload.
        /// </summary>
        public static bool TryToggleDeploy(EntityManager entityManager, Entity unit)
        {
            if (!entityManager.Exists(unit) ||
                !entityManager.HasComponent<DeployState>(unit))
            {
                return false;
            }

            var state = entityManager.GetComponentData<DeployState>(unit);
            var weapon = entityManager.GetComponentData<Weapon>(unit);

            if (state.Deployed == 0)
            {
                state.Deployed = 1;
                weapon.Range = state.DeployedRange;
                weapon.Cooldown = state.DeployedCooldown;

                var move = entityManager.GetComponentData<MoveTarget>(unit);
                move.HasOrder = 0;
                move.Field = Entity.Null;
                entityManager.SetComponentData(unit, move);
            }
            else
            {
                state.Deployed = 0;
                weapon.Range = state.NormalRange;
                weapon.Cooldown = state.NormalCooldown;
            }

            entityManager.SetComponentData(unit, state);
            entityManager.SetComponentData(unit, weapon);
            return true;
        }

        /// <summary>
        /// Lays a mine at the Sower's position if its cooldown is ready.
        /// </summary>
        public static bool TryLayMine(EntityManager entityManager, Entity sower)
        {
            if (!entityManager.Exists(sower) ||
                !entityManager.HasComponent<MineLayer>(sower) ||
                !entityManager.HasComponent<UnitInfo>(sower))
            {
                return false;
            }

            var layer = entityManager.GetComponentData<MineLayer>(sower);
            if (layer.CooldownRemaining > 0f)
            {
                return false;
            }

            var team = entityManager.GetComponentData<UnitInfo>(sower).Team;
            var position = entityManager.GetComponentData<LocalTransform>(sower).Position;
            BuildingFactory.SpawnMine(entityManager, position, team);

            layer.CooldownRemaining = layer.Cooldown;
            entityManager.SetComponentData(sower, layer);
            return true;
        }

        /// <summary>
        /// Toggles continuous repair. Cancelling is always free; starting requires damaged
        /// health and an economy singleton.
        /// </summary>
        public static bool TryToggleRepair(
            EntityManager entityManager,
            Entity building,
            out BuildingCommandFailure failure)
        {
            failure = BuildingCommandFailure.None;

            if (!IsBuilding(entityManager, building) ||
                !entityManager.HasComponent<RepairState>(building))
            {
                failure = BuildingCommandFailure.NotABuilding;
                return false;
            }

            var repair = entityManager.GetComponentData<RepairState>(building);
            if (repair.Active != 0)
            {
                repair.Active = 0;
                entityManager.SetComponentData(building, repair);
                return true;
            }

            var health = entityManager.GetComponentData<Health>(building);
            if (health.Current >= health.Max - 0.001f)
            {
                failure = BuildingCommandFailure.AlreadyFull;
                return false;
            }

            if (!TryGetEconomy(entityManager, out _, out _))
            {
                failure = BuildingCommandFailure.NoEconomy;
                return false;
            }

            repair.Active = 1;
            entityManager.SetComponentData(building, repair);
            return true;
        }

        /// <summary>Sells a building for half its cost, frees its nav footprint and removes it.</summary>
        public static bool TrySell(
            EntityManager entityManager,
            Entity building,
            out int refund,
            out BuildingCommandFailure failure)
        {
            refund = 0;
            failure = BuildingCommandFailure.None;

            if (!IsBuilding(entityManager, building))
            {
                failure = BuildingCommandFailure.NotABuilding;
                return false;
            }

            var info = entityManager.GetComponentData<BuildingInfo>(building);
            if (!RepairRules.CanSell(info.Kind))
            {
                failure = BuildingCommandFailure.CannotSell;
                return false;
            }

            if (!TryGetEconomy(entityManager, out var economyEntity, out var economy))
            {
                failure = BuildingCommandFailure.NoEconomy;
                return false;
            }

            refund = RepairRules.SellRefund(info.Kind);
            Economy.AddCredits(ref economy, info.Team, refund);
            entityManager.SetComponentData(economyEntity, economy);

            RemoveBuilding(entityManager, building);
            return true;
        }

        /// <summary>
        /// Removes a building and frees the nav cells it blocked. Also used by combat when
        /// a building is destroyed.
        /// </summary>
        public static void RemoveBuilding(EntityManager entityManager, Entity building)
        {
            if (!entityManager.Exists(building))
            {
                return;
            }

            if (entityManager.HasComponent<BuildingInfo>(building) &&
                entityManager.HasComponent<LocalTransform>(building))
            {
                var info = entityManager.GetComponentData<BuildingInfo>(building);
                var position = entityManager.GetComponentData<LocalTransform>(building).Position;
                var spec = BuildCatalog.Get(info.Kind);
                NavGridQuery.UnblockCircle(
                    entityManager,
                    new float2(position.x, position.z),
                    spec.NavRadius);
                NavGridQuery.InvalidateFlowFields(entityManager);
            }

            entityManager.DestroyEntity(building);
        }

        private static bool IsBuilding(EntityManager entityManager, Entity entity)
        {
            return entity != Entity.Null &&
                   entityManager.Exists(entity) &&
                   entityManager.HasComponent<BuildingInfo>(entity) &&
                   entityManager.HasComponent<BuildingTag>(entity);
        }

        /// <summary>
        /// Places one building at the first valid deterministic site around the team's
        /// construction yard. Used by the AI and capture helpers.
        /// </summary>
        public static bool TryAutoPlace(EntityManager entityManager, int team, BuildingKind kind)
        {
            var yard = FindConstructionYard(entityManager, team);
            if (yard == Entity.Null)
            {
                return false;
            }

            var centre = entityManager.GetComponentData<LocalTransform>(yard).Position;
            var candidates = BuildCandidates(centre);

            for (var i = 0; i < candidates.Length; i++)
            {
                if (TryPlaceBuilding(entityManager, team, kind, candidates[i], out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Finds a shore site around the lake for the AI naval yard.</summary>
        public static bool TryAutoPlaceNavalYard(EntityManager entityManager, int team)
        {
            var radius = WaterLake.Radius + 8f;
            for (var step = 0; step < 24; step++)
            {
                var angle = step * math.PI / 12f;
                var point = new float3(
                    WaterLake.Centre.x + math.cos(angle) * radius,
                    0f,
                    WaterLake.Centre.y + math.sin(angle) * radius);

                if (TryPlaceBuilding(entityManager, team, BuildingKind.NavalYard, point, out _))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Development helper: one gun tower and a short wall line in front of the yard.
        /// Used by the Phase 11 capture rig.
        /// </summary>
        public static int AutoBuildDefenses(EntityManager entityManager, int team)
        {
            var placed = 0;
            if (TryAutoPlace(entityManager, team, BuildingKind.GunTower))
            {
                placed++;
            }

            for (var i = 0; i < 3; i++)
            {
                if (TryAutoPlace(entityManager, team, BuildingKind.Wall))
                {
                    placed++;
                }
            }

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
