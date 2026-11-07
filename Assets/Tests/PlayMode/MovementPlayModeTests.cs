using System.Collections;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using CQRa.View;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class MovementPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnitFactory_AddsMovementComponents()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            var entityManager = world.EntityManager;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f));
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(new[] { material }, new[] { mesh });

            var entity = Entity.Null;
            try
            {
                entity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0, 0,
                    new float3(0f, 0f, 0f), 0.5f, UnitKind.Tank, 0);

                Assert.IsTrue(entityManager.HasComponent<MoveTarget>(entity));
                Assert.IsTrue(entityManager.HasComponent<UnitMotion>(entity));

                var target = entityManager.GetComponentData<MoveTarget>(entity);
                Assert.AreEqual(0, target.HasOrder, "units start idle");
                Assert.Greater(target.ArriveRadius, 1f);

                var motion = entityManager.GetComponentData<UnitMotion>(entity);
                Assert.AreEqual(0.5f, motion.Yaw, 0.0001f);
            }
            finally
            {
                if (entity != Entity.Null && entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }

                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator Tank_FollowsFlowField_AndStopsAtDestination()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            var entityManager = world.EntityManager;

            var dataEntity = CreateWorldData(entityManager, 120f, 61, 1f, 2f);
            var entities = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f));
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(new[] { material }, new[] { mesh });

            try
            {
                // Start on the left, destination on the right, with a wall in between.
                BlockColumn(entityManager, dataEntity, 30, 40, 110);
                // reopen a gap the unit must find
                for (var z = 70; z < 80; z++)
                {
                    SetWalkable(entityManager, dataEntity, 30, z, 1);
                }

                var entity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0, 0,
                    new float3(-40f, 0f, 20f), 0f, UnitKind.Tank, 0);

                entities.Add(entity);

                var target = entityManager.GetComponentData<MoveTarget>(entity);
                target.Destination = new float3(40f, 0f, 10f);
                target.HasOrder = 1;
                target.Field = Entity.Null;
                entityManager.SetComponentData(entity, target);

                var start = entityManager.GetComponentData<LocalTransform>(entity).Position;
                var reached = false;

                for (var frame = 0; frame < 900 && !reached; frame++)
                {
                    yield return null;

                    var current = entityManager.GetComponentData<MoveTarget>(entity);
                    reached = current.HasOrder == 0;

                    Assert.AreEqual(
                        (byte)1,
                        GetWalkable(entityManager, dataEntity,
                            NavGridMath.WorldToCell(
                                entityManager.GetComponentData<NavGridInfo>(dataEntity),
                                new float2(
                                    entityManager.GetComponentData<LocalTransform>(entity).Position.x,
                                    entityManager.GetComponentData<LocalTransform>(entity).Position.z))),
                        "unit must never stand in a blocked cell");
                }

                var finalTransform = entityManager.GetComponentData<LocalTransform>(entity);
                var distance = math.distance(
                    new float2(finalTransform.Position.x, finalTransform.Position.z),
                    new float2(40f, 10f));

                Assert.IsTrue(reached, "unit should finish its order");
                Assert.Less(distance, 8f, "unit should stop near the destination");
                Assert.Greater(
                    math.distance(new float2(start.x, start.z), new float2(finalTransform.Position.x, finalTransform.Position.z)),
                    20f,
                    "unit should actually travel");
                Assert.AreEqual(0f, finalTransform.Position.y, 0.01f, "flat test terrain keeps units grounded");
            }
            finally
            {
                foreach (var entity in entities)
                {
                    if (entityManager.Exists(entity))
                    {
                        entityManager.DestroyEntity(entity);
                    }
                }

                entities.Dispose();

                if (entityManager.Exists(dataEntity))
                {
                    entityManager.DestroyEntity(dataEntity);
                }

                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>Builds a flat simulation world: 1 m cells with a matching height buffer.</summary>
        private static Entity CreateWorldData(EntityManager entityManager, float size, int resolution, float cellSize, float maxSlope)
        {
            var entity = entityManager.CreateEntity();
            var cells = Mathf.CeilToInt(size / cellSize);

            entityManager.AddComponentData(entity, new TerrainInfo { Resolution = resolution, Size = size });

            var heights = entityManager.AddBuffer<TerrainHeightSample>(entity);
            heights.ResizeUninitialized(resolution * resolution);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = new TerrainHeightSample { Height = 0f };
            }

            entityManager.AddComponentData(entity, new NavGridInfo
            {
                Origin = new float2(-size * 0.5f, -size * 0.5f),
                Size = new int2(cells, cells),
                CellSize = cellSize,
                MaxSlope = maxSlope,
            });

            var nav = entityManager.AddBuffer<NavCell>(entity);
            nav.ResizeUninitialized(cells * cells);
            for (var i = 0; i < nav.Length; i++)
            {
                nav[i] = new NavCell { Walkable = 1 };
            }

            return entity;
        }

        private static void BlockColumn(EntityManager entityManager, Entity data, int x, int z0, int z1)
        {
            for (var z = z0; z < z1; z++)
            {
                SetWalkable(entityManager, data, x, z, 0);
            }
        }

        private static void SetWalkable(EntityManager entityManager, Entity data, int x, int z, byte value)
        {
            var info = entityManager.GetComponentData<NavGridInfo>(data);
            var nav = entityManager.GetBuffer<NavCell>(data);
            nav[z * info.Size.x + x] = new NavCell { Walkable = value };
        }

        private static byte GetWalkable(EntityManager entityManager, Entity data, int2 cell)
        {
            var info = entityManager.GetComponentData<NavGridInfo>(data);
            var nav = entityManager.GetBuffer<NavCell>(data, true);
            return nav[cell.y * info.Size.x + cell.x].Walkable;
        }
    }
}
