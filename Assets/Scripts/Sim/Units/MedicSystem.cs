using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>Medics slowly heal nearby friendly infantry. Vehicles/buildings are ignored.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial class MedicSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var medicQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MedicAura>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var medics = medicQuery.ToEntityArray(Allocator.Temp);
            if (medics.Length == 0)
            {
                return;
            }

            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);

            for (var m = 0; m < medics.Length; m++)
            {
                var medic = medics[m];
                if (entityManager.GetComponentData<Health>(medic).Current <= 0f)
                {
                    continue;
                }

                var info = entityManager.GetComponentData<UnitInfo>(medic);
                var aura = entityManager.GetComponentData<MedicAura>(medic);
                var position = entityManager.GetComponentData<LocalTransform>(medic).Position;
                var radiusSq = aura.Radius * aura.Radius;

                for (var i = 0; i < units.Length; i++)
                {
                    var unit = units[i];
                    if (unit == medic)
                    {
                        continue;
                    }

                    var other = entityManager.GetComponentData<UnitInfo>(unit);
                    if (other.Team != info.Team || !InfantryCatalog.IsInfantry(other.Kind))
                    {
                        continue;
                    }

                    var health = entityManager.GetComponentData<Health>(unit);
                    if (health.Current <= 0f || health.Current >= health.Max)
                    {
                        continue;
                    }

                    var unitPosition = entityManager.GetComponentData<LocalTransform>(unit).Position;
                    if (math.distancesq(
                            new float2(position.x, position.z),
                            new float2(unitPosition.x, unitPosition.z)) > radiusSq)
                    {
                        continue;
                    }

                    health.Current = math.min(health.Max, health.Current + aura.HealPerSecond * dt);
                    entityManager.SetComponentData(unit, health);
                }
            }
        }
    }
}
