using CQRa.Sim.Bridges;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    public enum SuperweaponKind : byte
    {
        ArtilleryBarrage = 0,
        SporeBloom = 1,
        OrbitalLance = 2,
    }

    public struct SuperweaponState : IComponentData
    {
        public float Charge;
        public float ChargeSeconds;
        public byte Ready;
    }

    /// <summary>Short-lived area strike created by a charged superweapon building.</summary>
    public struct SuperweaponStrike : IComponentData
    {
        public SuperweaponKind Kind;
        public int Team;
        public float3 Point;
        public float Radius;
        public float Damage;
        public int PulsesRemaining;
        public float Timer;
        public float Interval;
    }

    public static class SuperweaponCatalog
    {
        public const float ChargeSeconds = 150f;

        public static SuperweaponKind ForFaction(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant:
                    return SuperweaponKind.SporeBloom;
                case FactionId.Meridian:
                    return SuperweaponKind.OrbitalLance;
                default:
                    return SuperweaponKind.ArtilleryBarrage;
            }
        }

        public static string DisplayName(SuperweaponKind kind)
        {
            switch (kind)
            {
                case SuperweaponKind.SporeBloom:
                    return "Spore Bloom";
                case SuperweaponKind.OrbitalLance:
                    return "Orbital Lance";
                default:
                    return "Ashfall Barrage";
            }
        }

        public static float Radius(SuperweaponKind kind)
        {
            switch (kind)
            {
                case SuperweaponKind.SporeBloom:
                    return 9f;
                case SuperweaponKind.OrbitalLance:
                    return 5f;
                default:
                    return 6f;
            }
        }

        public static float Damage(SuperweaponKind kind)
        {
            switch (kind)
            {
                case SuperweaponKind.SporeBloom:
                    return 70f;
                case SuperweaponKind.OrbitalLance:
                    return 520f;
                default:
                    return 120f;
            }
        }

        public static int Pulses(SuperweaponKind kind)
        {
            switch (kind)
            {
                case SuperweaponKind.SporeBloom:
                    return 8;
                case SuperweaponKind.OrbitalLance:
                    return 1;
                default:
                    return 6;
            }
        }

        public static float Interval(SuperweaponKind kind)
        {
            return kind == SuperweaponKind.SporeBloom ? 1f : 0.5f;
        }
    }

    public static class SuperweaponCommands
    {
        public static bool IsCharged(EntityManager entityManager, Entity building)
        {
            return entityManager.Exists(building) &&
                   entityManager.HasComponent<SuperweaponState>(building) &&
                   entityManager.GetComponentData<SuperweaponState>(building).Ready != 0;
        }

        public static float ChargeFraction(EntityManager entityManager, Entity building)
        {
            if (!entityManager.Exists(building) ||
                !entityManager.HasComponent<SuperweaponState>(building))
            {
                return 0f;
            }

            var state = entityManager.GetComponentData<SuperweaponState>(building);
            return state.ChargeSeconds <= 0f
                ? 1f
                : math.saturate(state.Charge / state.ChargeSeconds);
        }

        public static bool TryFire(
            EntityManager entityManager,
            Entity building,
            float3 point,
            out string failure)
        {
            failure = string.Empty;
            if (!entityManager.Exists(building) ||
                !entityManager.HasComponent<BuildingInfo>(building) ||
                !entityManager.HasComponent<SuperweaponState>(building))
            {
                failure = "Select a superweapon";
                return false;
            }

            var info = entityManager.GetComponentData<BuildingInfo>(building);
            if (info.Kind != BuildingKind.Superweapon)
            {
                failure = "Not a superweapon";
                return false;
            }

            var state = entityManager.GetComponentData<SuperweaponState>(building);
            if (state.Ready == 0)
            {
                failure = "Superweapon is still charging";
                return false;
            }

            var faction = FactionRuntime.ForTeam(info.Team);
            var kind = SuperweaponCatalog.ForFaction(faction);
            var strike = entityManager.CreateEntity(typeof(SuperweaponStrike));
            entityManager.SetComponentData(strike, new SuperweaponStrike
            {
                Kind = kind,
                Team = info.Team,
                Point = point,
                Radius = SuperweaponCatalog.Radius(kind),
                Damage = SuperweaponCatalog.Damage(kind),
                PulsesRemaining = SuperweaponCatalog.Pulses(kind),
                Timer = 0f,
                Interval = SuperweaponCatalog.Interval(kind),
            });

            state.Charge = 0f;
            state.Ready = 0;
            entityManager.SetComponentData(building, state);
            Units.CombatFeed.AddTracer(
                point + new float3(0f, 60f, 0f),
                point,
                info.Team);
            return true;
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class SuperweaponSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using (var chargeQuery = entityManager.CreateEntityQuery(
                       ComponentType.ReadWrite<SuperweaponState>()))
            using (var buildings = chargeQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < buildings.Length; i++)
                {
                    var state = entityManager.GetComponentData<SuperweaponState>(buildings[i]);
                    if (state.ChargeSeconds <= 0f)
                    {
                        state.ChargeSeconds = SuperweaponCatalog.ChargeSeconds;
                    }

                    if (state.Ready == 0)
                    {
                        state.Charge = math.min(state.ChargeSeconds, state.Charge + dt);
                        state.Ready = state.Charge >= state.ChargeSeconds - 0.0001f ? (byte)1 : (byte)0;
                        entityManager.SetComponentData(buildings[i], state);
                    }
                }
            }

            using var strikeQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<SuperweaponStrike>());
            using var strikes = strikeQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < strikes.Length; i++)
            {
                var strike = entityManager.GetComponentData<SuperweaponStrike>(strikes[i]);
                strike.Timer -= dt;
                if (strike.Timer > 0f)
                {
                    entityManager.SetComponentData(strikes[i], strike);
                    continue;
                }

                ApplyPulse(entityManager, ref strike);
                strike.PulsesRemaining--;
                strike.Timer = strike.Interval;

                if (strike.PulsesRemaining <= 0)
                {
                    entityManager.DestroyEntity(strikes[i]);
                }
                else
                {
                    entityManager.SetComponentData(strikes[i], strike);
                }
            }
        }

        private static void ApplyPulse(EntityManager entityManager, ref SuperweaponStrike strike)
        {
            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitInfo>(),
                    ComponentType.ReadOnly<BuildingInfo>(),
                    ComponentType.ReadOnly<BridgeTag>(),
                },
            });

            using var targets = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var radiusSq = strike.Radius * strike.Radius;

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                var position = entityManager.GetComponentData<LocalTransform>(target).Position;
                if (math.distancesq(
                        new float2(position.x, position.z),
                        new float2(strike.Point.x, strike.Point.z)) > radiusSq)
                {
                    continue;
                }

                if (!TryGetTeam(entityManager, target, out var team) ||
                    MatchRuntime.AreAllied(strike.Team, team))
                {
                    continue;
                }

                var health = entityManager.GetComponentData<Health>(target);
                health.Current = math.max(0f, health.Current - strike.Damage);
                entityManager.SetComponentData(target, health);

                if (health.Current > 0f)
                {
                    continue;
                }

                MatchStatsCommands.AddKill(entityManager, strike.Team, target);

                if (entityManager.HasComponent<BuildingInfo>(target))
                {
                    BaseCommands.RemoveBuilding(entityManager, target);
                }
                else if (entityManager.HasComponent<BridgeTag>(target))
                {
                    BridgeSystem.DestroyBridge(entityManager, target);
                }
                else
                {
                    entityManager.DestroyEntity(target);
                }
            }
        }

        private static bool TryGetTeam(EntityManager entityManager, Entity entity, out int team)
        {
            if (entityManager.HasComponent<UnitInfo>(entity))
            {
                team = entityManager.GetComponentData<UnitInfo>(entity).Team;
                return true;
            }

            if (entityManager.HasComponent<BuildingInfo>(entity))
            {
                team = entityManager.GetComponentData<BuildingInfo>(entity).Team;
                return true;
            }

            if (entityManager.HasComponent<BridgeTag>(entity))
            {
                team = BridgeRules.NeutralTeam;
                return true;
            }

            team = -1;
            return false;
        }
    }
}
