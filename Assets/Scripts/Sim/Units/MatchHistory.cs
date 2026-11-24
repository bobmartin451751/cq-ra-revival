using System.Collections.Generic;
using CQRa.Sim.Base;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Presentation-only post-match history. Samples economy, army size and kills every
    /// five seconds so the result screen can draw graphs. It never feeds the simulation.
    /// </summary>
    public static class MatchHistory
    {
        public const int SampleTicks = 150; // 5 s at 30 Hz

        public struct Sample
        {
            public int Tick;
            public int Units0;
            public int Units1;
            public int Credits0;
            public int Credits1;
            public int Kills0;
            public int Kills1;
        }

        private static readonly List<Sample> Entries = new List<Sample>(512);

        public static IReadOnlyList<Sample> Samples => Entries;
        public static int Count => Entries.Count;

        public static void Reset()
        {
            Entries.Clear();
        }

        public static void Capture(EntityManager entityManager, int tick)
        {
            var units0 = 0;
            var units1 = 0;
            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<UnitTag>(),
                       ComponentType.ReadOnly<UnitInfo>(),
                       ComponentType.ReadOnly<Health>()))
            using (var units = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < units.Length; i++)
                {
                    if (entityManager.GetComponentData<Health>(units[i]).Current <= 0f)
                    {
                        continue;
                    }

                    var team = entityManager.GetComponentData<UnitInfo>(units[i]).Team;
                    if (team == 0)
                    {
                        units0++;
                    }
                    else if (team == 1)
                    {
                        units1++;
                    }
                }
            }

            var credits0 = 0;
            var credits1 = 0;
            if (BaseCommands.TryGetEconomy(entityManager, out _, out var economy))
            {
                credits0 = Economy.GetCredits(economy, 0);
                credits1 = Economy.GetCredits(economy, 1);
            }

            var kills0 = 0;
            var kills1 = 0;
            if (MatchStatsCommands.TryRead(entityManager, 0, out var stats0))
            {
                kills0 = stats0.Kills;
            }

            if (MatchStatsCommands.TryRead(entityManager, 1, out var stats1))
            {
                kills1 = stats1.Kills;
            }

            Entries.Add(new Sample
            {
                Tick = tick,
                Units0 = units0,
                Units1 = units1,
                Credits0 = credits0,
                Credits1 = credits1,
                Kills0 = kills0,
                Kills1 = kills1,
            });
        }
    }

    [Unity.Entities.UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class MatchHistorySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (Core.SimClock.Tick % MatchHistory.SampleTicks != 0)
            {
                return;
            }

            MatchHistory.Capture(EntityManager, Core.SimClock.Tick);
        }
    }
}
