using CQRa.Sim.Core;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Tests.PlayMode
{
    public sealed class MatchChecksumPlayModeTests
    {
        [Test]
        public void Checksum_IsStableAndChangesWithState()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var first = UnitFactory.SpawnDefault(
                entityManager, float3.zero, 0f, UnitKind.Tank, 0);
            var second = UnitFactory.SpawnDefault(
                entityManager, new float3(10f, 0f, 0f), 0f, UnitKind.Infantry, 0);

            try
            {
                var a = MatchChecksum.Compute(entityManager);
                var b = MatchChecksum.Compute(entityManager);
                Assert.AreEqual(a, b, "checksum must be deterministic for unchanged state");

                var transform = entityManager.GetComponentData<LocalTransform>(first);
                transform.Position += new float3(5f, 0f, 0f);
                entityManager.SetComponentData(first, transform);

                Assert.AreNotEqual(a, MatchChecksum.Compute(entityManager));
            }
            finally
            {
                if (entityManager.Exists(first))
                {
                    entityManager.DestroyEntity(first);
                }

                if (entityManager.Exists(second))
                {
                    entityManager.DestroyEntity(second);
                }
            }
        }
    }
}
