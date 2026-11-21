using CQRa.Sim.Core;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase60Tests
    {
        [Test]
        public void CommandLog_RecordsAndClears()
        {
            CommandLog.Clear();
            Assert.AreEqual(0, CommandLog.Count);

            CommandLog.Record(new SimCommand
            {
                Tick = 12,
                Team = 0,
                Type = SimCommandType.Move,
                Kind = 3,
                Point = new float3(1f, 0f, 2f),
            });

            Assert.AreEqual(1, CommandLog.Count);
            Assert.AreEqual(SimCommandType.Move, CommandLog.Commands[0].Type);
            Assert.AreEqual(12, CommandLog.Commands[0].Tick);

            CommandLog.Clear();
            Assert.AreEqual(0, CommandLog.Count);
        }

        [Test]
        public void SimClock_AdvancesAndResets()
        {
            SimClock.Reset();
            Assert.AreEqual(0, SimClock.Tick);
            SimClock.Advance();
            SimClock.Advance();
            Assert.AreEqual(2, SimClock.Tick);
            SimClock.Reset();
            Assert.AreEqual(0, SimClock.Tick);
        }
    }
}
