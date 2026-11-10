using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase26Tests
    {
        [Test]
        public void VeterancyRankThresholds()
        {
            Assert.AreEqual(0, VeterancyMath.RankForKills(0));
            Assert.AreEqual(0, VeterancyMath.RankForKills(1));
            Assert.AreEqual(1, VeterancyMath.RankForKills(2));
            Assert.AreEqual(1, VeterancyMath.RankForKills(4));
            Assert.AreEqual(2, VeterancyMath.RankForKills(5));
            Assert.AreEqual(3, VeterancyMath.RankForKills(9));
            Assert.AreEqual(3, VeterancyMath.RankForKills(40));
        }
    }
}
