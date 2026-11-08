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

            var has0 = FogQuery.TryGet(entityManager, 0, out var fog0, out var fogInfo);
            var has1 = FogQuery.TryGet(entityManager, 1, out var fog1, out _);
            var cells0 = has0 ? FogQuery.GetCells(entityManager, fog0) : default;
            var cells1 = has1 ? FogQuery.GetCells(entityManager, fog1) : default;

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
                var teamCells = info.Team == 0 ? cells0 : cells1;
                var hasFog = info.Team == 0 ? has0 : has1;

                var target = FindTarget(
                    entityManager,
                    tower,
                    info.Team,
                    position,
                    weapon.Range,
                    hasFog,
                    fogInfo,
                    teamCells);

                if (target != Entity.Null && weapon.CooldownRemaining <= 0f)
                {
                    var targetHealth = entityManager.GetComponentData<Health>(target);
                    targetHealth.Current -= CombatMath.DamageAfterArmor(weapon.Damage, targetHealth.Armor);
                    entityManager.SetComponentData(target, targetHealth);

                    var targetPosition = entityManager.GetComponentData<LocalTransform>(target).Position;
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
            float range,
            bool hasFog,
            in FogGridInfo fogInfo,
            NativeArray<FogCell> fogCells)
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
                if (info.Team == team ||
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

                if (hasFog &&
                    !FogMath.IsVisible(
                        fogCells,
                        fogInfo,
                        new float2(candidatePosition.x, candidatePosition.z)))
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
