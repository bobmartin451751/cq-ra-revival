using CQRa.Sim.AI;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase8Tests
    {
        [Test]
        public void AiProfiles_ArePositive()
        {
            foreach (AiDifficulty difficulty in System.Enum.GetValues(typeof(AiDifficulty)))
            {
                var profile = AiProfile.For(difficulty);
                Assert.Greater(profile.ThinkInterval, 0f, $"{difficulty} thinks at a real interval");
                Assert.Greater(profile.AttackArmySize, 0, $"{difficulty} needs an attack threshold");
                Assert.Greater(profile.HarvesterTarget, 0);
                Assert.GreaterOrEqual(profile.ArmyCap, profile.AttackArmySize);
                Assert.That(profile.TankRatio, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(profile.MaxBarracks, 1);
                Assert.GreaterOrEqual(profile.MaxFactories, 1);
            }
        }

        [Test]
        public void HarderAi_ThinksFasterAndAttacksWithSmallerArmies()
        {
            var recruit = AiProfile.For(AiDifficulty.Recruit);
            var veteran = AiProfile.For(AiDifficulty.Veteran);
            var hardened = AiProfile.For(AiDifficulty.Hardened);
            var brutal = AiProfile.For(AiDifficulty.Brutal);

            Assert.Less(veteran.ThinkInterval, recruit.ThinkInterval);
            Assert.Less(hardened.ThinkInterval, veteran.ThinkInterval);
            Assert.Less(brutal.ThinkInterval, hardened.ThinkInterval);

            Assert.LessOrEqual(brutal.AttackArmySize, recruit.AttackArmySize);
            Assert.GreaterOrEqual(brutal.ArmyCap, recruit.ArmyCap);
            Assert.GreaterOrEqual(brutal.TankRatio, recruit.TankRatio);
        }
    }
}
