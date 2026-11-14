using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    /// <summary>
    /// Hitscan defensive towers. Each gun tower acquires the nearest visible enemy unit in
    /// range and fires on its own cooldown. Fog-aware, so turrets cannot shoot through the
    /// shroud any more than units can.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FogSystem))]
    public partial class DefenseSystem : SystemBase
    {
        private static readonly float3 MuzzleOffset = new float3(0f, 4.2f, 0f);
        private static readonly float3 TargetOffset = new float3(0f, 0.8f, 0f);

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var towerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DefenseWeapon>(),
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var towers = towerQuery.ToEntityArray(Allocator.Temp);
            if (towers.Length == 0)
            {
                return;
            }

            var dead = new NativeList<Entity>(Allocator.Temp);

            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                var health = entityManager.GetComponentData<Health>(tower);
                if (health.Current <= 0f)
                {
                    continue;
                }

                var weapon = entityManager.GetComponentData<DefenseWeapon>(tower);
                weapon.CooldownRemaining = math.max(0f, weapon.CooldownRemaining - dt);

                var info = entityManager.GetComponentData<BuildingInfo>(tower);
                var position = entityManager.GetComponentData<LocalTransform>(tower).Position;

                var target = FindTarget(
                    entityManager,
                    tower,
                    info.Team,
                    position,
                    weapon.Range);

                if (target != Entity.Null && weapon.CooldownRemaining <= 0f)
                {
                    var targetHealth = entityManager.GetComponentData<Health>(target);
                    var targetTransform = entityManager.GetComponentData<LocalTransform>(target);
                    var directional = CombatMath.DirectionalMultiplier(
                        position,
                        targetTransform.Position,
                        targetTransform.Rotation);
                    var damage = CombatMath.DamageAfterArmor(
                        weapon.Damage * directional,
                        targetHealth.Armor);
                    damage = CombatMath.AbsorbWithShield(entityManager, target, damage);

                    if (damage > 0f)
                    {
                        targetHealth.Current -= damage;
                        entityManager.SetComponentData(target, targetHealth);
                    }

                    var targetPosition = targetTransform.Position;
                    CombatFeed.AddTracer(
                        position + MuzzleOffset,
                        targetPosition + TargetOffset,
                        info.Team);

                    weapon.CooldownRemaining = weapon.Cooldown;

                    if (targetHealth.Current <= 0f)
                    {
                        dead.Add(target);
                    }
                }

                entityManager.SetComponentData(tower, weapon);
            }

            for (var i = 0; i < dead.Length; i++)
            {
                if (entityManager.Exists(dead[i]))
                {
                    entityManager.DestroyEntity(dead[i]);
                }
            }

            dead.Dispose();
        }

        private static Entity FindTarget(
            EntityManager entityManager,
            Entity self,
            int team,
            float3 position,
            float range)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var units = query.ToEntityArray(Allocator.Temp);
            var best = Entity.Null;
            var bestDistanceSq = range * range;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (candidate == self)
                {
                    continue;
                }

                var info = entityManager.GetComponentData<UnitInfo>(candidate);
                if (MatchRuntime.AreAllied(info.Team, team) ||
                    entityManager.GetComponentData<Health>(candidate).Current <= 0f)
                {
                    continue;
                }

                var candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position;
                var distanceSq = math.distancesq(position, candidatePosition);
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                if (!FogQuery.IsVisible(entityManager, team, candidatePosition))
                {
                    continue;
                }

                if (distanceSq < bestDistanceSq ||
                    (distanceSq == bestDistanceSq && best != Entity.Null && candidate.Index < best.Index))
                {
                    bestDistanceSq = distanceSq;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
