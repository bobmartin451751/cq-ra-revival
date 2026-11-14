using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Advances Sower mine cooldowns and detonates mines when an enemy unit enters the
    /// trigger radius. Blast damage falls off with distance and ignores friendly units.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class MineSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var layerQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<MineLayer>());
            using var layers = layerQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = entityManager.GetComponentData<MineLayer>(layers[i]);
                if (layer.CooldownRemaining > 0f)
                {
                    layer.CooldownRemaining = math.max(0f, layer.CooldownRemaining - dt);
                    entityManager.SetComponentData(layers[i], layer);
                }
            }

            using var mineQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Mine>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var mines = mineQuery.ToEntityArray(Allocator.Temp);
            if (mines.Length == 0)
            {
                return;
            }

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            var dead = new NativeList<Entity>(Allocator.Temp);

            for (var m = 0; m < mines.Length; m++)
            {
                var mineEntity = mines[m];
                if (!entityManager.Exists(mineEntity))
                {
                    continue;
                }

                var mine = entityManager.GetComponentData<Mine>(mineEntity);
                var minePosition = entityManager.GetComponentData<LocalTransform>(mineEntity).Position;
                var triggered = false;

                for (var u = 0; u < units.Length; u++)
                {
                    var unit = units[u];
                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (MatchRuntime.AreAllied(info.Team, mine.Team) ||
                        entityManager.GetComponentData<Health>(unit).Current <= 0f)
                    {
                        continue;
                    }

                    var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    if (math.distance(
                            new float2(minePosition.x, minePosition.z),
                            new float2(position.x, position.z)) <= mine.TriggerRadius)
                    {
                        triggered = true;
                        break;
                    }
                }

                if (!triggered)
                {
                    continue;
                }

                for (var u = 0; u < units.Length; u++)
                {
                    var unit = units[u];
                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (MatchRuntime.AreAllied(info.Team, mine.Team))
                    {
                        continue;
                    }

                    var health = entityManager.GetComponentData<Health>(unit);
                    if (health.Current <= 0f)
                    {
                        continue;
                    }

                    var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    var distance = math.distance(
                        new float2(minePosition.x, minePosition.z),
                        new float2(position.x, position.z));
                    if (distance > mine.BlastRadius)
                    {
                        continue;
                    }

                    var falloff = math.lerp(0.4f, 1f, 1f - distance / mine.BlastRadius);
                    var damage = CombatMath.DamageAfterArmor(mine.Damage * falloff, health.Armor);
                    damage = CombatMath.AbsorbWithShield(entityManager, unit, damage);

                    if (damage <= 0f)
                    {
                        continue;
                    }

                    health.Current -= damage;
                    entityManager.SetComponentData(unit, health);

                    if (health.Current <= 0f)
                    {
                        dead.Add(unit);
                    }
                }

                entityManager.DestroyEntity(mineEntity);
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
    }
}
