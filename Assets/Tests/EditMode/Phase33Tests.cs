using CQRa.Sim.Base;
using CQRa.Sim.Factions;
using CQRa.View;
using NUnit.Framework;
using UnityEngine;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase33Tests
    {
        [Test]
        public void FactionBuildings_HaveDistinctSilhouettes()
        {
            var ashfall = ProceduralMeshes.FactionBuilding(
                BuildingKind.ConstructionYard, FactionId.Ashfall);
            var verdant = ProceduralMeshes.FactionBuilding(
                BuildingKind.ConstructionYard, FactionId.Verdant);
            var meridian = ProceduralMeshes.FactionBuilding(
                BuildingKind.ConstructionYard, FactionId.Meridian);

            Assert.Greater(ashfall.vertexCount, 0);
            Assert.Greater(verdant.vertexCount, 0);
            Assert.Greater(meridian.vertexCount, 0);

            Assert.AreNotEqual(ashfall.vertexCount, verdant.vertexCount);
            Assert.AreNotEqual(ashfall.vertexCount, meridian.vertexCount);
            Assert.AreNotEqual(verdant.vertexCount, meridian.vertexCount);

            Object.DestroyImmediate(ashfall);
            Object.DestroyImmediate(verdant);
            Object.DestroyImmediate(meridian);
        }

        [Test]
        public void FactionBuildings_CoverEveryKindAndFaction()
        {
            for (var faction = 0; faction < BuildingFactory.FactionVariantCount; faction++)
            {
                for (var kind = 0; kind < BuildingFactory.BuildingKindCount; kind++)
                {
                    var mesh = ProceduralMeshes.FactionBuilding(
                        (BuildingKind)kind, (FactionId)faction);

                    Assert.IsNotNull(mesh, $"missing mesh for faction {faction}, kind {kind}");
                    Assert.Greater(mesh.vertexCount, 0, $"empty mesh for faction {faction}, kind {kind}");
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void MeshIndex_FollowsTheTeamsFaction()
        {
            FactionRuntime.Set(FactionId.Ashfall, FactionId.Verdant);

            try
            {
                Assert.AreEqual(
                    (int)BuildingKind.Refinery,
                    BuildingFactory.MeshIndex(0, BuildingKind.Refinery),
                    "team 0 uses the Ashfall variant");
                Assert.AreEqual(
                    BuildingFactory.BuildingKindCount + (int)BuildingKind.Refinery,
                    BuildingFactory.MeshIndex(1, BuildingKind.Refinery),
                    "team 1 uses the Verdant variant");

                Assert.AreEqual(
                    BuildingFactory.BuildingMeshCount,
                    BuildingFactory.OreMeshIndex,
                    "the ore mesh follows the building variants");
            }
            finally
            {
                FactionRuntime.Reset();
            }
        }
    }
}
