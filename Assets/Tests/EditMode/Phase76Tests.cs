using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase76Tests
    {
        [Test]
        public void HeroAbilities_MapToFactions()
        {
            Assert.AreEqual(
                HeroAbilityKind.Overdrive,
                HeroAbilityCatalog.ForFaction(FactionId.Ashfall));
            Assert.AreEqual(
                HeroAbilityKind.Regrowth,
                HeroAbilityCatalog.ForFaction(FactionId.Verdant));
            Assert.AreEqual(
                HeroAbilityKind.Aegis,
                HeroAbilityCatalog.ForFaction(FactionId.Meridian));

            Assert.IsFalse(HeroAbilityCatalog.IsUnlocked(0));
            Assert.IsFalse(HeroAbilityCatalog.IsUnlocked(HeroAbilityCatalog.UnlockKills - 1));
            Assert.IsTrue(HeroAbilityCatalog.IsUnlocked(HeroAbilityCatalog.UnlockKills));
            Assert.AreNotEqual(
                HeroAbilityCatalog.DisplayName(HeroAbilityKind.Overdrive),
                HeroAbilityCatalog.DisplayName(HeroAbilityKind.Aegis));
        }
    }
}
