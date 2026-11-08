using CQRa.Sim.Base;
using CQRa.Sim.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Phase 4 combat: acquisition, firing, damage, death and match resolution.
    ///
    /// The simulation is hitscan and tick-based. Idle armed units acquire the closest
    /// enemy inside their acquisition radius; a player attack order is explicit, so the
    /// unit may chase a target that is outside weapon range. There is no randomness,
    /// no physics and no per-frame delta time, so engagements replay identically.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMovementSystem))]
    public partial class CombatSystem : SystemBase
    {
        private const float AutoLeash = 1.15f;

        private static readonly float3 MuzzleOffset = new float3(0f, 1.0f, 0f);
        private static readonly float3 TargetOffset = new float3(0f, 0.8f, 0f);

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadWrite<CombatState>(),
                ComponentType.ReadWrite<Weapon>(),
                ComponentType.ReadWrite<MoveTarget>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var allUnits = unitQuery.ToEntityArray(Allocator.Temp);
            var count = allUnits.Length;

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

            if (count == 0)
            {
                UpdateMatchState(entityManager);
                return;
            }

            var hasFog0 = FogQuery.TryGet(entityManager, 0, out var fog0, out var fogInfo);
            var hasFog1 = FogQuery.TryGet(entityManager, 1, out var fog1, out _);
            var hasFog = hasFog0 && hasFog1;
            var fogTeam0 = hasFog0 ? FogQuery.GetCells(entityManager, fog0) : default;
            var fogTeam1 = hasFog1 ? FogQuery.GetCells(entityManager, fog1) : default;

            var dt = SystemAPI.Time.DeltaTime;
            var dead = new NativeList<Entity>(Allocator.Temp);

            for (var i = 0; i < count; i++)
            {
                var entity = allUnits[i];
                var health = entityManager.GetComponentData<Health>(entity);
                if (health.Current <= 0f)
                {
                    dead.Add(entity);
                    continue;
                }

                var weapon = entityManager.GetComponentData<Weapon>(entity);
                weapon.CooldownRemaining = math.max(0f, weapon.CooldownRemaining - dt);

                if (weapon.Damage <= 0f)
                {
                    entityManager.SetComponentData(entity, weapon);
                    continue;
                }

                var info = entityManager.GetComponentData<UnitInfo>(entity);
                var state = entityManager.GetComponentData<CombatState>(entity);
                var moveTarget = entityManager.GetComponentData<MoveTarget>(entity);
                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;

                // Drop targets that died, were removed, or switched sides. Auto-acquired
                // targets also require current vision; explicit orders may push into fog.
                if (state.Target != Entity.Null &&
                    !IsValidTarget(
                        entityManager,
                        state.Target,
                        info.Team,
                        state.Explicit == 0,
                        hasFog,
                        fogInfo,
                        fogTeam0,
                        fogTeam1))
                {
                    state.Target = Entity.Null;
                    state.Explicit = 0;
                }

                var targetDistance = 0f;
                if (state.Target != Entity.Null)
                {
                    var targetPosition = entityManager.GetComponentData<LocalTransform>(state.Target).Position;
                    targetDistance = math.distance(position, targetPosition);

                    var leash = state.Explicit != 0
                        ? float.MaxValue
                        : weapon.Range * AutoLeash;

                    if (targetDistance > leash)
                    {
                        state.Target = Entity.Null;
                        state.Explicit = 0;
                    }
                }

                // Idle armed units guard their position and pick a target on their own.
                if (state.Target == Entity.Null && moveTarget.HasOrder == 0)
                {
                    state.Target = FindNearestEnemy(
                        entityManager,
                        targets,
                        entity,
                        info.Team,
                        position,
                        weapon.AcquireRange,
                        hasFog,
                        fogInfo,
                        fogTeam0,
                        fogTeam1);
                    state.Explicit = 0;
                }

                if (state.Target != Entity.Null)
                {
                    if (targetDistance <= weapon.Range)
                    {
                        // Stop a chase as soon as the target is in range, otherwise the
                        // unit drives through/past the target while shooting.
                        if (moveTarget.HasOrder != 0)
                        {
                            moveTarget.HasOrder = 0;
                            moveTarget.Field = Entity.Null;
                            entityManager.SetComponentData(entity, moveTarget);
                        }

                        if (weapon.CooldownRemaining <= 0f)
                        {
                            Fire(entityManager, entity, state.Target, position, weapon, ref dead);
                            weapon.CooldownRemaining = weapon.Cooldown;
                        }
                    }
                    else if (state.Explicit != 0 && moveTarget.HasOrder == 0)
                    {
                        // Attack order out of range: walk into range, then re-check next tick.
                        var targetPosition = entityManager.GetComponentData<LocalTransform>(state.Target).Position;
                        IssueChaseOrder(entityManager, entity, moveTarget, targetPosition);
                    }
                }

                entityManager.SetComponentData(entity, weapon);
                entityManager.SetComponentData(entity, state);
            }

            for (var i = 0; i < dead.Length; i++)
            {
                var entity = dead[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                if (entityManager.HasComponent<BuildingTag>(entity))
                {
                    // Buildings free their nav footprint when destroyed.
                    BaseCommands.RemoveBuilding(entityManager, entity);
                }
                else
                {
                    entityManager.DestroyEntity(entity);
                }
            }

            dead.Dispose();
            UpdateMatchState(entityManager);
        }

        private static void Fire(
            EntityManager entityManager,
            Entity attacker,
            Entity target,
            float3 position,
            in Weapon weapon,
            ref NativeList<Entity> dead)
        {
            var health = entityManager.GetComponentData<Health>(target);
            var targetTransform = entityManager.GetComponentData<LocalTransform>(target);
            var directional = CombatMath.DirectionalMultiplier(
                position,
                targetTransform.Position,
                targetTransform.Rotation);
            var attackerKind = entityManager.GetComponentData<UnitInfo>(attacker).Kind;
            var airBonus = CombatMath.AirSuperiorityMultiplier(
                attackerKind,
                entityManager.HasComponent<AirUnit>(target));
            health.Current -= CombatMath.DamageAfterArmor(
                weapon.Damage * directional * airBonus,
                health.Armor);
            entityManager.SetComponentData(target, health);

            var targetPosition = targetTransform.Position;
            var team = entityManager.GetComponentData<UnitInfo>(attacker).Team;
            CombatFeed.AddTracer(position + MuzzleOffset, targetPosition + TargetOffset, team);

            if (health.Current <= 0f)
            {
                dead.Add(target);
            }
        }

        private static bool IsValidTarget(
            EntityManager entityManager,
            Entity target,
            int team,
            bool requireVisible,
            bool hasFog,
            in FogGridInfo fogInfo,
            NativeArray<FogCell> fogTeam0,
            NativeArray<FogCell> fogTeam1)
        {
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasComponent<Health>(target))
            {
                return false;
            }

            if (entityManager.GetComponentData<Health>(target).Current <= 0f ||
                !TryGetTeam(entityManager, target, out var targetTeam) ||
                targetTeam == team)
            {
                return false;
            }

            if (!requireVisible)
            {
                return true;
            }

            var targetPosition = entityManager.GetComponentData<LocalTransform>(target).Position;
            return IsVisibleTo(hasFog, fogInfo, fogTeam0, fogTeam1, team, targetPosition);
        }

        private static bool IsVisibleTo(
            bool hasFog,
            in FogGridInfo info,
            NativeArray<FogCell> team0,
            NativeArray<FogCell> team1,
            int team,
            float3 position)
        {
            if (!hasFog)
            {
                return true;
            }

            var cells = team == 0 ? team0 : team1;
            return FogMath.IsVisible(cells, info, new float2(position.x, position.z));
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

        private static Entity FindNearestEnemy(
            EntityManager entityManager,
            NativeArray<Entity> targets,
            Entity self,
            int team,
            float3 position,
            float acquireRange,
            bool hasFog,
            in FogGridInfo fogInfo,
            NativeArray<FogCell> fogTeam0,
            NativeArray<FogCell> fogTeam1)
        {
            var best = Entity.Null;
            var bestDistanceSq = acquireRange * acquireRange;

            for (var i = 0; i < targets.Length; i++)
            {
                var candidate = targets[i];
                if (candidate == self)
                {
                    continue;
                }

                if (!TryGetTeam(entityManager, candidate, out var candidateTeam) ||
                    candidateTeam == team ||
                    entityManager.GetComponentData<Health>(candidate).Current <= 0f)
                {
                    continue;
                }

                var candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position;
                if (!IsVisibleTo(hasFog, fogInfo, fogTeam0, fogTeam1, team, candidatePosition))
                {
                    continue;
                }

                var distanceSq = math.distancesq(position, candidatePosition);

                // Tie-break on entity index so acquisition is deterministic.
                if (distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq && best != Entity.Null && candidate.Index < best.Index))
                {
                    bestDistanceSq = distanceSq;
                    best = candidate;
                }
            }

            return best;
        }

        private static void IssueChaseOrder(
            EntityManager entityManager,
            Entity entity,
            in MoveTarget moveTarget,
            float3 destination)
        {
            var target = moveTarget;
            target.Destination = destination;
            target.PathDestination = destination;
            target.Field = Entity.Null;
            target.FormationRadius = 0f;
            target.HasOrder = 1;
            entityManager.SetComponentData(entity, target);
        }

        private static void UpdateMatchState(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            var match = query.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(MatchState))
                : query.GetSingletonEntity();

            using var targetQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Health>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<BuildingInfo>(),
                },
            });

            using var entities = targetQuery.ToEntityArray(Allocator.Temp);

            var team0 = 0;
            var team1 = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<Health>(entities[i]).Current <= 0f ||
                    !TryGetTeam(entityManager, entities[i], out var team))
                {
                    continue;
                }

                if (team == 0)
                {
                    team0++;
                }
                else if (team == 1)
                {
                    team1++;
                }
            }

            var state = new MatchState
            {
                Team0Alive = team0,
                Team1Alive = team1,
                GameOver = 0,
                Winner = 255,
            };

            if (team0 > 0 && team1 > 0)
            {
                // Live match (also resets a finished test/restart scenario).
            }
            else if (team0 > 0)
            {
                state.GameOver = 1;
                state.Winner = 0;
            }
            else if (team1 > 0)
            {
                state.GameOver = 1;
                state.Winner = 1;
            }
            else
            {
                state.GameOver = 1;
                state.Winner = 255;
            }

            entityManager.SetComponentData(match, state);
        }
    }
}
