using System.Collections;
using CQRa.Sim.Content;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class NavalPlayModeTests
    {
        [UnityTest]
        public IEnumerator PatrolBoat_StaysInsideTheLake()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var data = CreateFlatWorld(entityManager, 240f, 241, 1f);
            var entity = entityManager.CreateEntity();
            MapRuntime.Select(0);
            var body = MapWater.Body(0);
            var level = body.Level;

            try
            {
                entityManager.AddComponent<UnitTag>(entity);
                entityManager.AddComponentData(entity, new UnitInfo
                {
                    Team = 0,
                    Kind = UnitKind.PatrolBoat,
                    Radius = 4f,
                    MaxSpeed = 9f,
                });
                entityManager.AddComponentData(entity, new Health { Max = 320f, Current = 320f });
                entityManager.AddComponentData(entity, new MoveTarget
                {
                    Destination = new float3(50f, 0f, 0f),
                    PathDestination = new float3(50f, 0f, 0f),
                    ArriveRadius = 6.5f,
                    HasOrder = 1,
                });
                entityManager.AddComponentData(entity, new UnitMotion());
                entityManager.AddComponentData(entity, new NavalUnit
                {
                    BodyIndex = 0,
                    Level = level,
                });
                var start = MapWater.ClampPoint(
                    0,
                    new float3(body.Centre.x + body.Radius - 10f, level, body.Centre.y),
                    4f);
                entityManager.AddComponentData(
                    entity,
                    LocalTransform.FromPositionRotationScale(
                        start,
                        quaternion.identity,
                        1f));

                yield return new WaitForSeconds(2f);

                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;

                Assert.IsTrue(
                    MapWater.Contains(0, new float2(position.x, position.z)),
                    "ships must stay in their water body");
                Assert.AreEqual(level, position.y, 0.01f);
            }
            finally
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }

                if (entityManager.Exists(data))
                {
                    entityManager.DestroyEntity(data);
                }

                MapRuntime.Reset();
            }
        }

        private static Entity CreateFlatWorld(
            EntityManager entityManager,
            float size,
            int resolution,
            float cellSize)
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
