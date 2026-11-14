using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Tests.PlayMode
{
    public sealed class TransportPlayModeTests
    {
        [Test]
        public void AttackHelicopter_CarriesFiveSoldiers()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var transport = CreateTransport(entityManager, UnitKind.Gunship);

            try
            {
                for (var i = 0; i < 5; i++)
                {
                    var soldier = CreateSoldier(entityManager);
                    Assert.IsTrue(
                        TransportCommands.TryLoad(entityManager, transport, soldier, out var failure),
                        $"load {i} failed: {failure}");
                }

                var extra = CreateSoldier(entityManager);
                Assert.IsFalse(
                    TransportCommands.TryLoad(entityManager, transport, extra, out var full),
                    "the sixth soldier must not fit");
                Assert.AreEqual(TransportFailure.Full, full);
                Assert.AreEqual(5, entityManager.GetBuffer<CargoUnit>(transport).Length);
                Assert.AreEqual(5, TransportCommands.TryUnload(entityManager, transport));
            }
            finally
            {
                if (entityManager.Exists(transport))
                {
                    entityManager.DestroyEntity(transport);
                }

                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitTag>());
                if (!query.IsEmptyIgnoreFilter)
                {
                    entityManager.DestroyEntity(query.ToEntityArray(Unity.Collections.Allocator.Temp));
                }
            }
        }

        [Test]
        public void HeavyTransport_CarriesTankAndFiveSoldiers()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var transport = CreateTransport(entityManager, UnitKind.AshfallTransport);

            try
            {
                var tank = CreateUnit(entityManager, UnitKind.Tank);
                Assert.IsTrue(TransportCommands.TryLoad(entityManager, transport, tank, out var failure),
                    $"tank load failed: {failure}");

                for (var i = 0; i < 5; i++)
                {
                    var soldier = CreateSoldier(entityManager);
                    Assert.IsTrue(
                        TransportCommands.TryLoad(entityManager, transport, soldier, out failure),
                        $"soldier {i} failed: {failure}");
                }

                var extra = CreateSoldier(entityManager);
                Assert.IsFalse(
                    TransportCommands.TryLoad(entityManager, transport, extra, out failure),
                    "a tank + five soldiers is the mixed limit");
                Assert.AreEqual(TransportFailure.Full, failure);
                Assert.AreEqual(6, entityManager.GetBuffer<CargoUnit>(transport).Length);
            }
            finally
            {
                if (entityManager.Exists(transport))
                {
                    entityManager.DestroyEntity(transport);
                }
            }
        }

        [Test]
        public void HeavyTransport_CarriesTwentySoldiersWithoutATank()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var transport = CreateTransport(entityManager, UnitKind.VerdantTransport);

            try
            {
                for (var i = 0; i < 20; i++)
                {
                    var soldier = CreateSoldier(entityManager);
                    Assert.IsTrue(
                        TransportCommands.TryLoad(entityManager, transport, soldier, out var failure),
                        $"soldier {i} failed: {failure}");
                }

                var extra = CreateSoldier(entityManager);
                Assert.IsFalse(
                    TransportCommands.TryLoad(entityManager, transport, extra, out var full));
                Assert.AreEqual(TransportFailure.Full, full);

                var tank = CreateUnit(entityManager, UnitKind.Tank);
                Assert.IsFalse(
                    TransportCommands.TryLoad(entityManager, transport, tank, out full),
                    "20 soldiers must block loading a tank");
                Assert.AreEqual(TransportFailure.Full, full);
            }
            finally
            {
                if (entityManager.Exists(transport))
                {
                    entityManager.DestroyEntity(transport);
                }
            }
        }

        private static Entity CreateTransport(EntityManager entityManager, UnitKind kind)
        {
            var entity = CreateUnit(entityManager, kind);
            AirCatalog.TryGetCargoRules(
                kind, out var infantry, out var tanks, out var withTank, out var isTransport);
            entityManager.AddComponentData(entity, new TransportCargo
            {
                InfantryCapacity = infantry,
                TankCapacity = tanks,
                InfantryWithTank = withTank,
                IsTransport = (byte)(isTransport ? 1 : 0),
            });
            entityManager.AddBuffer<CargoUnit>(entity);
            return entity;
        }

        private static Entity CreateSoldier(EntityManager entityManager)
        {
            return CreateUnit(entityManager, UnitKind.Infantry);
        }

        private static Entity CreateUnit(EntityManager entityManager, UnitKind kind)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new UnitInfo
            {
                Team = 0,
                Kind = kind,
                Radius = 1f,
                MaxSpeed = 1f,
            });
            entityManager.AddComponentData(entity, new Health
            {
                Max = 100f,
                Current = 100f,
            });
            entityManager.AddComponentData(
                entity,
                LocalTransform.FromPositionRotationScale(
                    float3.zero,
                    quaternion.identity,
                    1f));
            return entity;
        }
    }
}
