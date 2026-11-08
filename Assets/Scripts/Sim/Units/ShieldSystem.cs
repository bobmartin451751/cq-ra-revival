using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Regenerates shields after a quiet period. Damage resets the delay through
    /// <see cref="CombatMath.AbsorbWithShield"/>.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial class ShieldSystem : SystemBase
    {
        private const float RegenPerSecond = 12f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Shield>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var shield = entityManager.GetComponentData<Shield>(entities[i]);
                shield.TimeSinceHit += dt;

                if (shield.TimeSinceHit >= shield.RegenDelay &&
                    shield.Current < shield.Max)
                {
                    shield.Current = math.min(
                        shield.Max,
                        shield.Current + RegenPerSecond * dt);
                    entityManager.SetComponentData(entities[i], shield);
                }
                else
                {
                    entityManager.SetComponentData(entities[i], shield);
                }
            }
        }
    }
}
