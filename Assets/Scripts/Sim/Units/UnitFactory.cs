using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Fog;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Creates renderable ECS units at runtime using Entities Graphics.
    /// Shared meshes and materials keep draw calls low; selection changes swap the
    /// entity's material index to a dedicated highlight material.
    /// </summary>
    public static class UnitFactory
    {
        private static RenderMeshArray _defaultRenderMeshArray;
        private static bool _hasDefaultVisuals;

        public static RenderMeshArray CreateRenderMeshArray(Material[] materials, Mesh[] meshes)
        {
            return new RenderMeshArray(materials, meshes);
        }

        /// <summary>
        /// Stores the scene's shared visuals so gameplay systems (production) can spawn
        /// units without owning UnityEngine materials themselves.
        /// </summary>
        public static void SetDefaultVisuals(RenderMeshArray renderMeshArray)
        {
            _defaultRenderMeshArray = renderMeshArray;
            _hasDefaultVisuals = true;
        }

        public static void ClearDefaultVisuals()
        {
            _hasDefaultVisuals = false;
            _defaultRenderMeshArray = default;
        }

        public static Entity Spawn(
            EntityManager entityManager,
            RenderMeshArray renderMeshArray,
            int meshIndex,
            int materialIndex,
            float3 position,
            float yawRadians,
            UnitKind kind,
            int team)
        {
            var entity = CreateUnit(entityManager, position, yawRadians, kind, team);
            AddRenderComponents(entityManager, entity, renderMeshArray, meshIndex, materialIndex);
            return entity;
        }

        /// <summary>Spawns a unit using the scene-wide unit visuals, if they were registered.</summary>
        public static Entity SpawnDefault(
            EntityManager entityManager,
            float3 position,
            float yawRadians,
            UnitKind kind,
            int team)
        {
            var entity = CreateUnit(entityManager, position, yawRadians, kind, team);
            if (_hasDefaultVisuals)
            {
                AddRenderComponents(
                    entityManager,
                    entity,
                    _defaultRenderMeshArray,
                    (int)kind,
                    UnitMaterials.BaseIndex(team, kind));
            }

            return entity;
        }

        private static Entity CreateUnit(
            EntityManager entityManager,
            float3 position,
            float yawRadians,
            UnitKind kind,
            int team)
        {
            var entity = entityManager.CreateEntity();
            var factionId = FactionRuntime.ForTeam(team);
            var visualScale = factionId == FactionId.Verdant
                ? 0.92f
                : factionId == FactionId.Meridian ? 1.08f : 1f;

            entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                position,
                quaternion.RotateY(yawRadians),
                visualScale));

            entityManager.AddComponent<UnitTag>(entity);
            entityManager.AddComponentData(entity, new UnitInfo
            {
                Team = team,
                Kind = kind,
                Radius = RadiusFor(kind),
                MaxSpeed = SpeedFor(kind) * FactionRuntime.StatsForTeam(team).Speed,
            });

            entityManager.AddComponentData(entity, new Selected());
            entityManager.SetComponentEnabled<Selected>(entity, false);

            entityManager.AddComponentData(entity, new HighlightState { IsSelected = 0 });

            entityManager.AddComponentData(entity, new MoveTarget
            {
                Destination = position,
                PathDestination = position,
                Field = Entity.Null,
                DestinationCell = default,
                ArriveRadius = RadiusFor(kind) + 2.5f,
                FormationRadius = 0f,
                HasOrder = 0,
            });

            var combat = CombatStats.For(kind);
            var faction = FactionRuntime.StatsForTeam(team);
            entityManager.AddComponentData(entity, new Health
            {
                Max = combat.MaxHealth * faction.Health,
                Current = combat.MaxHealth * faction.Health,
                Armor = combat.Armor,
            });

            entityManager.AddComponentData(entity, new Weapon
            {
                Damage = combat.Damage * faction.Damage,
                Range = combat.Range,
                Cooldown = combat.Cooldown,
                CooldownRemaining = 0f,
                AcquireRange = combat.AcquireRange,
            });

            entityManager.AddComponentData(entity, new CombatState
            {
                Target = Entity.Null,
                Explicit = 0,
            });

            if (kind == UnitKind.Harvester)
            {
                entityManager.AddComponentData(entity, new CQRa.Sim.Base.HarvesterJob
                {
                    Phase = CQRa.Sim.Base.HarvesterPhase.Idle,
                    Ore = Entity.Null,
                    Cargo = 0f,
                    AutoOrder = 0,
                });
            }

            if (kind == UnitKind.Gunship || kind == UnitKind.Skyfall)
            {
                entityManager.AddComponentData(entity, new AirUnit { Altitude = 18f });
            }

            if (kind == UnitKind.Thumper)
            {
                entityManager.AddComponentData(entity, new DeployState
                {
                    Deployed = 0,
                    NormalRange = combat.Range,
                    DeployedRange = combat.Range * 1.4f,
                    NormalCooldown = combat.Cooldown,
                    DeployedCooldown = combat.Cooldown * 1.5f,
                });
            }

            if (kind == UnitKind.Infantry &&
                FactionRuntime.ForTeam(team) == FactionId.Meridian)
            {
                entityManager.AddComponentData(entity, new Shield
                {
                    Max = 40f,
                    Current = 40f,
                    RegenDelay = 4f,
                    TimeSinceHit = 0f,
                });
            }

            if (kind == UnitKind.Mantis)
            {
                entityManager.AddComponentData(entity, new Cloak
                {
                    Cloaked = 0,
                    Delay = 3f,
                    Timer = 0f,
                });
            }

            if (combat.Damage > 0f)
            {
                entityManager.AddComponentData(entity, new Veterancy
                {
                    Kills = 0,
                    Rank = 0,
                });
            }

            if (kind == UnitKind.Rig)
            {
                entityManager.AddComponentData(entity, new RepairAura
                {
                    Radius = 10f,
                    HealPerSecond = 10f,
                });
            }

            if (kind == UnitKind.Sower)
            {
                entityManager.AddComponentData(entity, new MineLayer
                {
                    Cooldown = 4f,
                    CooldownRemaining = 0f,
                });
            }

            if (WaterLake.IsNaval(kind))
            {
                entityManager.AddComponentData(entity, new NavalUnit
                {
                    Centre = WaterLake.Centre,
                    Radius = WaterLake.Radius,
                    Level = WaterLake.Level,
                });
            }

            entityManager.AddComponentData(entity, new UnitMotion
            {
                Velocity = float2.zero,
                Yaw = yawRadians,
                Speed = 0f,
            });

            entityManager.AddComponentData(entity, new VisionSource
            {
                Radius = VisionCatalog.ForUnit(kind) * faction.Vision,
            });

            return entity;
        }

        private static void AddRenderComponents(
            EntityManager entityManager,
            Entity entity,
            RenderMeshArray renderMeshArray,
            int meshIndex,
            int materialIndex)
        {
            var description = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(materialIndex, meshIndex);
            RenderMeshUtility.AddComponents(entity, entityManager, description, renderMeshArray, materialMeshInfo);
        }

        public static float RadiusFor(UnitKind kind)
        {
            return ContentDatabase.Unit(kind).Radius;
        }

        public static float SpeedFor(UnitKind kind)
        {
            return ContentDatabase.Unit(kind).Speed;
        }
    }
}
