using Unity.Collections;
using Unity.Entities;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Applies veterancy rank-ups after combat. Each rank adds +15% weapon damage and
    /// +10% maximum health (with the new health granted immediately).
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial class VeterancySystem : SystemBase
    {
        private const float DamagePerRank = 1.15f;
        private const float HealthPerRank = 1.10f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<Veterancy>(),
                ComponentType.ReadWrite<Weapon>(),
                ComponentType.ReadWrite<Health>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var veterancy = entityManager.GetComponentData<Veterancy>(entities[i]);
                var targetRank = VeterancyMath.RankForKills(veterancy.Kills);
                if (targetRank <= veterancy.Rank)
                {
                    continue;
                }

                var weapon = entityManager.GetComponentData<Weapon>(entities[i]);
                var health = entityManager.GetComponentData<Health>(entities[i]);

                while (veterancy.Rank < targetRank)
                {
                    veterancy.Rank++;
                    weapon.Damage *= DamagePerRank;

                    var previousMax = health.Max;
                    health.Max *= HealthPerRank;
                    health.Current += health.Max - previousMax;
                }

                entityManager.SetComponentData(entities[i], veterancy);
                entityManager.SetComponentData(entities[i], weapon);
                entityManager.SetComponentData(entities[i], health);
            }
        }
    }
}
