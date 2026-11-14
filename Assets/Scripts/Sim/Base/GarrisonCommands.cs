using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Base
{
    public enum GarrisonFailure : byte
    {
        None = 0,
        NotGarrisonable = 1,
        NotFriendly = 2,
        Full = 3,
        NotInfantry = 4,
        Empty = 5,
    }

    public static class GarrisonCommands
    {
        public static bool TryEnter(
            EntityManager entityManager,
            Entity building,
            Entity unit,
            out GarrisonFailure failure)
        {
            failure = GarrisonFailure.None;

            if (!entityManager.Exists(building) ||
                !entityManager.HasComponent<Garrison>(building) ||
                !entityManager.HasBuffer<CargoUnit>(building))
            {
                failure = GarrisonFailure.NotGarrisonable;
                return false;
            }

            if (!entityManager.Exists(unit) || !entityManager.HasComponent<UnitInfo>(unit))
            {
                failure = GarrisonFailure.NotInfantry;
                return false;
            }

            var info = entityManager.GetComponentData<UnitInfo>(unit);
            var buildingInfo = entityManager.GetComponentData<BuildingInfo>(building);
            if (info.Team != buildingInfo.Team)
            {
                failure = GarrisonFailure.NotFriendly;
                return false;
            }

            if (info.Kind != UnitKind.Infantry &&
                info.Kind != UnitKind.Engineer &&
                info.Kind != UnitKind.Mantis)
            {
                failure = GarrisonFailure.NotInfantry;
                return false;
            }

            var garrison = entityManager.GetComponentData<Garrison>(building);
            var buffer = entityManager.GetBuffer<CargoUnit>(building);
            if (buffer.Length >= garrison.Capacity)
            {
                failure = GarrisonFailure.Full;
                return false;
            }

            var health = entityManager.GetComponentData<Health>(unit);
            buffer.Add(new CargoUnit { Kind = info.Kind, Health = health.Current });
            entityManager.DestroyEntity(unit);
            UpdateDamage(entityManager, building);
            return true;
        }

        public static int TryEject(EntityManager entityManager, Entity building)
        {
            if (!entityManager.Exists(building) ||
                !entityManager.HasBuffer<CargoUnit>(building) ||
                !entityManager.HasComponent<BuildingInfo>(building))
            {
                return 0;
            }

            var buffer = entityManager.GetBuffer<CargoUnit>(building);
            if (buffer.Length == 0)
            {
                return 0;
            }

            var info = entityManager.GetComponentData<BuildingInfo>(building);
            var transform = entityManager.GetComponentData<LocalTransform>(building);
            var count = buffer.Length;
            var snapshot = buffer.ToNativeArray(Allocator.Temp);
            buffer.Clear();

            for (var i = 0; i < count; i++)
            {
                var cargo = snapshot[i];
                var angle = i * math.PI * 2f / math.max(1, count);
                var spawn = transform.Position +
                            new float3(math.cos(angle) * (info.Radius + 2f), 0f,
                                math.sin(angle) * (info.Radius + 2f));
                var unit = UnitFactory.SpawnDefault(entityManager, spawn, angle, cargo.Kind, info.Team);
                var health = entityManager.GetComponentData<Health>(unit);
                health.Current = math.clamp(cargo.Health, 1f, health.Max);
                entityManager.SetComponentData(unit, health);
            }

            snapshot.Dispose();
            UpdateDamage(entityManager, building);
            return count;
        }

        public static void UpdateDamage(EntityManager entityManager, Entity building)
        {
            if (!entityManager.HasComponent<Garrison>(building) ||
                !entityManager.HasComponent<DefenseWeapon>(building) ||
                !entityManager.HasBuffer<CargoUnit>(building))
            {
                return;
            }

            var garrison = entityManager.GetComponentData<Garrison>(building);
            var count = entityManager.GetBuffer<CargoUnit>(building).Length;
            var weapon = entityManager.GetComponentData<DefenseWeapon>(building);
            weapon.Damage = garrison.BaseDamage + garrison.DamagePerOccupant * count;
            entityManager.SetComponentData(building, weapon);
        }
    }
}
