using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase18Tests
    {
        [Test]
        public void DirectionalArmor_FrontSideRear()
        {
            var origin = float3.zero;
            var identity = quaternion.identity;

            Assert.AreEqual(
                CombatMath.FrontalMultiplier,
                CombatMath.DirectionalMultiplier(new float3(0f, 0f, 5f), origin, identity),
                0.001f);

            Assert.AreEqual(
                CombatMath.SideMultiplier,
                CombatMath.DirectionalMultiplier(new float3(5f, 0f, 0f), origin, identity),
                0.001f);

            Assert.AreEqual(
                CombatMath.RearMultiplier,
                CombatMath.DirectionalMultiplier(new float3(0f, 0f, -5f), origin, identity),
                0.001f);
        }

        [Test]
        public void DirectionalArmor_FollowsTargetFacing()
        {
            var target = float3.zero;
            var turned = quaternion.RotateY(math.PI);

            // With the target turned around, an attacker in +Z is now behind it.
            Assert.AreEqual(
                CombatMath.RearMultiplier,
                CombatMath.DirectionalMultiplier(new float3(0f, 0f, 5f), target, turned),
                0.001f);

            Assert.AreEqual(
                CombatMath.FrontalMultiplier,
                CombatMath.DirectionalMultiplier(new float3(0f, 0f, -5f), target, turned),
                0.001f);
        }

        [Test]
        public void DirectionalArmor_ScalesEffectiveDamage()
        {
            var frontal = 100f * CombatMath.FrontalMultiplier;
            var rear = 100f * CombatMath.RearMultiplier;

            Assert.AreEqual(75f, frontal, 0.001f);
            Assert.AreEqual(100f, rear, 0.001f);
            Assert.Less(frontal, rear);
        }
    }
}
