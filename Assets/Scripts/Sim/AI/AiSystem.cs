using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Fog;
using CQRa.Sim.Core;
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
            if (ReplayRuntime.IsPlaying)
            {
                return;
            }

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
            MaintainAirForce(entityManager, team, profile, credits);
            MaintainTransportProduction(entityManager, team, credits);
            MaintainResearch(entityManager, team, credits);

            if (TryBuild(entityManager, team, profile, credits, economy))
            {
                return;
            }

            UpdateMilitary(entityManager, ref commander, profile, yard);
            TryFireSuperweapon(entityManager, team);
            MaintainBunkerGarrison(entityManager, commander.Team);
            MaintainTransportMission(entityManager, commander.Team);
            UpdateTransportMissions(entityManager, commander.Team);
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

            if (credits > 1800 &&
                CountBuildings(entityManager, team, BuildingKind.Airfield) == 0)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.Airfield);
            }

            if (credits > 3000 &&
                CountBuildings(entityManager, team, BuildingKind.Superweapon) == 0)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.Superweapon);
            }

            if (credits > 900 &&
                CountBuildings(entityManager, team, BuildingKind.GunTower) < profile.MaxTurrets)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.GunTower);
            }

            if (credits > 700 &&
                CountBuildings(entityManager, team, BuildingKind.Bunker) < 1)
            {
                return BaseCommands.TryAutoPlace(entityManager, team, BuildingKind.Bunker);
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
            if (CountBuildings(entityManager, team, BuildingKind.Refinery) == 0)
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

            if (hasBarracks)
            {
                if (CountUnits(entityManager, team, UnitKind.Medic) +
                    CountQueued(entityManager, team, UnitKind.Medic) == 0 &&
                    army >= 4 &&
                    credits >= ProductionCatalog.Get(UnitKind.Medic).Cost + CreditsReserve)
                {
                    ProductionCommands.TryEnqueue(entityManager, team, UnitKind.Medic, Entity.Null, out _);
                    return;
                }

                if (CountUnits(entityManager, team, UnitKind.AntiTank) +
                    CountQueued(entityManager, team, UnitKind.AntiTank) == 0 &&
                    tanks >= 2 &&
                    credits >= ProductionCatalog.Get(UnitKind.AntiTank).Cost + CreditsReserve)
                {
                    ProductionCommands.TryEnqueue(entityManager, team, UnitKind.AntiTank, Entity.Null, out _);
                    return;
                }

                if (CountUnits(entityManager, team, UnitKind.AntiAir) +
                    CountQueued(entityManager, team, UnitKind.AntiAir) == 0 &&
                    credits >= ProductionCatalog.Get(UnitKind.AntiAir).Cost + CreditsReserve)
                {
                    ProductionCommands.TryEnqueue(entityManager, team, UnitKind.AntiAir, Entity.Null, out _);
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

        private static void MaintainAirForce(
            EntityManager entityManager,
            int team,
            in AiProfile profile,
            int credits)
        {
            if (CountBuildings(entityManager, team, BuildingKind.Airfield) == 0)
            {
                return;
            }

            var roster = ProductionCatalog.RosterForTeam(team);
            var target = profile.MaxFactories >= 2 ? 3 : 2;
            var air = 0;
            for (var i = 0; i < roster.Length; i++)
            {
                if (!AirCatalog.IsAircraft(roster[i]))
                {
                    continue;
                }

                air += CountUnits(entityManager, team, roster[i]);
                air += CountQueued(entityManager, team, roster[i]);
            }

            if (air >= target)
            {
                return;
            }

            var choiceCount = 0;
            for (var i = 0; i < roster.Length; i++)
            {
                if (AirCatalog.IsAircraft(roster[i]) && !AirCatalog.IsTransport(roster[i]))
                {
                    choiceCount++;
                }
            }

            if (choiceCount == 0)
            {
                return;
            }

            var start = air % choiceCount;
            var seen = 0;
            for (var i = 0; i < roster.Length; i++)
            {
                var kind = roster[i];
                if (!AirCatalog.IsAircraft(kind) || AirCatalog.IsTransport(kind))
                {
                    continue;
                }

                if (seen++ < start)
                {
                    continue;
                }

                var spec = ProductionCatalog.Get(kind);
                if (credits >= spec.Cost + CreditsReserve &&
                    ProductionCommands.TryEnqueue(entityManager, team, kind, Entity.Null, out _))
                {
                    return;
                }
            }
        }

        private static void MaintainBunkerGarrison(EntityManager entityManager, int team)
        {
            using var bunkerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Garrison>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var bunkers = bunkerQuery.ToEntityArray(Allocator.Temp);
            if (bunkers.Length == 0)
            {
                return;
            }

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            for (var b = 0; b < bunkers.Length; b++)
            {
                var bunker = bunkers[b];
                var info = entityManager.GetComponentData<BuildingInfo>(bunker);
                if (info.Team != team || !entityManager.HasBuffer<CargoUnit>(bunker))
                {
                    continue;
                }

                var garrison = entityManager.GetComponentData<Garrison>(bunker);
                if (entityManager.GetBuffer<CargoUnit>(bunker).Length >= garrison.Capacity)
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(bunker).Position;
                for (var i = 0; i < units.Length; i++)
                {
                    if (entityManager.GetBuffer<CargoUnit>(bunker).Length >= garrison.Capacity)
                    {
                        break;
                    }

                    var unit = units[i];
                    if (!entityManager.Exists(unit))
                    {
                        continue;
                    }

                    var unitInfo = entityManager.GetComponentData<UnitInfo>(unit);
                    if (unitInfo.Team != team ||
                        !InfantryCatalog.IsInfantry(unitInfo.Kind) ||
                        entityManager.GetComponentData<Health>(unit).Current <= 0f)
                    {
                        continue;
                    }

                    var unitPosition = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    if (math.distance(
                            new float2(position.x, position.z),
                            new float2(unitPosition.x, unitPosition.z)) > 30f)
                    {
                        continue;
                    }

                    GarrisonCommands.TryEnter(entityManager, bunker, unit, out _);
                }
            }
        }

        private static void MaintainTransportProduction(
            EntityManager entityManager,
            int team,
            int credits)
        {
            if (CountBuildings(entityManager, team, BuildingKind.Airfield) == 0)
            {
                return;
            }

            var roster = ProductionCatalog.RosterForTeam(team);
            for (var i = 0; i < roster.Length; i++)
            {
                var kind = roster[i];
                if (!AirCatalog.IsTransport(kind))
                {
                    continue;
                }

                if (CountUnits(entityManager, team, kind) +
                    CountQueued(entityManager, team, kind) > 0)
                {
                    return;
                }

                var spec = ProductionCatalog.Get(kind);
                if (credits >= spec.Cost + CreditsReserve)
                {
                    ProductionCommands.TryEnqueue(entityManager, team, kind, Entity.Null, out _);
                }

                return;
            }
        }

        private static void MaintainTransportMission(EntityManager entityManager, int team)
        {
            var transport = FindTransport(entityManager, team);
            if (transport == Entity.Null)
            {
                return;
            }

            if (entityManager.HasComponent<AiTransportMission>(transport))
            {
                return;
            }

            var position = entityManager.GetComponentData<LocalTransform>(transport).Position;
            var target = FindEnemyTarget(entityManager, team, position);
            if (target == Entity.Null)
            {
                return;
            }

            var buffer = entityManager.GetBuffer<CargoUnit>(transport);
            if (buffer.Length == 0)
            {
                using var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>());
                using var units = query.ToEntityArray(Allocator.Temp);

                var loaded = 0;
                for (var i = 0; i < units.Length && loaded < 5; i++)
                {
                    var unit = units[i];
                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (info.Team != team ||
                        !InfantryCatalog.IsInfantry(info.Kind) ||
                        entityManager.GetComponentData<Health>(unit).Current <= 0f)
                    {
                        continue;
                    }

                    var unitPosition = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    if (math.distance(
                            new float2(position.x, position.z),
                            new float2(unitPosition.x, unitPosition.z)) > 45f)
                    {
                        continue;
                    }

                    if (TransportCommands.TryLoad(entityManager, transport, unit, out _))
                    {
                        loaded++;
                    }
                }

                if (loaded == 0)
                {
                    return;
                }
            }

            var targetPosition = entityManager.GetComponentData<LocalTransform>(target).Position;
            entityManager.AddComponentData(transport, new AiTransportMission
            {
                Target = target,
                Phase = 1,
            });

            var move = entityManager.GetComponentData<MoveTarget>(transport);
            move.Destination = targetPosition;
            move.PathDestination = targetPosition;
            move.Field = Entity.Null;
            move.FormationRadius = 0f;
            move.HasOrder = 1;
            entityManager.SetComponentData(transport, move);
        }

        private static void UpdateTransportMissions(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<AiTransportMission>(),
                ComponentType.ReadOnly<UnitInfo>());
            using var transports = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < transports.Length; i++)
            {
                var transport = transports[i];
                var info = entityManager.GetComponentData<UnitInfo>(transport);
                if (info.Team != team || !entityManager.Exists(transport))
                {
                    continue;
                }

                var mission = entityManager.GetComponentData<AiTransportMission>(transport);
                var position = entityManager.GetComponentData<LocalTransform>(transport).Position;
                var done = mission.Target == Entity.Null ||
                           !entityManager.Exists(mission.Target) ||
                           !entityManager.HasComponent<Health>(mission.Target) ||
                           entityManager.GetComponentData<Health>(mission.Target).Current <= 0f;

                if (!done)
                {
                    var targetPosition = entityManager.GetComponentData<LocalTransform>(mission.Target).Position;
                    if (math.distance(
                            new float2(position.x, position.z),
                            new float2(targetPosition.x, targetPosition.z)) <= 18f)
                    {
                        done = true;
                    }
                }

                if (done)
                {
                    TransportCommands.TryUnload(entityManager, transport);
                    entityManager.RemoveComponent<AiTransportMission>(transport);
                }
            }
        }

        private static Entity FindTransport(EntityManager entityManager, int team)
        {
            var roster = ProductionCatalog.RosterForTeam(team);
            for (var i = 0; i < roster.Length; i++)
            {
                if (!AirCatalog.IsTransport(roster[i]))
                {
                    continue;
                }

                var transport = FindUnit(entityManager, team, roster[i]);
                if (transport != Entity.Null)
                {
                    return transport;
                }
            }

            return Entity.Null;
        }

        private static void MaintainResearch(
            EntityManager entityManager,
            int team,
            int credits)
        {
            if (credits < 1500)
            {
                return;
            }

            var hasState = ResearchQuery.TryGet(entityManager, out _, out var state);
            if (hasState && state.IsActive(team))
            {
                return;
            }

            for (var i = 0; i < ResearchCatalog.Count; i++)
            {
                var kind = (UpgradeKind)i;
                if (state.Has(team, kind))
                {
                    continue;
                }

                var spec = ResearchCatalog.Get(kind);
                if (credits >= spec.Cost + CreditsReserve)
                {
                    ResearchCommands.TryStart(entityManager, team, kind, out _);
                    return;
                }
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
                    if (army.Length == 0)
                    {
                        commander.Attacking = 0;
                    }

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

        /// <summary>
        /// Fires a ready superweapon at the closest visible enemy structure, or the
        /// closest visible enemy unit when no structure is in range. Uses the same
        /// visibility grid and command path as the player.
        /// </summary>
        public static bool TryFireSuperweapon(EntityManager entityManager, int team)
        {
            using var superQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<SuperweaponState>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var superweapons = superQuery.ToEntityArray(Allocator.Temp);

            var launcher = Entity.Null;
            var launcherPosition = float3.zero;
            for (var i = 0; i < superweapons.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(superweapons[i]);
                var state = entityManager.GetComponentData<SuperweaponState>(superweapons[i]);
                if (info.Team == team && info.Kind == BuildingKind.Superweapon && state.Ready != 0)
                {
                    launcher = superweapons[i];
                    launcherPosition = entityManager
                        .GetComponentData<LocalTransform>(superweapons[i]).Position;
                    break;
                }
            }

            if (launcher == Entity.Null)
            {
                return false;
            }

            using var targetQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<BuildingInfo>(),
                },
            });
            using var targets = targetQuery.ToEntityArray(Allocator.Temp);

            var pick = float3.zero;
            var hasPick = false;
            var bestScore = float.MaxValue;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (!TryReadTeam(entityManager, target, out var targetTeam) ||
                    MatchRuntime.AreAllied(team, targetTeam) ||
                    entityManager.GetComponentData<Health>(target).Current <= 0f)
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(target).Position;
                if (!FogQuery.IsVisible(entityManager, team, position))
                {
                    continue;
                }

                var distance = math.distance(position, launcherPosition);
                if (distance > 260f)
                {
                    continue;
                }

                var structureBonus = entityManager.HasComponent<BuildingInfo>(target) ? 0f : 80f;
                var score = distance + structureBonus;
                if (score < bestScore)
                {
                    bestScore = score;
                    pick = position;
                    hasPick = true;
                }
            }

            return hasPick &&
                   SuperweaponCommands.TryFire(entityManager, launcher, pick, out _);
        }

        private static bool TryReadTeam(EntityManager entityManager, Entity entity, out int team)
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

            team = -1;
            return false;
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
