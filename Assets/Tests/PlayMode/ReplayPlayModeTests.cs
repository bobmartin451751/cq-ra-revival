using System.IO;
using CQRa.Sim.Base;
using CQRa.Sim.Core;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Tests.PlayMode
{
    public sealed class ReplayPlayModeTests
    {
        [Test]
        public void Replay_RoundTripsThroughDisk_AndRestoresFirstFrame()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            ReplayRuntime.Reset();

            using var economyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            var economy = economyQuery.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(EconomyState))
                : economyQuery.GetSingletonEntity();
            entityManager.SetComponentData(economy, EconomyState.Create(900));
            var unit = UnitFactory.SpawnDefault(
                entityManager, new float3(4f, 0f, 6f), 0f, UnitKind.Tank, 0);
            var path = Path.Combine(Path.GetTempPath(), "cqra-replay-test.dat");

            try
            {
                ReplayRuntime.BeginRecording(
                    "ash-basin", "Ashfall", "Verdant", 2, "FreeForAll", 1, false);
                ReplayRuntime.CaptureFrame(entityManager, 0);
                ReplayRuntime.CaptureFrame(entityManager, 15);

                var data = ReplayRuntime.StopRecording(entityManager);
                Assert.AreEqual(3, data.frames.Length);
                Assert.IsFalse(ReplayRuntime.IsRecording);

                ReplayStorage.SaveToFile(data, path);
                var loaded = ReplayStorage.LoadFromFile(path);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("ash-basin", loaded.mapId);
                Assert.AreEqual(3, loaded.frames.Length);

                var transform = entityManager.GetComponentData<LocalTransform>(unit);
                transform.Position = new float3(50f, 0f, 50f);
                entityManager.SetComponentData(unit, transform);

                Assert.IsTrue(ReplayRuntime.BeginPlayback(loaded));
                ReplayRuntime.TickPlayback(entityManager, 0);
                Assert.IsTrue(ReplayRuntime.IsPlaying, "the second frame is still pending");

                ReplayRuntime.TickPlayback(entityManager, 15);
                Assert.IsTrue(ReplayRuntime.IsPlaying, "the final frame is still pending");

                ReplayRuntime.TickPlayback(entityManager, 30);
                Assert.IsFalse(ReplayRuntime.IsPlaying, "playback must end after the last frame");

                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitTag>());
                using var units = query.ToEntityArray(Allocator.Temp);
                Assert.GreaterOrEqual(units.Length, 1);
                var restoredPosition = entityManager.GetComponentData<LocalTransform>(units[0]).Position;
                Assert.AreEqual(4f, restoredPosition.x, 0.01f);
                Assert.AreEqual(6f, restoredPosition.z, 0.01f);
            }
            finally
            {
                ReplayRuntime.Reset();
                DestroyAll<UnitTag>(entityManager);
                DestroyAll<BuildingTag>(entityManager);
                if (entityManager.Exists(economy))
                {
                    entityManager.SetComponentData(economy, default(EconomyState));
                }
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void DestroyAll<T>(EntityManager entityManager) where T : struct, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
            entities.Dispose();
        }
    }
}
