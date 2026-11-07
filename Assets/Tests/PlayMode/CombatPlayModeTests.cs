using System.Collections;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using CQRa.View;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class CombatPlayModeTests
    {
        [UnityTest]
        public IEnumerator AutoAcquisition_KillsIdleEnemy()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 120f, 61, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = Meshes();

            try
            {
                var renderMeshArray = CreateRenderMeshArray(meshes, material);

                var tank = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(-6f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(tank);

                var infantry = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Infantry,
                    UnitMaterials.BaseIndex(1, UnitKind.Infantry),
                    new float3(6f, 0f, 0f), 0f, UnitKind.Infantry, 1);
                owned.Add(infantry);

                yield return WaitUntilDestroyed(entityManager, infantry, 900);

                Assert.IsFalse(entityManager.Exists(infantry), "an idle enemy in range must be destroyed");
                Assert.IsTrue(entityManager.Exists(tank), "the tank should survive infantry fire");
                Assert.Greater(
                    entityManager.GetComponentData<Health>(tank).Current,
                    0f,
                    "survivor health must stay positive");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator FriendlyUnits_NeverDamageEachOther()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 80f, 41, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = Meshes();

            try
            {
                var renderMeshArray = CreateRenderMeshArray(meshes, material);

                var first = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Infantry,
                    UnitMaterials.BaseIndex(0, UnitKind.Infantry),
                    new float3(-4f, 0f, 0f), 0f, UnitKind.Infantry, 0);
                owned.Add(first);

                var second = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Infantry,
                    UnitMaterials.BaseIndex(0, UnitKind.Infantry),
                    new float3(4f, 0f, 0f), 0f, UnitKind.Infantry, 0);
                owned.Add(second);

                for (var frame = 0; frame < 240; frame++)
                {
                    yield return null;
                }

                Assert.IsTrue(entityManager.Exists(first) && entityManager.Exists(second),
                    "friendly units must never kill each other");
                Assert.AreEqual(
                    entityManager.GetComponentData<Health>(first).Max,
                    entityManager.GetComponentData<Health>(first).Current,
                    0.0001f,
                    "friendly fire must not reduce health");
                Assert.AreEqual(
                    entityManager.GetComponentData<Health>(second).Max,
                    entityManager.GetComponentData<Health>(second).Current,
                    0.0001f,
                    "friendly fire must not reduce health");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator ExplicitAttackOrder_ChasesDistantTarget()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 140f, 71, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = Meshes();

            try
            {
                var renderMeshArray = CreateRenderMeshArray(meshes, material);

                var tank = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(-40f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(tank);

                var infantry = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Infantry,
                    UnitMaterials.BaseIndex(1, UnitKind.Infantry),
                    new float3(40f, 0f, 0f), 0f, UnitKind.Infantry, 1);
                owned.Add(infantry);

                // 80 m apart: well outside acquisition range, so only an explicit order moves it.
                OrderAttack(entityManager, tank, infantry);

                yield return WaitUntilDestroyed(entityManager, infantry, 2400);

                Assert.IsFalse(entityManager.Exists(infantry), "the ordered tank must reach and kill the target");
                Assert.Greater(
                    entityManager.GetComponentData<LocalTransform>(tank).Position.x,
                    -20f,
                    "the tank must actually advance across the map");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator TeamWipe_SetsMatchStateWinner()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 120f, 61, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = Meshes();

            try
            {
                var renderMeshArray = CreateRenderMeshArray(meshes, material);

                var tank = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(-8f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(tank);

                var infantry = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Infantry,
                    UnitMaterials.BaseIndex(1, UnitKind.Infantry),
                    new float3(8f, 0f, 0f), 0f, UnitKind.Infantry, 1);
                owned.Add(infantry);

                OrderAttack(entityManager, tank, infantry);

                yield return WaitUntilDestroyed(entityManager, infantry, 900);

                var match = GetMatchState(entityManager);
                Assert.IsTrue(match.HasValue, "match state must exist after combat");
                Assert.AreEqual(1, match.Value.GameOver);
                Assert.AreEqual(0, match.Value.Winner, "red team should win");
                Assert.AreEqual(1, match.Value.Team0Alive);
                Assert.AreEqual(0, match.Value.Team1Alive);
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Harvester_IsNotAcquiredAsThreat_ButCanBeDestroyed()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateWorldData(entityManager, 120f, 61, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var meshes = Meshes();

            try
            {
                var renderMeshArray = CreateRenderMeshArray(meshes, material);

                var tank = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(-6f, 0f, 0f), 0f, UnitKind.Tank, 0);
                owned.Add(tank);

                var harvester = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Harvester,
                    UnitMaterials.BaseIndex(1, UnitKind.Harvester),
                    new float3(6f, 0f, 0f), 0f, UnitKind.Harvester, 1);
                owned.Add(harvester);

                yield return WaitUntilDestroyed(entityManager, harvester, 1200);

                Assert.IsFalse(entityManager.Exists(harvester), "an unarmed harvester is still a valid target");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        private static Mesh[] Meshes()
        {
            return new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
            };
        }

        private static RenderMeshArray CreateRenderMeshArray(Mesh[] meshes, Material material)
        {
            var materials = new Material[UnitMaterials.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            return UnitFactory.CreateRenderMeshArray(materials, meshes);
        }

        private static void OrderAttack(EntityManager entityManager, Entity attacker, Entity target)
        {
            var state = entityManager.GetComponentData<CombatState>(attacker);
            state.Target = target;
            state.Explicit = 1;
            entityManager.SetComponentData(attacker, state);

            var move = entityManager.GetComponentData<MoveTarget>(attacker);
            move.HasOrder = 0;
            move.Field = Entity.Null;
            entityManager.SetComponentData(attacker, move);
        }

        private static MatchState? GetMatchState(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return null;
            }

            return entityManager.GetComponentData<MatchState>(query.GetSingletonEntity());
        }

        private static IEnumerator WaitUntilDestroyed(EntityManager entityManager, Entity entity, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (!entityManager.Exists(entity))
                {
                    yield break;
                }

                yield return null;
            }
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

        private static Entity CreateWorldData(EntityManager entityManager, float size, int resolution, float cellSize)
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
                MaxSlope = 2f,
            });

            var nav = entityManager.AddBuffer<NavCell>(entity);
            nav.ResizeUninitialized(cells * cells);
            for (var i = 0; i < nav.Length; i++)
            {
                nav[i] = new NavCell { Walkable = 1 };
            }

            return entity;
        }
    }
}
