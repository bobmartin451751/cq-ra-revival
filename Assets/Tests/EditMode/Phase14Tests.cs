using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase14Tests
    {
        [Test]
        public void Defaults_MatchTheEstablishedValues()
        {
            Assert.AreEqual(350, ProductionCatalog.Get(UnitKind.Tank).Cost);
            Assert.AreEqual(260f, CombatStats.For(UnitKind.Tank).MaxHealth, 0.01f);
            Assert.AreEqual(50, BuildCatalog.Get(BuildingKind.Wall).Cost);
            Assert.AreEqual(4.2f, UnitFactory.RadiusFor(UnitKind.Mcv), 0.01f);
            Assert.AreEqual(40f, VisionCatalog.ForUnit(UnitKind.Tank), 0.01f);
        }

        [Test]
        public void JsonOverrides_FeedEveryAccessor_AndResetRestoresDefaults()
        {
            const string json = @"{
              ""units"": [{
                ""kind"": ""Tank"", ""cost"": 999, ""buildSeconds"": 2.5, ""producer"": ""WarFactory"",
                ""maxHealth"": 1000, ""armor"": 0.5, ""damage"": 88, ""range"": 30,
                ""cooldown"": 0.5, ""acquireRange"": 35, ""speed"": 12, ""radius"": 4,
                ""vision"": 50, ""label"": ""Prototype""
              }],
              ""buildings"": [{
                ""kind"": ""Barracks"", ""cost"": 123, ""powerProduced"": 0, ""powerUsed"": 7,
                ""radius"": 5, ""navRadius"": 5, ""health"": 333, ""vision"": 21
              }]
            }";

            try
            {
                Assert.IsTrue(ContentDatabase.LoadFromJson(json));
                Assert.IsTrue(ContentDatabase.HasLoaded);

                Assert.AreEqual(999, ProductionCatalog.Get(UnitKind.Tank).Cost);
                Assert.AreEqual(2.5f, ProductionCatalog.Get(UnitKind.Tank).Seconds, 0.01f);
                Assert.AreEqual("Prototype", ProductionCatalog.Get(UnitKind.Tank).Label);
                Assert.AreEqual(1000f, CombatStats.For(UnitKind.Tank).MaxHealth, 0.01f);
                Assert.AreEqual(88f, CombatStats.For(UnitKind.Tank).Damage, 0.01f);
                Assert.AreEqual(50f, VisionCatalog.ForUnit(UnitKind.Tank), 0.01f);

                Assert.AreEqual(123, BuildCatalog.Get(BuildingKind.Barracks).Cost);
                Assert.AreEqual(7, BuildCatalog.Get(BuildingKind.Barracks).PowerUsed);
                Assert.AreEqual(333f, ContentDatabase.Building(BuildingKind.Barracks).Health, 0.01f);
            }
            finally
            {
                ContentDatabase.ResetToDefaults();
            }

            Assert.IsFalse(ContentDatabase.HasLoaded);
            Assert.AreEqual(350, ProductionCatalog.Get(UnitKind.Tank).Cost);
            Assert.AreEqual(260f, CombatStats.For(UnitKind.Tank).MaxHealth, 0.01f);
        }

        [Test]
        public void JsonWithUnknownKinds_IsIgnoredSafely()
        {
            const string json = @"{ ""units"": [ { ""kind"": ""Ufo"" } ], ""buildings"": [ { ""kind"": ""Castle"" } ] }";
            try
            {
                Assert.IsTrue(ContentDatabase.LoadFromJson(json));
                Assert.AreEqual(350, ProductionCatalog.Get(UnitKind.Tank).Cost);
            }
            finally
            {
                ContentDatabase.ResetToDefaults();
            }
        }
    }
}
