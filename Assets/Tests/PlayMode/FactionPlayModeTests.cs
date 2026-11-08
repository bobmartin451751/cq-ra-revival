using System.Collections;
using CQRa.Sim.Factions;
using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using CQRa.View;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace CQRa.Tests.PlayMode
{
    public sealed class FactionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpawnedUnits_ReceiveFactionModifiers()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();
            var owned = new NativeList<Entity>(Allocator.Persistent);

            try
            {
                FactionRuntime.Set(FactionId.Meridian, FactionId.Verdant);
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);

                var meridian = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(0, UnitKind.Tank),
                    float3.zero, 0f, UnitKind.Tank, 0);
                owned.Add(meridian);

                var verdant = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Tank,
                    UnitMaterials.BaseIndex(1, UnitKind.Tank),
                    new float3(10f, 0f, 0f), 0f, UnitKind.Tank, 1);
                owned.Add(verdant);

                var baseHealth = CombatStats.For(UnitKind.Tank).MaxHealth;

                Assert.AreEqual(
                    baseHealth * 1.02f,
                    entityManager.GetComponentData<Health>(meridian).Max,
                    0.01f);
                Assert.AreEqual(
                    baseHealth * 0.92f,
                    entityManager.GetComponentData<Health>(verdant).Max,
                    0.01f);

                Assert.Greater(
                    entityManager.GetComponentData<Weapon>(meridian).Damage,
                    entityManager.GetComponentData<Weapon>(verdant).Damage);
                Assert.Greater(
                    entityManager.GetComponentData<UnitInfo>(verdant).MaxSpeed,
                    entityManager.GetComponentData<UnitInfo>(meridian).MaxSpeed);
                Assert.Greater(
                    entityManager.GetComponentData<VisionSource>(meridian).Radius,
                    entityManager.GetComponentData<VisionSource>(verdant).Radius);
            }
            finally
            {
                FactionRuntime.Reset();
                foreach (var entity in owned)
                {
                    if (entityManager.Exists(entity))
                    {
                        entityManager.DestroyEntity(entity);
                    }
                }

                owned.Dispose();
                foreach (var mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }

                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator Skyfall_SpawnsAsAirUnit()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();
            var skyfall = Entity.Null;

            try
            {
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                skyfall = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Skyfall,
                    UnitMaterials.BaseIndex(0, UnitKind.Skyfall),
                    float3.zero, 0f, UnitKind.Skyfall, 0);

                Assert.IsTrue(entityManager.HasComponent<AirUnit>(skyfall));
                Assert.AreEqual(UnitKind.Skyfall, entityManager.GetComponentData<UnitInfo>(skyfall).Kind);
            }
            finally
            {
                FactionRuntime.Reset();
                if (entityManager.Exists(skyfall))
                {
                    entityManager.DestroyEntity(skyfall);
                }

                foreach (var mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }

                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator Aegis_ShieldAbsorbsDamage_AndRegenerates()
        {
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var meshes = Meshes();
            var entity = Entity.Null;

            try
            {
                FactionRuntime.Set(FactionId.Meridian, FactionId.Ashfall);
                var renderMeshArray = UnitFactory.CreateRenderMeshArray(Materials(material), meshes);
                entity = UnitFactory.Spawn(
                    entityManager, renderMeshArray, (int)UnitKind.Infantry,
                    UnitMaterials.BaseIndex(0, UnitKind.Infantry),
                    float3.zero, 0f, UnitKind.Infantry, 0);

                Assert.IsTrue(entityManager.HasComponent<Shield>(entity));

                var shield = entityManager.GetComponentData<Shield>(entity);
                shield.Current = 10f;
                entityManager.SetComponentData(entity, shield);

                var remaining = CombatMath.AbsorbWithShield(entityManager, entity, 25f);
                Assert.AreEqual(15f, remaining, 0.01f);
                Assert.AreEqual(
                    0f,
                    entityManager.GetComponentData<Shield>(entity).Current,
                    0.01f);

                shield = entityManager.GetComponentData<Shield>(entity);
                shield.TimeSinceHit = 10f;
                entityManager.SetComponentData(entity, shield);

                yield return new WaitForSeconds(0.5f);

                Assert.Greater(
                    entityManager.GetComponentData<Shield>(entity).Current,
                    0f,
                    "shields should regenerate after the quiet period");
            }
            finally
            {
                FactionRuntime.Reset();
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }

                foreach (var mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }

                Object.DestroyImmediate(material);
            }
        }

        private static Mesh[] Meshes()
        {
            return new[]
            {
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(3f, 1.5f, 7f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 7f)),
                ProceduralMeshes.Box(new Vector3(5f, 3f, 8f)),
                ProceduralMeshes.Box(new Vector3(4f, 1f, 8f)),
                ProceduralMeshes.Box(new Vector3(0.9f, 1.2f, 0.9f)),
                ProceduralMeshes.Box(new Vector3(4f, 2f, 6f)),
                ProceduralMeshes.Box(new Vector3(0.6f, 1.4f, 0.6f)),
                ProceduralMeshes.Box(new Vector3(4f, 1f, 8f)),
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
    }
}
