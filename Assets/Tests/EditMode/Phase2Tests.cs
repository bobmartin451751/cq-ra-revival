using CQRa.View;
using NUnit.Framework;
using UnityEngine;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase2Tests
    {
        [Test]
        public void Box_HasTwentyFourVertices_AndThirtySixIndices()
        {
            var mesh = ProceduralMeshes.Box(new Vector3(2f, 3f, 4f));

            try
            {
                Assert.AreEqual(24, mesh.vertexCount);
                Assert.AreEqual(36, mesh.triangles.Length);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Tank_HasMergedParts_AndRealWorldLength()
        {
            var mesh = ProceduralMeshes.Tank();

            try
            {
                Assert.Greater(mesh.vertexCount, 100, "tank should be assembled from several parts");
                Assert.Greater(mesh.bounds.size.z, 6f, "tank should be roughly 7 m long");
                Assert.Less(mesh.bounds.size.z, 10f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Infantry_IsAboutHumanHeight()
        {
            var mesh = ProceduralMeshes.Infantry();

            try
            {
                Assert.Greater(mesh.bounds.size.y, 1.0f);
                Assert.Less(mesh.bounds.size.y, 1.5f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Terrain_CentreIsFlattenedForBaseBuilding()
        {
            var go = new GameObject("TerrainTest");
            var terrain = go.AddComponent<HeightmapTerrain>();

            try
            {
                terrain.Generate();

                Assert.Less(Mathf.Abs(terrain.SampleHeight(0f, 0f)), 1.5f);
                Assert.Less(Mathf.Abs(terrain.SampleHeight(30f, -25f)), 1.5f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Terrain_HeightsStayInRange_AndNormalsPointUp()
        {
            var go = new GameObject("TerrainTest");
            var terrain = go.AddComponent<HeightmapTerrain>();

            try
            {
                terrain.Generate();

                for (var z = -160f; z <= 160f; z += 20f)
                {
                    for (var x = -160f; x <= 160f; x += 20f)
                    {
                        var height = terrain.SampleHeight(x, z);
                        Assert.IsFalse(float.IsNaN(height));
                        Assert.GreaterOrEqual(height, -3f, "basin undulation may dip slightly below sea level");
                        Assert.LessOrEqual(height, terrain.MaxHeight + 3f);
                    }
                }

                var normal = terrain.SampleNormal(110f, 110f);
                Assert.Greater(normal.y, 0f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Terrain_ContainsOnlyInsideMap()
        {
            var go = new GameObject("TerrainTest");
            var terrain = go.AddComponent<HeightmapTerrain>();

            try
            {
                Assert.IsTrue(terrain.Contains(0f, 0f));
                Assert.IsTrue(terrain.Contains(terrain.Size * 0.5f - 0.1f, 0f));
                Assert.IsFalse(terrain.Contains(terrain.Size * 0.5f + 1f, 0f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
