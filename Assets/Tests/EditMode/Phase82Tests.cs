using CQRa.Sim.Content;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase82Tests
    {
        [Test]
        public void ContentAudit_ReportsNoIssues()
        {
            var issues = ContentAudit.ValidateAll();
            Assert.IsEmpty(issues, ContentAudit.Format(issues));
        }

        [Test]
        public void ContentAudit_ChecksMapsMissionsAndFactions()
        {
            Assert.IsEmpty(ContentAudit.ValidateMaps());
            Assert.IsEmpty(ContentAudit.ValidateMissions());
            Assert.IsEmpty(ContentAudit.ValidateFactions());
            Assert.AreEqual(50, MapCatalog.Count);
            Assert.AreEqual(3, CQRa.Sim.Factions.FactionCatalog.All.Length);
        }
    }
}
