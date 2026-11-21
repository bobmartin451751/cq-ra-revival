using System.IO;
using CQRa.Sim.Base;
using CQRa.Sim.Core;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Tests.PlayMode
{
    public sealed class MatchSaveSerializerPlayModeTests
    {
        [Test]
        public void SaveData_RoundTripsThroughJson()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            using var economyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            var economy = economyQuery.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(EconomyState))
                : economyQuery.GetSingletonEntity();
            entityManager.SetComponentData(economy, EconomyState.Create(1234));
            var unit = UnitFactory.SpawnDefault(entityManager, new float3(3f, 0f, 4f), 0f, UnitKind.Tank, 0);
            var building = BuildingFactory.SpawnBuilding(
                entityManager, 1, BuildingKind.Barracks, new float3(-5f, 0f, 2f));
            var path = Path.Combine(Path.GetTempPath(), "cqra-save-test.json");

            try
            {
                var save = MatchSaveSerializer.Capture(entityManager);
                Assert.GreaterOrEqual(save.units.Length, 1);
                Assert.GreaterOrEqual(save.buildings.Length, 1);
                Assert.AreEqual(1234, save.team0Credits);

                var json = MatchSaveSerializer.ToJson(save);
                var loaded = MatchSaveSerializer.FromJson(json);
                Assert.AreEqual(save.mapId, loaded.mapId);
                Assert.AreEqual(save.teamCount, loaded.teamCount);
                Assert.AreEqual(save.units.Length, loaded.units.Length);
                Assert.AreEqual(save.buildings.Length, loaded.buildings.Length);
                Assert.AreEqual(1234, loaded.team0Credits);

                MatchSaveSerializer.SaveToFile(entityManager, path);
                var fromFile = MatchSaveSerializer.LoadFromFile(path);
                Assert.IsNotNull(fromFile);
                Assert.AreEqual(save.units.Length, fromFile.units.Length);
            }
            finally
            {
                if (entityManager.Exists(unit))
                {
                    entityManager.DestroyEntity(unit);
                }

                if (entityManager.Exists(building))
                {
                    entityManager.DestroyEntity(building);
                }

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
    }
}
