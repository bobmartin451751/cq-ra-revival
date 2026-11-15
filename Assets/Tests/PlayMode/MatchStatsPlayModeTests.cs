using CQRa.Sim.Base;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;

namespace CQRa.Tests.PlayMode
{
    public sealed class MatchStatsPlayModeTests
    {
        [Test]
        public void MatchStats_TracksKillsLossesAndProduction()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var unit = entityManager.CreateEntity();
            entityManager.AddComponentData(unit, new UnitInfo { Team = 1, Kind = UnitKind.Infantry });
            var building = entityManager.CreateEntity();
            entityManager.AddComponentData(building, new BuildingInfo { Team = 1, Kind = BuildingKind.Barracks });

            try
            {
                using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchStats>()))
                {
                    if (!existing.IsEmptyIgnoreFilter)
                    {
                        entityManager.DestroyEntity(existing.GetSingletonEntity());
                    }
                }

                MatchStatsCommands.AddKill(entityManager, 0, unit);
                MatchStatsCommands.AddKill(entityManager, 0, building);
                MatchStatsCommands.AddBuilt(entityManager, 0);

                Assert.IsTrue(MatchStatsCommands.TryRead(entityManager, 0, out var attacker));
                Assert.AreEqual(2, attacker.Kills);
                Assert.AreEqual(1, attacker.BuildingsDestroyed);
                Assert.AreEqual(1, attacker.UnitsBuilt);

                Assert.IsTrue(MatchStatsCommands.TryRead(entityManager, 1, out var victim));
                Assert.AreEqual(2, victim.Losses);
                Assert.AreEqual(1, victim.BuildingsLost);
            }
            finally
            {
                using var statsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchStats>());
                if (!statsQuery.IsEmptyIgnoreFilter)
                {
                    entityManager.DestroyEntity(statsQuery.GetSingletonEntity());
                }

                if (entityManager.Exists(unit))
                {
                    entityManager.DestroyEntity(unit);
                }

                if (entityManager.Exists(building))
                {
                    entityManager.DestroyEntity(building);
                }
            }
        }
    }
}
