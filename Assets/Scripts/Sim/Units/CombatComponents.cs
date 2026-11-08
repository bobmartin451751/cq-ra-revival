using CQRa.Sim.Content;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    /// <summary>Hit points for a mobile unit. Reaches zero -> entity is removed.</summary>
    public struct Health : IComponentData
    {
        public float Max;
        public float Current;
        public float Armor;
    }

    /// <summary>
    /// Hitscan weapon. Damage is applied instantly on a fixed tick; the view reads the
    /// tracer feed for presentation only. Cooldown is measured in simulation seconds.
    /// </summary>
    public struct Weapon : IComponentData
    {
        public float Damage;
        public float Range;
        public float Cooldown;
        public float CooldownRemaining;

        /// <summary>Radius at which an idle unit picks up a target on its own.</summary>
        public float AcquireRange;
    }

    /// <summary>Current engagement state. <see cref="Explicit"/> marks a player attack order.</summary>
    public struct CombatState : IComponentData
    {
        public Entity Target;

        /// <summary>1 when the target came from a right-click attack order (may chase).</summary>
        public byte Explicit;
    }

    /// <summary>Per-team survival summary, written by <see cref="CombatSystem"/>.</summary>
    public struct MatchState : IComponentData
    {
        public int Team0Alive;
        public int Team1Alive;
        public byte GameOver;
        public byte Winner; // 0/1 when a team wins, 255 for a draw or in progress
    }

    /// <summary>Static per-kind combat tuning. One place to balance Phase 4.</summary>
    public readonly struct CombatStats
    {
        public readonly float MaxHealth;
        public readonly float Armor;
        public readonly float Damage;
        public readonly float Range;
        public readonly float Cooldown;
        public readonly float AcquireRange;

        private CombatStats(
            float maxHealth,
            float armor,
            float damage,
            float range,
            float cooldown,
            float acquireRange)
        {
            MaxHealth = maxHealth;
            Armor = armor;
            Damage = damage;
            Range = range;
            Cooldown = cooldown;
            AcquireRange = acquireRange;
        }

        public bool IsArmed => Damage > 0f;

        public static CombatStats For(UnitKind kind)
        {
            var definition = ContentDatabase.Unit(kind);
            return new CombatStats(
                definition.MaxHealth,
                definition.Armor,
                definition.Damage,
                definition.Range,
                definition.Cooldown,
                definition.AcquireRange);
        }
    }

    public static class CombatMath
    {
        public const float FrontalMultiplier = 0.75f;
        public const float SideMultiplier = 0.85f;
        public const float RearMultiplier = 1f;
        public const float AirSuperiorityBonus = 2f;

        /// <summary>Armour reduces incoming damage linearly and is clamped to 0..0.9.</summary>
        public static float DamageAfterArmor(float damage, float armor)
        {
            return math.max(0f, damage) * (1f - math.clamp(armor, 0f, 0.9f));
        }

        /// <summary>
        /// Directional armour: shots into the front arc are reduced most, side shots a
        /// little, rear shots take full damage. The target's rotation defines its facing.
        /// </summary>
        public static float DirectionalMultiplier(
            float3 attackerPosition,
            float3 targetPosition,
            quaternion targetRotation)
        {
            var forward = math.mul(targetRotation, new float3(0f, 0f, 1f));
            forward.y = 0f;
            forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));

            var incoming = attackerPosition - targetPosition;
            incoming.y = 0f;
            incoming = math.normalizesafe(incoming, new float3(0f, 0f, 1f));

            var dot = math.dot(forward, incoming);
            if (dot >= 0.5f)
            {
                return FrontalMultiplier;
            }

            return dot <= -0.5f ? RearMultiplier : SideMultiplier;
        }

        /// <summary>Skyfall fighters deal double damage to other air units.</summary>
        public static float AirSuperiorityMultiplier(UnitKind attackerKind, bool targetIsAir)
        {
            return attackerKind == UnitKind.Skyfall && targetIsAir ? AirSuperiorityBonus : 1f;
        }

        /// <summary>Applies a shield before health, resetting its regeneration delay.</summary>
        public static float AbsorbWithShield(EntityManager entityManager, Entity target, float damage)
        {
            if (!entityManager.HasComponent<Shield>(target))
            {
                return damage;
            }

            var shield = entityManager.GetComponentData<Shield>(target);
            shield.TimeSinceHit = 0f;

            if (shield.Current > 0f && damage > 0f)
            {
                var absorbed = math.min(shield.Current, damage);
                shield.Current -= absorbed;
                damage -= absorbed;
            }

            entityManager.SetComponentData(target, shield);
            return damage;
        }

        /// <summary>Shots needed to destroy a target; 0 when the weapon cannot hurt it.</summary>
        public static int ShotsToKill(float damage, float armor, float health)
        {
            var perShot = DamageAfterArmor(damage, armor);
            if (perShot <= 0f || health <= 0f)
            {
                return 0;
            }

            return (int)math.ceil(health / perShot);
        }
    }
}
