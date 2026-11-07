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
                target.PathDestination = new float3(40f, 0f, 10f);
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

        [UnityTest]
        public IEnumerator RepeatedOrders_ProduceIdenticalArrival()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 120f, 61, 1f, 2f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f));
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(new[] { material }, new[] { mesh });

            try
            {
                var firstEntity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0, 0,
                    new float3(-20f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(firstEntity);
                Order(entityManager, firstEntity, new float3(20f, 0f, 0f));
                yield return WaitUntilIdle(entityManager, firstEntity, 900);

                var first = entityManager.GetComponentData<LocalTransform>(firstEntity).Position;

                // Remove the first tank so it cannot perturb the re-run.
                entityManager.DestroyEntity(firstEntity);
                owned.RemoveAtSwapBack(0);

                var secondEntity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0, 0,
                    new float3(-20f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(secondEntity);
                Order(entityManager, secondEntity, new float3(20f, 0f, 0f));
                yield return WaitUntilIdle(entityManager, secondEntity, 900);

                var second = entityManager.GetComponentData<LocalTransform>(secondEntity).Position;

                Assert.AreEqual(first.x, second.x, 0.0001f, "repeated orders must be deterministic (x)");
                Assert.AreEqual(first.z, second.z, 0.0001f, "repeated orders must be deterministic (z)");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, mesh);
            }
        }

        [UnityTest]
        public IEnumerator BlockedDestination_SnapsToNearestWalkable()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 120f, 61, 1f, 2f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f));
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(new[] { material }, new[] { mesh });

            try
            {
                // Wall off a 5x5 block around the requested destination.
                for (var z = 58; z <= 62; z++)
                {
                    for (var x = 78; x <= 82; x++)
                    {
                        SetWalkable(entityManager, data, x, z, 0);
                    }
                }

                var entity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0, 0,
                    new float3(-20f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(entity);

                Order(entityManager, entity, new float3(20f, 0f, 0f));

                yield return WaitUntilIdle(entityManager, entity, 900);

                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                var info = entityManager.GetComponentData<NavGridInfo>(data);
                var cell = NavGridMath.WorldToCell(info, new float2(position.x, position.z));

                Assert.AreEqual(
                    (byte)1,
                    GetWalkable(entityManager, data, cell),
                    "unit must stop on a walkable cell");
                Assert.Less(
                    math.distance(new float2(position.x, position.z), new float2(20f, 0f)),
                    10f,
                    "unit should stop beside the blocked destination");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, mesh);
            }
        }

        [UnityTest]
        public IEnumerator RepathMidMove_ReversesDirection()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 120f, 61, 1f, 2f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f));
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(new[] { material }, new[] { mesh });

            try
            {
                var entity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 0, 0,
                    new float3(-20f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(entity);

                Order(entityManager, entity, new float3(40f, 0f, 0f));

                for (var frame = 0; frame < 30; frame++)
                {
                    yield return null;
                }

                Assert.Greater(
                    entityManager.GetComponentData<LocalTransform>(entity).Position.x,
                    -18f,
                    "unit should be moving toward the first destination");

                Order(entityManager, entity, new float3(-40f, 0f, 0f));

                var reversed = false;
                for (var frame = 0; frame < 120 && !reversed; frame++)
                {
                    yield return null;
                    reversed = entityManager.GetComponentData<UnitMotion>(entity).Velocity.x < -0.5f;
                }

                Assert.IsTrue(reversed, "unit should turn around after a new order");

                yield return WaitUntilIdle(entityManager, entity, 900);

                var final = entityManager.GetComponentData<LocalTransform>(entity).Position;
                Assert.Less(final.x, -30f, "unit should arrive near the second destination");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, mesh);
            }
        }

        [UnityTest]
        public IEnumerator ManyUnits_ConvergeWithoutNaNOrBlockedCells()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 140f, 71, 1f, 2f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
            };
            var materials = new Material[UnitMaterials.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            var renderMeshArray = UnitFactory.CreateRenderMeshArray(materials, meshes);
            var destination = new float3(10f, 0f, 0f);

            try
            {
                for (var i = 0; i < 12; i++)
                {
                    var kind = (UnitKind)(i % 3);
                    var entity = UnitFactory.Spawn(
                        entityManager, renderMeshArray, (int)kind,
                        UnitMaterials.BaseIndex(0, kind),
                        new float3(-50f + (i % 4) * 4f, 0f, -12f + (i / 4) * 8f),
                        0f, kind, 0);

                    owned.Add(entity);
                }

                Assert.IsTrue(
                    MoveOrderIssuer.TryIssue(entityManager, owned, destination, out _),
                    "group order should be issued");

                var idle = false;
                for (var frame = 0; frame < 3000 && !idle; frame++)
                {
                    yield return null;
                    idle = true;
                    foreach (var entity in owned)
                    {
                        if (entityManager.GetComponentData<MoveTarget>(entity).HasOrder != 0)
                        {
                            idle = false;
                            break;
                        }
                    }
                }

                var info = entityManager.GetComponentData<NavGridInfo>(data);
                foreach (var entity in owned)
                {
                    var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    Assert.IsFalse(
                        math.any(math.isnan(new float3(position.x, position.y, position.z))),
                        "positions must stay finite");
                    Assert.Less(
                        math.distance(new float2(position.x, position.z), new float2(destination.x, destination.z)),
                        30f,
                        "units should converge near the destination");

                    var cell = NavGridMath.WorldToCell(info, new float2(position.x, position.z));
                    Assert.AreEqual((byte)1, GetWalkable(entityManager, data, cell), "units must stay on walkable cells");
                }

                Assert.IsTrue(idle, "all units should finish their orders");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator ManyUnits_SharedDestination_StillArrives()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 140f, 71, 1f, 2f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
            };
            var materials = new Material[UnitMaterials.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            var renderMeshArray = UnitFactory.CreateRenderMeshArray(materials, meshes);
            var destination = new float3(10f, 0f, 0f);

            try
            {
                for (var i = 0; i < 8; i++)
                {
                    var kind = (UnitKind)(i % 3);
                    var entity = UnitFactory.Spawn(
                        entityManager, renderMeshArray, (int)kind,
                        UnitMaterials.BaseIndex(0, kind),
                        new float3(-40f + (i % 4) * 3f, 0f, -6f + (i / 4) * 6f),
                        0f, kind, 0);

                    owned.Add(entity);
                    Order(entityManager, entity, destination);
                }

                var idle = false;
                for (var frame = 0; frame < 1200 && !idle; frame++)
                {
                    yield return null;
                    idle = true;
                    foreach (var entity in owned)
                    {
                        if (entityManager.GetComponentData<MoveTarget>(entity).HasOrder != 0)
                        {
                            idle = false;
                            break;
                        }
                    }
                }

                Assert.IsTrue(idle, "separation must not keep units outside their arrival radius");

                var info = entityManager.GetComponentData<NavGridInfo>(data);
                foreach (var entity in owned)
                {
                    var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    var cell = NavGridMath.WorldToCell(info, new float2(position.x, position.z));
                    Assert.AreEqual((byte)1, GetWalkable(entityManager, data, cell), "units must stay on walkable cells");
                    Assert.Less(
                        math.distance(new float2(position.x, position.z), new float2(destination.x, destination.z)),
                        20f,
                        "units should stop around the shared destination");
                }
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        private static IEnumerator WaitUntilIdle(EntityManager entityManager, Entity entity, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (entityManager.GetComponentData<MoveTarget>(entity).HasOrder == 0)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static void Order(EntityManager entityManager, Entity entity, float3 destination)
        {
            var target = entityManager.GetComponentData<MoveTarget>(entity);
            target.Destination = destination;
            target.PathDestination = destination;
            target.Field = Entity.Null;
            target.HasOrder = 1;
            entityManager.SetComponentData(entity, target);
        }

        private static void Cleanup(
            EntityManager entityManager,
            Entity data,
            NativeList<Entity> owned,
            Material material,
            params Mesh[] meshes)
        {
            foreach (var entity in owned)
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }
            }

            owned.Dispose();

            if (entityManager.Exists(data))
            {
                entityManager.DestroyEntity(data);
            }

            foreach (var mesh in meshes)
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }

            Object.DestroyImmediate(material);
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
