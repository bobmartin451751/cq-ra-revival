using CQRa.Sim.AI;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using CQRa.Sim.Bridges;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Tests.PlayMode
{
    public sealed class BridgePlayModeTests
    {
        [Test]
        public void DestroyBridge_BlocksDeck_AndIsIdempotent()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            NavGridInfo info;
            Entity grid;
            Entity bridge;

            using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NavGridInfo>()))
            {
                if (!existing.IsEmptyIgnoreFilter)
                {
                    entityManager.DestroyEntity(existing.GetSingletonEntity());
                }
            }

            grid = entityManager.CreateEntity();
            info = new NavGridInfo
            {
                Origin = new float2(-20f, -20f),
                Size = new int2(20, 20),
                CellSize = 2f,
                MaxSlope = 2f,
            };
            entityManager.AddComponentData(grid, info);
            var nav = entityManager.AddBuffer<NavCell>(grid);
            nav.ResizeUninitialized(400);
            for (var i = 0; i < nav.Length; i++)
            {
                nav[i] = new NavCell { Walkable = 1 };
            }

            bridge = entityManager.CreateEntity(
                typeof(BridgeTag),
                typeof(Bridge),
                typeof(Health),
                typeof(LocalTransform));
            entityManager.SetComponentData(bridge, new Bridge
            {
                Index = 0,
                Centre = float2.zero,
                Yaw = 0f,
                HalfLength = 8f,
                HalfWidth = 4f,
                Level = 0.9f,
                Destroyed = 0,
            });
            entityManager.SetComponentData(bridge, new Health
            {
                Max = BridgeRules.MaxHealth,
                Current = 0f,
            });
            entityManager.SetComponentData(
                bridge,
                LocalTransform.FromPosition(new float3(0f, 0.9f, 0f)));

            try
            {
                BridgeSystem.DestroyBridge(entityManager, bridge);
                Assert.AreEqual(1, entityManager.GetComponentData<Bridge>(bridge).Destroyed);

                var centre = NavGridMath.WorldToCell(info, float2.zero);
                var cells = entityManager.GetBuffer<NavCell>(grid);
                Assert.AreEqual(0, cells[centre.y * info.Size.x + centre.x].Walkable,
                    "destroyed bridge cells become blocked");

                var edge = NavGridMath.WorldToCell(info, new float2(0f, 3f));
                Assert.AreEqual(0, cells[edge.y * info.Size.x + edge.x].Walkable);
                var outside = NavGridMath.WorldToCell(info, new float2(12f, 0f));
                Assert.AreEqual(1, cells[outside.y * info.Size.x + outside.x].Walkable,
                    "cells outside the deck stay walkable");

                BridgeSystem.DestroyBridge(entityManager, bridge);
                Assert.AreEqual(1, entityManager.GetComponentData<Bridge>(bridge).Destroyed);
            }
            finally
            {
                DestroyAll<BridgeTag>(entityManager);
                if (entityManager.Exists(grid))
                {
                    entityManager.DestroyEntity(grid);
                }
                SkirmishCommands.ResetRuntimeState(entityManager);
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
