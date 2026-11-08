using CQRa.Sim.AI;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase9Tests
    {
        [Test]
        public void PlayerAndAiTeams_AreStable()
        {
            Assert.AreEqual(0, SkirmishCommands.PlayerTeam);
            Assert.AreEqual(1, SkirmishCommands.AiTeam);
        }

        [Test]
        public void ResultText_MapsTheWinner()
        {
            Assert.AreEqual("VICTORY", SkirmishCommands.ResultText(SkirmishCommands.PlayerTeam));
            Assert.AreEqual("DEFEAT", SkirmishCommands.ResultText(SkirmishCommands.AiTeam));
            Assert.AreEqual("DRAW", SkirmishCommands.ResultText(255));
        }
    }
}
