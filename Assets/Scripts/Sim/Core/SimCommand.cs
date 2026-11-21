using System.Collections.Generic;
using CQRa.Sim.Units;
using Unity.Entities;
using Unity.Mathematics;

namespace CQRa.Sim.Core
{
    public enum SimCommandType : byte
    {
        Build = 0,
        Sell = 1,
        Repair = 2,
        Produce = 3,
        Move = 4,
        Attack = 5,
        Capture = 6,
        Deploy = 7,
        Garrison = 8,
        Ungarrison = 9,
        LoadTransport = 10,
        UnloadTransport = 11,
        Research = 12,
        Superweapon = 13,
    }

    /// <summary>One deterministic player/AI command. Stable IDs are entity IDs as ints.</summary>
    public struct SimCommand
    {
        public int Tick;
        public int Team;
        public SimCommandType Type;
        public Entity Actor;
        public Entity Target;
        public int Kind;
        public float3 Point;
    }

    public static class CommandLog
    {
        private static readonly List<SimCommand> Entries = new List<SimCommand>();

        public static IReadOnlyList<SimCommand> Commands => Entries;
        public static int Count => Entries.Count;

        public static void Record(in SimCommand command)
        {
            Entries.Add(command);
        }

        public static void Clear()
        {
            Entries.Clear();
        }
    }

    public static class SimClock
    {
        public static int Tick { get; private set; }

        public static void Advance()
        {
            Tick++;
        }

        public static void Reset()
        {
            Tick = 0;
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class SimClockSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            SimClock.Advance();
        }
    }

    /// <summary>
    /// Cheap deterministic state fingerprint for replay/desync checks.
    /// Ordering is by entity index and stable component fields only.
    /// </summary>
    public static class MatchChecksum
    {
        public static uint Compute(EntityManager entityManager)
        {
            var hash = 2166136261u;

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<UnitInfo>(),
                       ComponentType.ReadOnly<Health>(),
                       ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
            using (var units = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < units.Length; i++)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                    var health = entityManager.GetComponentData<Health>(units[i]);
                    var position = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(units[i]).Position;
                    hash = Mix(hash, units[i].Index);
                    hash = Mix(hash, info.Team);
                    hash = Mix(hash, (int)info.Kind);
                    hash = Mix(hash, (int)math.round(health.Current * 10f));
                    hash = Mix(hash, (int)math.round(position.x * 10f));
                    hash = Mix(hash, (int)math.round(position.z * 10f));
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<CQRa.Sim.Base.BuildingInfo>(),
                       ComponentType.ReadOnly<Health>(),
                       ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
            using (var buildings = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < buildings.Length; i++)
                {
                    var info = entityManager.GetComponentData<CQRa.Sim.Base.BuildingInfo>(buildings[i]);
                    var health = entityManager.GetComponentData<Health>(buildings[i]);
                    var position = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(buildings[i]).Position;
                    hash = Mix(hash, buildings[i].Index);
                    hash = Mix(hash, info.Team);
                    hash = Mix(hash, (int)info.Kind);
                    hash = Mix(hash, (int)math.round(health.Current));
                    hash = Mix(hash, (int)math.round(position.x));
                    hash = Mix(hash, (int)math.round(position.z));
                }
            }

            return hash;
        }

        private static uint Mix(uint hash, int value)
        {
            hash ^= (uint)value;
            hash *= 16777619u;
            return hash;
        }
    }
}
