using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.AI
{
    /// <summary>
    /// Deterministic skirmish AI. It never touches simulation state directly: every base,
    /// production, harvest and attack action goes through the same commands the player
    /// uses, so the AI pays the same costs and obeys the same build radius and queues.
    ///
    /// Difficulty only changes decision quality (think interval, army size threshold,
    /// production mix and building caps). There are no credit, vision or stat bonuses.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    public partial class AiSystem : SystemBase
    {
        private const int CreditsReserve = 150;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            using var commanderQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<AiCommander>());
            if (commanderQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var commanders = commanderQuery.ToEntityArray(Allocator.Temp);
            var dt = SystemAPI.Time.DeltaTime;

            for (var i = 0; i < commanders.Length; i++)
            {
                var commander = entityManager.GetComponentData<AiCommander>(commanders[i]);
                commander.ThinkTimer -= dt;

                if (commander.ThinkTimer > 0f)
                {
                    entityManager.SetComponentData(commanders[i], commander);
                    continue;
                }

                var profile = AiProfile.For(commander.Difficulty);
                commander.ThinkTimer = profile.ThinkInterval;
                Think(entityManager, ref commander, profile);
                entityManager.SetComponentData(commanders[i], commander);
            }
        }

        private static void Think(EntityManager entityManager, ref AiCommander commander, in AiProfile profile)
        {
            var team = commander.Team;
            if (!BaseCommands.TryGetEconomy(entityManager, out _, out var economy))
            {
                return;
            }

            var credits = Economy.GetCredits(economy, team);
            var yard = BaseCommands.FindConstructionYard(entityManager, team);

            if (yard == Entity.Null)
            {
                ThinkBeforeYard(entityManager, team, credits);
                return;
            }

            MaintainHarvesters(entityManager, team, profile, credits);
            MaintainArmy(entityManager, team, profile, credits);
            MaintainNavy(entityManager, team, profile, credits);

            if (TryBuild(entityManager, team, profile, credits, economy))
            {
                return;
            }

            UpdateMilitary(entityManager, ref commander, profile, yard);
        }

        private static void ThinkBeforeYard(EntityManager entityManager, int team, int credits)
        {
            var mcv = FindUnit(entityManager, team, UnitKind.Mcv);
            if (mcv != Entity.Null)
            {
                BaseCommands.TryDeployMcv(entityManager, mcv, out _);
                return;
            }

            var spec = ProductionCatalog.Get(UnitKind.Mcv);
            if (credits >= spec.Cost + CreditsReserve &&
                CountQueued(entityManager, team, UnitKind.Mcv) == 0)
            {
                ProductionCommands.TryEnqueue(entityManager, team, UnitKind.Mcv, Entity.Null, out _);
            }
        }

        private static bool TryBuild(
            EntityManager entityManager,
            int team,
            in AiProfile profile,
            int credits,
            in EconomyState economy)
        {
            var made = Economy.GetPowerMade(economy, team);
            var used = Economy.GetPowerUsed(economy, team);

            if (used + 20 > made &&
                CountBuildings(entityManager, team, BuildingKind.PowerPlant) < profile.MaxPowerPlants)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.PowerPlant);
            }

            if (CountBuildings(entityManager, team, BuildingKind.Refinery) < profile.MaxRefineries)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.Refinery);
            }

            if (CountBuildings(entityManager, team, BuildingKind.Barracks) == 0)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.Barracks);
            }

            if (CountBuildings(entityManager, team, BuildingKind.WarFactory) == 0)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.WarFactory);
            }

            if (MapWater.HasWater &&
                credits > 1200 &&
                CountBuildings(entityManager, team, BuildingKind.Dock) == 0)
            {
                return BaseCommands.TryAutoPlaceDock(entityManager, team);
            }

            if (credits > 900 &&
                CountBuildings(entityManager, team, BuildingKind.GunTower) < profile.MaxTurrets)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.GunTower);
            }

            if (credits > 2200 &&
                CountBuildings(entityManager, team, BuildingKind.Barracks) < profile.MaxBarracks)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.Barracks);
            }

            if (credits > 2600 &&
                CountBuildings(entityManager, team, BuildingKind.WarFactory) < profile.MaxFactories)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.WarFactory);
            }

            if (used + 40 > made &&
                CountBuildings(entityManager, team, BuildingKind.PowerPlant) < profile.MaxPowerPlants)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.PowerPlant);
            }

            return false;
        }

        private static void MaintainHarvesters(
            EntityManager entityManager,
            int team,
            in AiProfile profile,
            int credits)
        {
            if (CountBuildings(entityManager, team, BuildingKind.WarFactory) == 0)
            {
                return;
            }

            var harvesters = CountUnits(entityManager, team, UnitKind.Harvester) +
                             CountQueued(entityManager, team, UnitKind.Harvester);

            if (harvesters >= profile.HarvesterTarget)
            {
                return;
            }

            var spec = ProductionCatalog.Get(UnitKind.Harvester);
            if (credits >= spec.Cost + CreditsReserve)
            {
                ProductionCommands.TryEnqueue(entityManager, team, UnitKind.Harvester, Entity.Null, out _);
            }
        }

        private static void MaintainArmy(
            EntityManager entityManager,
            int team,
            in AiProfile profile,
            int credits)
        {
            var hasBarracks = CountBuildings(entityManager, team, BuildingKind.Barracks) > 0;
            var hasFactory = CountBuildings(entityManager, team, BuildingKind.WarFactory) > 0;
            if (!hasBarracks && !hasFactory)
            {
                return;
            }

            var infantry = CountUnits(entityManager, team, UnitKind.Infantry) +
                           CountQueued(entityManager, team, UnitKind.Infantry);
            var tanks = CountUnits(entityManager, team, UnitKind.Tank) +
                        CountQueued(entityManager, team, UnitKind.Tank);
            var army = infantry + tanks;

            if (army >= profile.ArmyCap)
            {
                return;
            }

            var faction = FactionRuntime.ForTeam(team);
            var unique = ProductionCatalog.UniqueFor(faction);
            var uniqueSpec = ProductionCatalog.Get(unique);
            var uniques = CountUnits(entityManager, team, unique) +
                          CountQueued(entityManager, team, unique);
            if (uniques == 0 && army >= 3 && credits >= uniqueSpec.Cost + CreditsReserve)
            {
                ProductionCommands.TryEnqueue(entityManager, team, unique, Entity.Null, out _);
                return;
            }

            if (profile.MaxTurrets >= 2 && army >= 4)
            {
                var gunships = CountUnits(entityManager, team, UnitKind.Gunship) +
                               CountQueued(entityManager, team, UnitKind.Gunship);
                var gunshipSpec = ProductionCatalog.Get(UnitKind.Gunship);
                if (gunships == 0 && credits >= gunshipSpec.Cost + CreditsReserve)
                {
                    ProductionCommands.TryEnqueue(
                        entityManager, team, UnitKind.Gunship, Entity.Null, out _);
                    return;
                }
            }

            var desiredTanks = (int)math.round(army * profile.TankRatio);
            var kind = tanks < desiredTanks ? UnitKind.Tank : UnitKind.Infantry;

            if (kind == UnitKind.Tank && !hasFactory)
            {
                kind = UnitKind.Infantry;
            }
            else if (kind == UnitKind.Infantry && !hasBarracks)
            {
                kind = UnitKind.Tank;
            }

            var spec = ProductionCatalog.Get(kind);
            if (credits >= spec.Cost + CreditsReserve)
            {
                ProductionCommands.TryEnqueue(entityManager, team, kind, Entity.Null, out _);
            }
        }

        private static void MaintainNavy(
            EntityManager entityManager,
            int team,
            in AiProfile profile,
            int credits)
        {
            if (!MapWater.HasWater ||
                CountBuildings(entityManager, team, BuildingKind.Dock) == 0)
            {
                return;
            }

            var target = profile.MaxFactories >= 2 ? 2 : 1;
            var navy = CountUnits(entityManager, team, UnitKind.PatrolBoat) +
                       CountUnits(entityManager, team, UnitKind.Battleship) +
                       CountQueued(entityManager, team, UnitKind.PatrolBoat) +
                       CountQueued(entityManager, team, UnitKind.Battleship);

            if (navy >= target)
            {
                return;
            }

            var spec = ProductionCatalog.Get(UnitKind.PatrolBoat);
            if (credits >= spec.Cost + CreditsReserve)
            {
                ProductionCommands.TryEnqueue(entityManager, team, UnitKind.PatrolBoat, Entity.Null, out _);
            }
        }

        private static void UpdateMilitary(
            EntityManager entityManager,
            ref AiCommander commander,
            in AiProfile profile,
            Entity yard)
        {
            var army = CollectArmy(entityManager, commander.Team);
            try
            {
                var yardPosition = entityManager.GetComponentData<LocalTransform>(yard).Position;
                var enemy = FindEnemyTarget(entityManager, commander.Team, yardPosition);

                if (enemy == Entity.Null)
                {
                    commander.Attacking = 0;
                    return;
                }

                if (army.Length == 0)
                {
                    commander.Attacking = 0;
                    return;
                }

                if (commander.Attacking == 0 && army.Length < profile.AttackArmySize)
                {
                    return;
                }

                commander.Attacking = 1;

                for (var i = 0; i < army.Length; i++)
                {
                    var unit = army[i];
                    var state = entityManager.GetComponentData<CombatState>(unit);
                    if (state.Target != Entity.Null)
                    {
                        continue;
                    }

                    var move = entityManager.GetComponentData<MoveTarget>(unit);
                    if (move.HasOrder != 0)
                    {
                        continue;
                    }

                    var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    var target = FindNearestEnemy(entityManager, commander.Team, position);
                    if (target == Entity.Null)
                    {
                        target = enemy;
                    }

                    state.Target = target;
                    state.Explicit = 1;
                    entityManager.SetComponentData(unit, state);
                }
            }
            finally
            {
                army.Dispose();
            }
        }

        private static NativeList<Entity> CollectArmy(EntityManager entityManager, int team)
        {
            var list = new NativeList<Entity>(Allocator.Temp);
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<Weapon>());
            using var units = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < units.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                if (info.Team != team ||
                    entityManager.GetComponentData<Health>(units[i]).Current <= 0f ||
                    entityManager.GetComponentData<Weapon>(units[i]).Damage <= 0f)
                {
                    continue;
                }

                list.Add(units[i]);
            }

            return list;
        }

        private static Entity FindEnemyTarget(
            EntityManager entityManager,
            int team,
            float3 position)
        {
            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<BuildingInfo>(),
                    ComponentType.ReadOnly<UnitInfo>(),
                },
            });
            using var entities = query.ToEntityArray(Allocator.Temp);

            var best = Entity.Null;
            var bestDistanceSq = float.MaxValue;

            // Prefer infrastructure, then fall back to units.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var candidate = entities[i];
                    if (!TryGetTeam(entityManager, candidate, out var candidateTeam) ||
                        MatchRuntime.AreAllied(team, candidateTeam) ||
                        entityManager.GetComponentData<Health>(candidate).Current <= 0f)
                    {
                        continue;
                    }

                    var isBuilding = entityManager.HasComponent<BuildingInfo>(candidate);
                    if ((pass == 0) != isBuilding)
                    {
                        continue;
                    }

                    var candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position;
                    if (!IsVisibleTo(entityManager, team, candidatePosition))
                    {
                        continue;
                    }

                    if (CloakQuery.IsHiddenFrom(entityManager, candidate, team))
                    {
                        continue;
                    }

                    var distanceSq = math.distancesq(position, candidatePosition);
                    if (distanceSq < bestDistanceSq ||
                        (distanceSq == bestDistanceSq && best != Entity.Null && candidate.Index < best.Index))
                    {
                        bestDistanceSq = distanceSq;
                        best = candidate;
                    }
                }

                if (best != Entity.Null)
                {
                    return best;
                }
            }

            return Entity.Null;
        }

        private static Entity FindNearestEnemy(
            EntityManager entityManager,
            int team,
            float3 position)
        {
            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<BuildingInfo>(),
                    ComponentType.ReadOnly<UnitInfo>(),
                },
            });
            using var entities = query.ToEntityArray(Allocator.Temp);

            var best = Entity.Null;
            var bestDistanceSq = float.MaxValue;

            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (!TryGetTeam(entityManager, candidate, out var candidateTeam) ||
                    MatchRuntime.AreAllied(team, candidateTeam) ||
                    entityManager.GetComponentData<Health>(candidate).Current <= 0f)
                {
                    continue;
                }

                var candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position;
                if (!IsVisibleTo(entityManager, team, candidatePosition))
                {
                    continue;
                }

                if (CloakQuery.IsHiddenFrom(entityManager, candidate, team))
                {
                    continue;
                }

                var distanceSq = math.distancesq(position, candidatePosition);
                if (distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq && best != Entity.Null && candidate.Index < best.Index))
                {
                    bestDistanceSq = distanceSq;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsVisibleTo(EntityManager entityManager, int team, float3 position)
        {
            return FogQuery.IsVisible(entityManager, team, position);
        }

        private static bool TryGetTeam(EntityManager entityManager, Entity entity, out int team)
        {
            if (entityManager.HasComponent<UnitInfo>(entity))
            {
                team = entityManager.GetComponentData<UnitInfo>(entity).Team;
                return true;
            }

            if (entityManager.HasComponent<BuildingInfo>(entity))
            {
                team = entityManager.GetComponentData<BuildingInfo>(entity).Team;
                return true;
            }

            team = 0;
            return false;
        }

        private static Entity FindUnit(EntityManager entityManager, int team, UnitKind kind)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>());
            using var units = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < units.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                if (info.Team == team && info.Kind == kind &&
                    entityManager.GetComponentData<Health>(units[i]).Current > 0f)
                {
                    return units[i];
                }
            }

            return Entity.Null;
        }

        private static int CountUnits(EntityManager entityManager, int team, UnitKind kind)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>());
            using var units = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < units.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                if (info.Team == team && info.Kind == kind &&
                    entityManager.GetComponentData<Health>(units[i]).Current > 0f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountQueued(EntityManager entityManager, int team, UnitKind kind)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ProductionTag>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<ProductionOrder>());
            using var buildings = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                if (entityManager.GetComponentData<BuildingInfo>(buildings[i]).Team != team)
                {
                    continue;
                }

                var queue = entityManager.GetBuffer<ProductionOrder>(buildings[i], true);
                for (var q = 0; q < queue.Length; q++)
                {
                    if (queue[q].Kind == kind)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountBuildings(EntityManager entityManager, int team, BuildingKind kind)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingInfo>());
            using var buildings = query.ToEntityArray(Allocator.Temp);

            var count = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                if (info.Team == team && info.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
