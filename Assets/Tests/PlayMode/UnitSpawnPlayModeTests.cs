using System.Collections;
using CQRa.Sim.Units;
using CQRa.View;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class UnitSpawnPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnitFactory_SpawnsSelectableRenderableEntity()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world, "default ECS world should exist in play mode");

            var entityManager = world.EntityManager;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader, "URP Lit shader should be available");

            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f));
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(new[] { material }, new[] { mesh });

            var entity = Entity.Null;

            try
            {
                entity = UnitFactory.Spawn(
                    entityManager,
                    renderMeshArray,
                    0,
                    0,
                    new float3(0f, 0f, 0f),
                    0f,
                    UnitKind.Tank,
                    0);

                Assert.IsTrue(entityManager.Exists(entity));
                Assert.IsTrue(entityManager.HasComponent<UnitTag>(entity));
                Assert.IsTrue(entityManager.HasComponent<LocalTransform>(entity));
                Assert.IsTrue(entityManager.HasComponent<MaterialMeshInfo>(entity));
                Assert.IsTrue(entityManager.HasComponent<HighlightState>(entity));

                Assert.IsFalse(
                    entityManager.IsComponentEnabled<Selected>(entity),
                    "units must not start selected");

                entityManager.SetComponentEnabled<Selected>(entity, true);
                Assert.IsTrue(entityManager.IsComponentEnabled<Selected>(entity));

                var info = entityManager.GetComponentData<UnitInfo>(entity);
                Assert.AreEqual(UnitKind.Tank, info.Kind);
                Assert.AreEqual(0, info.Team);
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
        public IEnumerator SelectingUnit_SwapsToHighlightMaterial()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            var mesh = ProceduralMeshes.Box(Vector3.one);

            var materials = new Material[UnitMaterials.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            // Highlights use the unit's kind as the mesh index, so all three slots must exist.
            var renderMeshArray = UnitFactory.CreateRenderMeshArray(materials, new[] { mesh, mesh, mesh });
            var entity = Entity.Null;

            try
            {
                entity = UnitFactory.Spawn(
                    entityManager,
                    renderMeshArray,
                    0,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    new float3(0f, 0f, 0f),
                    0f,
                    UnitKind.Tank,
                    0);

                var initial = entityManager.GetComponentData<MaterialMeshInfo>(entity);
                Assert.AreEqual(
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    MaterialMeshInfo.StaticIndexToArrayIndex(initial.Material));

                entityManager.SetComponentEnabled<Selected>(entity, true);
                yield return null;

                var highlighted = entityManager.GetComponentData<MaterialMeshInfo>(entity);
                Assert.AreEqual(
                    UnitMaterials.HighlightIndex,
                    MaterialMeshInfo.StaticIndexToArrayIndex(highlighted.Material));
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
    }
}
