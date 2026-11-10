using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Ashfall Rig repair aura: heals nearby friendly mechanical units each fixed tick.
    /// Infantry, engineers and other Rigs are ignored.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class RepairAuraSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var repairerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<RepairAura>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var repairers = repairerQuery.ToEntityArray(Allocator.Temp);
            if (repairers.Length == 0)
            {
                return;
            }

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            for (var r = 0; r < repairers.Length; r++)
            {
                var aura = entityManager.GetComponentData<RepairAura>(repairers[r]);
                var repairerInfo = entityManager.GetComponentData<UnitInfo>(repairers[r]);
                var repairerPosition = entityManager.GetComponentData<LocalTransform>(repairers[r]).Position;
                var radiusSq = aura.Radius * aura.Radius;

                for (var u = 0; u < units.Length; u++)
                {
                    var unit = units[u];
                    if (unit == repairers[r])
                    {
                        continue;
                    }

                    var info = entityManager.GetComponentData<UnitInfo>(unit);
                    if (info.Team != repairerInfo.Team ||
                        !IsMechanical(info.Kind) ||
                        entityManager.GetComponentData<Health>(unit).Current <= 0f)
                    {
                        continue;
                    }

                    var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    if (math.distancesq(
                            new float2(repairerPosition.x, repairerPosition.z),
                            new float2(position.x, position.z)) > radiusSq)
                    {
                        continue;
                    }

                    var health = entityManager.GetComponentData<Health>(unit);
                    if (health.Current >= health.Max)
                    {
                        continue;
                    }

                    health.Current = math.min(health.Max, health.Current + aura.HealPerSecond * dt);
                    entityManager.SetComponentData(unit, health);
                }
            }
        }

        private static bool IsMechanical(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Tank:
                case UnitKind.Harvester:
                case UnitKind.Mcv:
                case UnitKind.Gunship:
                case UnitKind.Thumper:
                case UnitKind.Skyfall:
                case UnitKind.Rig:
                    return true;
                default:
                    return false;
            }
        }
    }
}
