using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase4Tests
    {
        [Test]
        public void AllKinds_HavePositiveHealth()
        {
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                var stats = CombatStats.For(kind);
                Assert.Greater(stats.MaxHealth, 0f, $"{kind} must have health");
                Assert.That(stats.Armor, Is.InRange(0f, 0.9f), $"{kind} armour must be sane");
            }
        }

        [Test]
        public void Harvester_IsUnarmed()
        {
            var stats = CombatStats.For(UnitKind.Harvester);
            Assert.IsFalse(stats.IsArmed);
            Assert.AreEqual(0f, stats.Damage);
        }

        [Test]
        public void ArmedKinds_HavePositiveRangeAndCooldown()
        {
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                var stats = CombatStats.For(kind);
                if (!stats.IsArmed)
                {
                    continue;
                }

                Assert.Greater(stats.Range, 0f, $"{kind} needs a range");
                Assert.Greater(stats.Cooldown, 0f, $"{kind} needs a cooldown");
                Assert.GreaterOrEqual(stats.AcquireRange, stats.Range, $"{kind} must acquire at least as far as it fires");
            }
        }

        [Test]
        public void DamageAfterArmor_ReducesAndNeverGoesNegative()
        {
            Assert.AreEqual(50f, CombatMath.DamageAfterArmor(50f, 0f), 0.0001f);
            Assert.AreEqual(35f, CombatMath.DamageAfterArmor(50f, 0.3f), 0.0001f);
            Assert.AreEqual(0f, CombatMath.DamageAfterArmor(-5f, 0f), 0.0001f);

            // Armour is clamped, so even an absurd value leaves a little damage.
            Assert.Greater(CombatMath.DamageAfterArmor(50f, 10f), 0f);
        }

        [Test]
        public void ShotsToKill_MatchesArmourMath()
        {
            // Tank vs infantry: 45 damage, no armour, 50 hp -> two shots.
            Assert.AreEqual(2, CombatMath.ShotsToKill(45f, 0f, 50f));

            // Tank vs tank: 45 * (1 - 0.35) = 29.25 per shot, 260 hp -> 9 shots.
            var tank = CombatStats.For(UnitKind.Tank);
            Assert.AreEqual(9, CombatMath.ShotsToKill(tank.Damage, tank.Armor, tank.MaxHealth));

            // Unarmed weapon can never kill.
            Assert.AreEqual(0, CombatMath.ShotsToKill(0f, 0f, 100f));
        }

        [Test]
        public void Tank_OutrangesInfantry()
        {
            var tank = CombatStats.For(UnitKind.Tank);
            var infantry = CombatStats.For(UnitKind.Infantry);

            Assert.Greater(tank.Range, infantry.Range);
            Assert.Greater(tank.MaxHealth, infantry.MaxHealth);
            Assert.Greater(tank.Armor, infantry.Armor);
        }
    }
}
