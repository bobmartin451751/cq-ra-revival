using System.Collections;
using CQRa.Sim.Base;
using CQRa.Sim.Content;
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
    public sealed class BaseEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator AutoBuildBase_DeploysAndPlacesStructures()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 180f, 91, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var economy = CreateEconomy(entityManager, 3000, 3000);

                var mcv = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 3,
                    UnitMaterials.BaseIndex(0, UnitKind.Mcv),
                    float3.zero, 0f, UnitKind.Mcv, 0);
                owned.Add(mcv);

                var placed = BaseCommands.AutoBuildBase(entityManager, 0);
                Assert.AreEqual(3, placed, "MCV + power plant + refinery should be placed");

                Assert.IsFalse(entityManager.Exists(mcv), "the MCV is consumed by deployment");
                Assert.AreNotEqual(Entity.Null, BaseCommands.FindConstructionYard(entityManager, 0));
                Assert.AreNotEqual(Entity.Null, BaseCommands.FindNearestRefinery(entityManager, 0, float3.zero));

                var state = entityManager.GetComponentData<EconomyState>(economy);
                Assert.AreEqual(2100, Economy.GetCredits(state, 0), "3000 - 300 - 600");

                var powerPlant = FindBuilding(entityManager, 0, BuildingKind.PowerPlant);
                Assert.AreNotEqual(Entity.Null, powerPlant);
                Assert.AreEqual(100, entityManager.GetComponentData<BuildingInfo>(powerPlant).PowerProduced);

                yield return new WaitForSeconds(0.3f);

                state = entityManager.GetComponentData<EconomyState>(economy);
                Assert.AreEqual(110, Economy.GetPowerMade(state, 0), "yard 10 + power plant 100");
                Assert.AreEqual(40, Economy.GetPowerUsed(state, 0), "refinery draw");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator PlaceBuilding_RejectsOutsideRadiusAndInsufficientCredits()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 180f, 91, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var economy = CreateEconomy(entityManager, 1000, 1000);
                BuildingFactory.SpawnBuilding(entityManager, 0, BuildingKind.ConstructionYard, float3.zero);

                var ok = BaseCommands.TryPlaceBuilding(
                    entityManager, 0, BuildingKind.PowerPlant, new float3(60f, 0f, 0f), out var failure);

                Assert.IsFalse(ok);
                Assert.AreEqual(BuildFailure.OutsideBuildRadius, failure);
                Assert.AreEqual(1000, Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));

                var state = entityManager.GetComponentData<EconomyState>(economy);
                Economy.SetCredits(ref state, 0, 100);
                entityManager.SetComponentData(economy, state);

                ok = BaseCommands.TryPlaceBuilding(
                    entityManager, 0, BuildingKind.PowerPlant, new float3(26f, 0f, 0f), out failure);

                Assert.IsFalse(ok);
                Assert.AreEqual(BuildFailure.NotEnoughCredits, failure);
                Assert.AreEqual(100, Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));
                Assert.AreEqual(Entity.Null, FindBuilding(entityManager, 0, BuildingKind.PowerPlant));
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Dock_RequiresWaterProximity()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 240f, 121, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                MapRuntime.Select(0);
                var body = MapWater.Body(0);
                var economy = CreateEconomy(entityManager, 5000, 5000);
                BuildingFactory.SpawnBuilding(entityManager, 0, BuildingKind.ConstructionYard, float3.zero);

                var ok = BaseCommands.TryPlaceBuilding(
                    entityManager, 0, BuildingKind.Dock, new float3(60f, 0f, 0f), out var failure);

                Assert.IsFalse(ok, "docks cannot be built away from water");
                Assert.AreEqual(BuildFailure.NotNearWater, failure);
                Assert.AreEqual(5000, Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));

                var shore = new float3(
                    body.Centre.x + body.Radius + 5f,
                    0f,
                    body.Centre.y);
                ok = BaseCommands.TryPlaceBuilding(
                    entityManager, 0, BuildingKind.Dock, shore, out failure);

                Assert.IsTrue(ok, $"a shore site should accept the dock: {failure}");
                Assert.AreEqual(BuildFailure.None, failure);
                Assert.AreEqual(4200, Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0));
                Assert.AreNotEqual(Entity.Null, FindBuilding(entityManager, 0, BuildingKind.Dock));
            }
            finally
            {
                MapRuntime.Reset();
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Dock_AiAutoPlaceFinishesOnTheShore()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 240f, 121, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                MapRuntime.Select(0);
                CreateEconomy(entityManager, 5000, 5000);
                BuildingFactory.SpawnBuilding(entityManager, 0, BuildingKind.ConstructionYard, float3.zero);

                Assert.IsTrue(
                    BaseCommands.TryAutoPlaceDock(entityManager, 0),
                    "the AI should find a shore site for its dock");

                var dock = FindBuilding(entityManager, 0, BuildingKind.Dock);
                Assert.AreNotEqual(Entity.Null, dock);

                var position = entityManager.GetComponentData<LocalTransform>(dock).Position;
                var distance = MapWater.DistanceToBoundary(
                    0,
                    new float2(position.x, position.z));

                Assert.LessOrEqual(
                    distance,
                    22f,
                    "the auto-placed dock must sit near the water");
            }
            finally
            {
                MapRuntime.Reset();
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Dock_CannotBeBuiltOnADryMap()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 240f, 121, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();
            MapRuntime.Select(1);

            try
            {
                CreateEconomy(entityManager, 5000, 5000);
                BuildingFactory.SpawnBuilding(entityManager, 0, BuildingKind.ConstructionYard, float3.zero);

                var ok = BaseCommands.TryPlaceBuilding(
                    entityManager, 0, BuildingKind.Dock, new float3(30f, 0f, 0f), out var failure);

                Assert.IsFalse(ok, "dry maps cannot host a dock");
                Assert.AreEqual(BuildFailure.NoWater, failure);
                Assert.IsFalse(
                    BaseCommands.TryAutoPlaceDock(entityManager, 0),
                    "the AI must not place a dock on a dry map");
            }
            finally
            {
                MapRuntime.Reset();
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Harvester_DeliversCargoToRefinery()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 120f, 61, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var economy = CreateEconomy(entityManager, 100, 100);

                BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Refinery, new float3(15f, 0f, 0f));

                var harvester = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 2,
                    UnitMaterials.BaseIndex(0, UnitKind.Harvester),
                    float3.zero, 0f, UnitKind.Harvester, 0);
                owned.Add(harvester);

                var job = entityManager.GetComponentData<HarvesterJob>(harvester);
                job.Phase = HarvesterPhase.ToRefinery;
                job.Cargo = 350f;
                entityManager.SetComponentData(harvester, job);

                var delivered = false;
                for (var frame = 0; frame < 240 && !delivered; frame++)
                {
                    yield return null;
                    delivered = Economy.GetCredits(entityManager.GetComponentData<EconomyState>(economy), 0) >= 450;
                }

                Assert.IsTrue(delivered, "the harvester must deposit its cargo at the refinery");
                Assert.AreEqual(
                    0f,
                    entityManager.GetComponentData<HarvesterJob>(harvester).Cargo,
                    0.001f,
                    "cargo is emptied on deposit");
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Harvester_SeeksAndHarvestsOre()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                var economy = CreateEconomy(entityManager, 100, 100);

                BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Refinery, new float3(-30f, 0f, 0f));

                var ore = BuildingFactory.SpawnOre(
                    entityManager, new float3(10f, 0f, 0f), 500, 7f);

                var harvester = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 2,
                    UnitMaterials.BaseIndex(0, UnitKind.Harvester),
                    float3.zero, 0f, UnitKind.Harvester, 0);
                owned.Add(harvester);

                var harvested = false;
                for (var frame = 0; frame < 1800 && !harvested; frame++)
                {
                    yield return null;
                    harvested = entityManager.GetComponentData<OreNode>(ore).Amount < 500;
                }

                Assert.IsTrue(harvested, "the harvester must drive to the ore patch and start mining");
                Assert.Greater(
                    entityManager.GetComponentData<HarvesterJob>(harvester).Cargo,
                    0f,
                    "mined ore must accumulate as cargo");

                // The ore entity must not disappear while it still has reserves.
                Assert.IsTrue(entityManager.Exists(ore));
                Assert.GreaterOrEqual(entityManager.GetComponentData<OreNode>(ore).Amount, 0);
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        [UnityTest]
        public IEnumerator Harvester_PlayerOrder_NotCancelledByPassingOre()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 140f, 71, 1f);
            var owned = new NativeList<Entity>(Allocator.Persistent);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = UnitMeshes();

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                CreateEconomy(entityManager, 100, 100);

                BuildingFactory.SpawnBuilding(
                    entityManager, 0, BuildingKind.Refinery, new float3(-30f, 0f, 0f));

                var ore = BuildingFactory.SpawnOre(
                    entityManager, new float3(4f, 0f, 0f), 500, 7f);

                var harvester = UnitFactory.Spawn(
                    entityManager, renderMeshArray, 2,
                    UnitMaterials.BaseIndex(0, UnitKind.Harvester),
                    float3.zero, 0f, UnitKind.Harvester, 0);
                owned.Add(harvester);

                var job = entityManager.GetComponentData<HarvesterJob>(harvester);
                job.Phase = HarvesterPhase.ToOre;
                job.Ore = ore;
                job.AutoOrder = 0;
                entityManager.SetComponentData(harvester, job);

                // The ore is already inside harvest range, so the only thing that keeps
                // the harvester moving is the player order it must not cancel.
                var moveUnits = new NativeList<Entity>(1, Allocator.Temp);
                moveUnits.Add(harvester);
                Assert.IsTrue(
                    MoveOrderIssuer.TryIssue(
                        entityManager, moveUnits, new float3(20f, 0f, 0f), out _),
                    "player move order should be issued");
                moveUnits.Dispose();

                yield return new WaitForSeconds(0.5f);

                var position = entityManager.GetComponentData<LocalTransform>(harvester).Position;
                var move = entityManager.GetComponentData<MoveTarget>(harvester);
                var current = entityManager.GetComponentData<HarvesterJob>(harvester);

                Assert.Greater(position.x, 1f, "the player order must keep the harvester moving");
                Assert.AreNotEqual(
                    0,
                    move.HasOrder,
                    "starting next to an ore patch must not cancel a player move order");
                Assert.AreEqual(
                    HarvesterPhase.ToOre,
                    current.Phase,
                    "the harvester must not start mining under a player order");
                Assert.AreEqual(0, current.AutoOrder);
            }
            finally
            {
                Cleanup(entityManager, data, owned, material, meshes);
            }
        }

        private static Entity CreateEconomy(EntityManager entityManager, int team0, int team1)
        {
            // EconomySystem creates the singleton on its first tick, so reuse it if the
            // test has already advanced a frame.
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            var entity = query.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(EconomyState))
                : query.GetSingletonEntity();

            entityManager.SetComponentData(entity, new EconomyState
            {
                Team0Credits = team0,
                Team1Credits = team1,
            });

            return entity;
        }

        private static Entity FindBuilding(EntityManager entityManager, int team, BuildingKind kind)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingInfo>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var info = entityManager.GetComponentData<BuildingInfo>(entities[i]);
                if (info.Team == team && info.Kind == kind)
                {
                    return entities[i];
                }
            }

            return Entity.Null;
        }

        private static Mesh[] UnitMeshes()
        {
            return new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
                ProceduralMeshes.Box(new Vector3(5f, 3f, 8f)),
            };
        }

        private static Material[] Materials(Material material)
        {
            var materials = new Material[UnitMaterials.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            return materials;
        }

        private static void Cleanup(
            EntityManager entityManager,
            Entity data,
            NativeList<Entity> owned,
            Material material,
            Mesh[] meshes)
        {
            foreach (var entity in owned)
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }
            }

            owned.Dispose();
            DestroyAll<BuildingTag>(entityManager);
            DestroyAll<OreNode>(entityManager);
            DestroyAll<EconomyState>(entityManager);
            DestroyAll<MatchState>(entityManager);

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

        private static void DestroyAll<T>(EntityManager entityManager)
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

        private static Entity CreateFlatWorld(EntityManager entityManager, float size, int resolution, float cellSize)
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
