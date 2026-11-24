using CQRa.Sim.Factions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    public enum HeroAbilityKind : byte
    {
        Overdrive = 0,
        Regrowth = 1,
        Aegis = 2,
    }

    public struct HeroAbility : IComponentData
    {
        public float Cooldown;
        public float CooldownRemaining;
    }

    /// <summary>Temporary fire-rate buff from the Ashfall hero.</summary>
    public struct WeaponBuff : IComponentData
    {
        public float Remaining;
        public float Multiplier;
    }

    /// <summary>Temporary heal-over-time from the Verdant hero.</summary>
    public struct RegenBuff : IComponentData
    {
        public float Remaining;
        public float PerSecond;
    }

    public static class HeroAbilityCatalog
    {
        public const int UnlockKills = 5;
        public const float Cooldown = 45f;
        public const float Radius = 20f;

        public static HeroAbilityKind ForFaction(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Verdant:
                    return HeroAbilityKind.Regrowth;
                case FactionId.Meridian:
                    return HeroAbilityKind.Aegis;
                default:
                    return HeroAbilityKind.Overdrive;
            }
        }

        public static string DisplayName(HeroAbilityKind kind)
        {
            switch (kind)
            {
                case HeroAbilityKind.Regrowth:
                    return "Regrowth";
                case HeroAbilityKind.Aegis:
                    return "Aegis";
                default:
                    return "Overdrive";
            }
        }

        public static bool IsUnlocked(int kills)
        {
            return kills >= UnlockKills;
        }
    }

    public static class HeroAbilityCommands
    {
        public static bool TryActivate(
            EntityManager entityManager,
            Entity hero,
            out string failure)
        {
            failure = string.Empty;
            if (!entityManager.Exists(hero) ||
                !entityManager.HasComponent<Hero>(hero) ||
                !entityManager.HasComponent<HeroAbility>(hero) ||
                !entityManager.HasComponent<UnitInfo>(hero))
            {
                failure = "Select a campaign hero";
                return false;
            }

            var kills = entityManager.HasComponent<Veterancy>(hero)
                ? entityManager.GetComponentData<Veterancy>(hero).Kills
                : 0;
            if (!HeroAbilityCatalog.IsUnlocked(kills))
            {
                failure = $"Hero needs {HeroAbilityCatalog.UnlockKills} kills";
                return false;
            }

            var ability = entityManager.GetComponentData<HeroAbility>(hero);
            if (ability.CooldownRemaining > 0f)
            {
                failure = $"Ability recharging ({ability.CooldownRemaining:0}s)";
                return false;
            }

            var heroInfo = entityManager.GetComponentData<UnitInfo>(hero);
            var heroPosition = entityManager.GetComponentData<LocalTransform>(hero).Position;
            var faction = FactionRuntime.ForTeam(heroInfo.Team);
            var kind = HeroAbilityCatalog.ForFaction(faction);

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = query.ToEntityArray(Allocator.Temp);
            var affected = 0;

            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                var info = entityManager.GetComponentData<UnitInfo>(unit);
                if (info.Team != heroInfo.Team)
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                if (math.distance(position, heroPosition) > HeroAbilityCatalog.Radius)
                {
                    continue;
                }

                switch (kind)
                {
                    case HeroAbilityKind.Overdrive:
                        if (entityManager.HasComponent<WeaponBuff>(unit))
                        {
                            entityManager.SetComponentData(unit, new WeaponBuff
                            {
                                Remaining = 8f,
                                Multiplier = 0.65f,
                            });
                        }
                        else
                        {
                            entityManager.AddComponentData(unit, new WeaponBuff
                            {
                                Remaining = 8f,
                                Multiplier = 0.65f,
                            });
                        }

                        if (entityManager.HasComponent<Weapon>(unit))
                        {
                            var weapon = entityManager.GetComponentData<Weapon>(unit);
                            weapon.CooldownRemaining = 0f;
                            entityManager.SetComponentData(unit, weapon);
                        }

                        break;

                    case HeroAbilityKind.Regrowth:
                        var health = entityManager.GetComponentData<Health>(unit);
                        health.Current = math.min(health.Max, health.Current + 80f);
                        entityManager.SetComponentData(unit, health);
                        if (entityManager.HasComponent<RegenBuff>(unit))
                        {
                            entityManager.SetComponentData(unit, new RegenBuff
                            {
                                Remaining = 6f,
                                PerSecond = 15f,
                            });
                        }
                        else
                        {
                            entityManager.AddComponentData(unit, new RegenBuff
                            {
                                Remaining = 6f,
                                PerSecond = 15f,
                            });
                        }

                        break;

                    case HeroAbilityKind.Aegis:
                        var shield = new Shield
                        {
                            Max = 80f,
                            Current = 80f,
                            RegenDelay = 3f,
                            TimeSinceHit = 0f,
                        };
                        if (entityManager.HasComponent<Shield>(unit))
                        {
                            entityManager.SetComponentData(unit, shield);
                        }
                        else
                        {
                            entityManager.AddComponentData(unit, shield);
                        }

                        break;
                }

                affected++;
            }

            ability.CooldownRemaining = HeroAbilityCatalog.Cooldown;
            entityManager.SetComponentData(hero, ability);
            return affected > 0;
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class HeroAbilitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityManager = EntityManager;
            var dt = SystemAPI.Time.DeltaTime;

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadWrite<HeroAbility>()))
            using (var heroes = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < heroes.Length; i++)
                {
                    var ability = entityManager.GetComponentData<HeroAbility>(heroes[i]);
                    if (ability.CooldownRemaining > 0f)
                    {
                        ability.CooldownRemaining =
                            math.max(0f, ability.CooldownRemaining - dt);
                        entityManager.SetComponentData(heroes[i], ability);
                    }
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadWrite<WeaponBuff>()))
            using (var buffed = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < buffed.Length; i++)
                {
                    var buff = entityManager.GetComponentData<WeaponBuff>(buffed[i]);
                    buff.Remaining -= dt;
                    if (buff.Remaining <= 0f)
                    {
                        entityManager.RemoveComponent<WeaponBuff>(buffed[i]);
                    }
                    else
                    {
                        entityManager.SetComponentData(buffed[i], buff);
                    }
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadWrite<RegenBuff>()))
            using (var regen = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < regen.Length; i++)
                {
                    var entity = regen[i];
                    var buff = entityManager.GetComponentData<RegenBuff>(entity);
                    buff.Remaining -= dt;
                    if (entityManager.HasComponent<Health>(entity))
                    {
                        var health = entityManager.GetComponentData<Health>(entity);
                        health.Current = math.min(
                            health.Max,
                            health.Current + buff.PerSecond * dt);
                        entityManager.SetComponentData(entity, health);
                    }

                    if (buff.Remaining <= 0f)
                    {
                        entityManager.RemoveComponent<RegenBuff>(entity);
                    }
                    else
                    {
                        entityManager.SetComponentData(entity, buff);
                    }
                }
            }
        }
    }
}
