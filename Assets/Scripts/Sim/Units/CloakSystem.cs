using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>Cloaks stationary Mantis units after a delay.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class CloakSystem : SystemBase
    {
        private const float MoveThreshold = 0.2f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<Cloak>(),
                ComponentType.ReadOnly<UnitMotion>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var cloak = entityManager.GetComponentData<Cloak>(entities[i]);
                var moving = entityManager.GetComponentData<UnitMotion>(entities[i]).Speed > MoveThreshold;

                cloak.Timer = moving ? 0f : cloak.Timer + dt;
                cloak.Cloaked = cloak.Timer >= cloak.Delay ? (byte)1 : (byte)0;
                entityManager.SetComponentData(entities[i], cloak);
            }
        }
    }

    /// <summary>Proximity detection for cloaked units.</summary>
    public static class CloakQuery
    {
        public const float DetectionRadius = 10f;

        public static bool IsCloaked(EntityManager entityManager, Entity entity)
        {
            return entityManager.HasComponent<Cloak>(entity) &&
                   entityManager.GetComponentData<Cloak>(entity).Cloaked != 0;
        }

        public static bool IsHiddenFrom(EntityManager entityManager, Entity candidate, int viewerTeam)
        {
            if (!IsCloaked(entityManager, candidate))
            {
                return false;
            }

            if (!entityManager.HasComponent<LocalTransform>(candidate))
            {
                return false;
            }

            var position = entityManager.GetComponentData<LocalTransform>(candidate).Position;
            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<Health>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<CQRa.Sim.Base.BuildingInfo>(),
                },
            });

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var watcher = entities[i];
                if (watcher == candidate ||
                    entityManager.GetComponentData<Health>(watcher).Current <= 0f)
                {
                    continue;
                }

                var team = entityManager.HasComponent<UnitInfo>(watcher)
                    ? entityManager.GetComponentData<UnitInfo>(watcher).Team
                    : entityManager.GetComponentData<CQRa.Sim.Base.BuildingInfo>(watcher).Team;

                if (team != viewerTeam)
                {
                    continue;
                }

                var watcherPosition = entityManager.GetComponentData<LocalTransform>(watcher).Position;
                if (math.distance(
                        new float2(position.x, position.z),
                        new float2(watcherPosition.x, watcherPosition.z)) <= DetectionRadius)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
