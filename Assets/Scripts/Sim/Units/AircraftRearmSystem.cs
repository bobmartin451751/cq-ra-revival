using CQRa.Sim.Base;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Returns empty fighters/bombers to a friendly Airfield, lands them and refills ammo.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMovementSystem))]
    public partial class AircraftRearmSystem : SystemBase
    {
        private const float LandingDistance = 9f;
        private const float RearmSeconds = 4f;

        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<AircraftAmmo>(),
                ComponentType.ReadWrite<AircraftHome>(),
                ComponentType.ReadWrite<CombatState>(),
                ComponentType.ReadWrite<MoveTarget>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>());

            using var aircraft = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < aircraft.Length; i++)
            {
                var entity = aircraft[i];
                if (entityManager.GetComponentData<Health>(entity).Current <= 0f)
                {
                    continue;
                }

                var ammo = entityManager.GetComponentData<AircraftAmmo>(entity);
                var home = entityManager.GetComponentData<AircraftHome>(entity);

                if (home.Landed != 0)
                {
                    home.RearmTimer -= dt;
                    if (home.RearmTimer <= 0f)
                    {
                        ammo.Current = ammo.Max;
                        home.Landed = 0;
                        home.Returning = 0;
                        entityManager.SetComponentData(entity, ammo);
                        entityManager.SetComponentData(entity, home);
                    }
                    else
                    {
                        entityManager.SetComponentData(entity, home);
                    }

                    continue;
                }

                if (home.Returning == 0 && ammo.Current > 0)
                {
                    continue;
                }

                if (home.Airfield == Entity.Null ||
                    !entityManager.Exists(home.Airfield) ||
                    !entityManager.HasComponent<BuildingInfo>(home.Airfield))
                {
                    home = FindHome(entityManager, entity, home);
                    if (home.Airfield == Entity.Null)
                    {
                        home.Returning = 0;
                        entityManager.SetComponentData(entity, home);
                        continue;
                    }
                }

                home.Returning = 1;
                var airfield = entityManager.GetComponentData<LocalTransform>(home.Airfield).Position;
                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;

                if (math.distance(
                        new float2(position.x, position.z),
                        new float2(airfield.x, airfield.z)) <= LandingDistance)
                {
                    var state = entityManager.GetComponentData<CombatState>(entity);
                    state.Target = Entity.Null;
                    state.Explicit = 0;
                    entityManager.SetComponentData(entity, state);

                    var move = entityManager.GetComponentData<MoveTarget>(entity);
                    move.HasOrder = 0;
                    move.Field = Entity.Null;
                    entityManager.SetComponentData(entity, move);

                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                    transform.Position = new float3(airfield.x, airfield.y + 1.6f, airfield.z);
                    entityManager.SetComponentData(entity, transform);

                    home.Landed = 1;
                    home.RearmTimer = RearmSeconds;
                }
                else
                {
                    var move = entityManager.GetComponentData<MoveTarget>(entity);
                    move.Destination = airfield;
                    move.PathDestination = airfield;
                    move.Field = Entity.Null;
                    move.FormationRadius = 0f;
                    move.HasOrder = 1;
                    entityManager.SetComponentData(entity, move);
                }

                entityManager.SetComponentData(entity, home);
            }
        }

        private static AircraftHome FindHome(EntityManager entityManager, Entity aircraft, AircraftHome home)
        {
            var team = entityManager.GetComponentData<UnitInfo>(aircraft).Team;
            var position = entityManager.GetComponentData<LocalTransform>(aircraft).Position;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var buildings = query.ToEntityArray(Allocator.Temp);

            var best = Entity.Null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < buildings.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                if (info.Team != team || info.Kind != CQRa.Sim.Base.BuildingKind.Airfield)
                {
                    continue;
                }

                var buildingPosition = entityManager.GetComponentData<LocalTransform>(buildings[i]).Position;
                var distance = math.distancesq(position, buildingPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = buildings[i];
                }
            }

            home.Airfield = best;
            return home;
        }
    }
}
