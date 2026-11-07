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

            if (count == 0)
            {
                UpdateMatchState(entityManager);
                return;
            }

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

                // Drop targets that died, were removed, or switched sides.
                if (state.Target != Entity.Null && !IsValidTarget(entityManager, state.Target, info.Team))
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
                        allUnits,
                        entity,
                        info.Team,
                        position,
                        weapon.AcquireRange);
                    state.Explicit = 0;
                }

                if (state.Target != Entity.Null)
                {
                    if (targetDistance <= weapon.Range)
                    {
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
                if (entityManager.Exists(dead[i]))
                {
                    entityManager.DestroyEntity(dead[i]);
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
            health.Current -= CombatMath.DamageAfterArmor(weapon.Damage, health.Armor);
            entityManager.SetComponentData(target, health);

            var targetPosition = entityManager.GetComponentData<LocalTransform>(target).Position;
            CombatFeed.AddTracer(position + MuzzleOffset, targetPosition + TargetOffset);

            if (health.Current <= 0f)
            {
                dead.Add(target);
            }
        }

        private static bool IsValidTarget(EntityManager entityManager, Entity target, int team)
        {
            if (target == Entity.Null ||
                !entityManager.Exists(target) ||
                !entityManager.HasComponent<Health>(target) ||
                !entityManager.HasComponent<UnitInfo>(target))
            {
                return false;
            }

            return entityManager.GetComponentData<Health>(target).Current > 0f &&
                   entityManager.GetComponentData<UnitInfo>(target).Team != team;
        }

        private static Entity FindNearestEnemy(
            EntityManager entityManager,
            NativeArray<Entity> units,
            Entity self,
            int team,
            float3 position,
            float acquireRange)
        {
            var best = Entity.Null;
            var bestDistanceSq = acquireRange * acquireRange;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (candidate == self)
                {
                    continue;
                }

                if (!entityManager.HasComponent<Health>(candidate) ||
                    !entityManager.HasComponent<UnitInfo>(candidate))
                {
                    continue;
                }

                if (entityManager.GetComponentData<UnitInfo>(candidate).Team == team ||
                    entityManager.GetComponentData<Health>(candidate).Current <= 0f)
                {
                    continue;
                }

                var candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position;
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

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>());

            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            var team0 = 0;
            var team1 = 0;
            for (var i = 0; i < units.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                if (entityManager.GetComponentData<Health>(units[i]).Current <= 0f)
                {
                    continue;
                }

                if (info.Team == 0)
                {
                    team0++;
                }
                else if (info.Team == 1)
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
