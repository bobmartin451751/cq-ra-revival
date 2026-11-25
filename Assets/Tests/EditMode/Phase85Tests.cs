using CQRa.Core;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase85Tests
    {
        [Test]
        public void PerformanceStats_AveragesAndWorst()
        {
            var stats = new PerformanceStats(4);
            Assert.AreEqual(0f, stats.AverageMs(), 0.001f);
            Assert.AreEqual(0f, stats.AverageFps(), 0.001f);

            stats.Add(10f);
            stats.Add(20f);
            Assert.AreEqual(15f, stats.AverageMs(), 0.001f);
            Assert.AreEqual(20f, stats.WorstMs(), 0.001f);
            Assert.AreEqual(66.667f, stats.AverageFps(), 0.01f);

            stats.Add(1f);
            stats.Add(3f);
            stats.Add(5f);
            Assert.AreEqual(4, stats.Count);
            Assert.AreEqual(20f, stats.WorstMs(), 0.001f);
            Assert.AreEqual((5f + 20f + 1f + 3f) / 4f, stats.AverageMs(), 0.001f);

            stats.Clear();
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.AverageMs(), 0.001f);
        }

        [Test]
        public void PerformanceStats_ClampsNegativeSamples()
        {
            var stats = new PerformanceStats(2);
            stats.Add(-5f);
            Assert.AreEqual(0f, stats.AverageMs(), 0.001f);
        }
    }
}
