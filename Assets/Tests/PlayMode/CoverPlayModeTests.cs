using CQRa.Sim.Combat;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class CoverPlayModeTests
    {
        [Test]
        public void RidgeBetweenInfantryAndAttacker_ReducesDamage()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var terrain = CreateTerrain(entityManager, ridge: true, out var info);

            try
            {
                var heights = entityManager.GetBuffer<TerrainHeightSample>(terrain, true);
                var covered = CoverRules.TerrainMultiplier(
                    heights.AsNativeArray(),
                    info,
                    float3.zero,
                    new float3(10f, 0f, 0f));

                var flat = CoverRules.TerrainMultiplier(
                    heights.AsNativeArray(),
                    info,
                    new float3(-8f, 0f, 0f),
                    new float3(-18f, 0f, 0f));

                Assert.Less(covered, 1f, "a ridge on the firing line must grant cover");
                Assert.AreEqual(1f, flat, 0.001f, "flat ground grants no cover");

                Assert.AreEqual(1f,
                    CoverRules.Multiplier(entityManager, float3.zero, new float3(10f, 0f, 0f), false),
                    0.001f,
                    "vehicles never receive infantry cover");
            }
            finally
            {
                if (entityManager.Exists(terrain))
                {
                    entityManager.DestroyEntity(terrain);
                }
            }
        }

        private static Entity CreateTerrain(EntityManager entityManager, bool ridge, out TerrainInfo info)
        {
            const int resolution = 41;
            const float size = 80f;
            var entity = entityManager.CreateEntity();
            info = new TerrainInfo { Resolution = resolution, Size = size };
            entityManager.AddComponentData(entity, info);
            var heights = entityManager.AddBuffer<TerrainHeightSample>(entity);
            heights.ResizeUninitialized(resolution * resolution);

            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var worldX = x / (float)(resolution - 1) * size - size * 0.5f;
                    var height = ridge && worldX >= 3f && worldX <= 6f ? 3f : 0f;
                    heights[z * resolution + x] = new TerrainHeightSample { Height = height };
                }
            }

            return entity;
        }
    }
}
