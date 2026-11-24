using CQRa.Sim.Content;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase73Tests
    {
        [Test]
        public void WeatherSelection_IsStableAndValid()
        {
            var maps = MapCatalog.Maps;
            Assert.Greater(maps.Length, 0);

            for (var i = 0; i < maps.Length; i++)
            {
                var first = WeatherCatalog.ForMap(maps[i].Id);
                var second = WeatherCatalog.ForMap(maps[i].Id);
                Assert.AreEqual(first, second, "weather must be deterministic per map");
                Assert.IsTrue(first <= WeatherKind.Snow);
                Assert.IsNotEmpty(WeatherCatalog.DisplayName(first));
            }

            Assert.AreEqual(WeatherKind.Clear, WeatherCatalog.ForMap(null));
            Assert.AreEqual(WeatherKind.Clear, WeatherCatalog.ForMap(string.Empty));
        }

        [Test]
        public void WeatherSelection_CoversMultipleKinds()
        {
            var clear = false;
            var rain = false;
            var dust = false;
            var snow = false;

            var maps = MapCatalog.Maps;
            for (var i = 0; i < maps.Length; i++)
            {
                switch (WeatherCatalog.ForMap(maps[i].Id))
                {
                    case WeatherKind.Clear: clear = true; break;
                    case WeatherKind.Rain: rain = true; break;
                    case WeatherKind.Dust: dust = true; break;
                    case WeatherKind.Snow: snow = true; break;
                }
            }

            Assert.IsTrue(rain, "the catalogue needs at least one rainy map");
            Assert.IsTrue(dust, "the catalogue needs at least one dusty map");
            Assert.IsTrue(snow, "the catalogue needs at least one snowy map");
            Assert.IsTrue(clear, "some maps should stay clear");
        }
    }
}
