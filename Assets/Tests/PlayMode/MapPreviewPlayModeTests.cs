using CQRa.Sim.Content;
using CQRa.View;
using NUnit.Framework;
using UnityEngine;

namespace CQRa.Tests.PlayMode
{
    public sealed class MapPreviewPlayModeTests
    {
        [Test]
        public void Preview_IsRenderedFromTerrainData()
        {
            var texture = MapPreviewCache.Get(3);
            Assert.IsNotNull(texture);
            Assert.AreEqual(128, texture.width);
            Assert.AreEqual(128, texture.height);

            var pixels = texture.GetPixels();
            var min = 1f;
            var max = 0f;
            for (var i = 0; i < pixels.Length; i += 7)
            {
                var luma = pixels[i].r * 0.3f + pixels[i].g * 0.6f + pixels[i].b * 0.1f;
                min = Mathf.Min(min, luma);
                max = Mathf.Max(max, luma);
            }

            Assert.Greater(max - min, 0.04f,
                "relief shading must produce visible variation");
            Assert.AreSame(texture, MapPreviewCache.Get(3), "previews are cached");
        }

        [Test]
        public void CustomPreview_MatchesSettings()
        {
            var map = CustomMapFactory.Build(31337, MapSizeTier.Large, true, 6);
            var texture = MapPreviewCache.GetCustom(map);
            Assert.IsNotNull(texture);
            Assert.AreEqual(128, texture.width);
        }
    }
}
