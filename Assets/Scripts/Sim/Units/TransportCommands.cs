using CQRa.Sim.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Units
{
    public enum TransportFailure : byte
    {
        None = 0,
        NotATransport = 1,
        NotFriendly = 2,
        Full = 3,
        InvalidCargo = 4,
        Empty = 5,
    }

    public static class TransportCommands
    {
        public static bool TryLoad(
            EntityManager entityManager,
            Entity transport,
            Entity unit,
            out TransportFailure failure)
        {
            failure = TransportFailure.None;

            if (!entityManager.Exists(transport) ||
                !entityManager.HasComponent<TransportCargo>(transport) ||
                !entityManager.HasBuffer<CargoUnit>(transport))
            {
                failure = TransportFailure.NotATransport;
                return false;
            }

            if (!entityManager.Exists(unit) ||
                !entityManager.HasComponent<UnitInfo>(unit))
            {
                failure = TransportFailure.InvalidCargo;
                return false;
            }

            var cargo = entityManager.GetComponentData<TransportCargo>(transport);
            var info = entityManager.GetComponentData<UnitInfo>(unit);
            var transportTeam = entityManager.GetComponentData<UnitInfo>(transport).Team;
            if (info.Team != transportTeam)
            {
                failure = TransportFailure.NotFriendly;
                return false;
            }

            var buffer = entityManager.GetBuffer<CargoUnit>(transport);
            var infantry = 0;
            var tanks = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Kind == UnitKind.Tank)
                {
                    tanks++;
                }
                else
                {
                    infantry++;
                }
            }

            var isInfantry = info.Kind == UnitKind.Infantry ||
                             info.Kind == UnitKind.Engineer ||
                             info.Kind == UnitKind.Mantis;
            var isTank = info.Kind == UnitKind.Tank;

            if (isInfantry)
            {
                var cap = tanks > 0 ? cargo.InfantryWithTank : cargo.InfantryCapacity;
                if (infantry >= cap)
                {
                    failure = TransportFailure.Full;
                    return false;
                }
            }
            else if (isTank)
            {
                if (tanks >= cargo.TankCapacity || cargo.TankCapacity == 0 ||
                    (cargo.InfantryWithTank > 0 && infantry > cargo.InfantryWithTank))
                {
                    failure = TransportFailure.Full;
                    return false;
                }
            }
            else
            {
                failure = TransportFailure.InvalidCargo;
                return false;
            }

            var health = entityManager.GetComponentData<Health>(unit);
            buffer.Add(new CargoUnit { Kind = info.Kind, Health = health.Current });
            entityManager.DestroyEntity(unit);
            return true;
        }

        public static int TryUnload(EntityManager entityManager, Entity transport)
        {
            if (!entityManager.Exists(transport) ||
                !entityManager.HasBuffer<CargoUnit>(transport) ||
                !entityManager.HasComponent<UnitInfo>(transport))
            {
                return 0;
            }

            var buffer = entityManager.GetBuffer<CargoUnit>(transport);
            if (buffer.Length == 0)
            {
                return 0;
            }

            var info = entityManager.GetComponentData<UnitInfo>(transport);
            var transform = entityManager.GetComponentData<LocalTransform>(transport);
            var dropHeight = transform.Position.y;
            if (entityManager.HasComponent<AirUnit>(transport))
            {
                dropHeight = transform.Position.y - entityManager.GetComponentData<AirUnit>(transport).Altitude;
            }

            var count = buffer.Length;
            var snapshot = buffer.ToNativeArray(Unity.Collections.Allocator.Temp);
            buffer.Clear();
            for (var i = 0; i < count; i++)
            {
                var cargo = snapshot[i];
                var angle = i * math.PI * 2f / math.max(1, count);
                var offset = new float3(math.cos(angle) * 3f, 0f, math.sin(angle) * 3f);
                var spawn = new float3(transform.Position.x, dropHeight, transform.Position.z) + offset;

                var unit = UnitFactory.SpawnDefault(entityManager, spawn, angle, cargo.Kind, info.Team);
                var health = entityManager.GetComponentData<Health>(unit);
                health.Current = math.clamp(cargo.Health, 1f, health.Max);
                entityManager.SetComponentData(unit, health);
            }

            snapshot.Dispose();
            return count;
        }
    }
}
