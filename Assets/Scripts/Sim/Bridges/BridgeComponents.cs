using System;
using System.Collections.Generic;
using CQRa.Sim.Content;
using CQRa.Sim.Units;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CQRa.Sim.Bridges
{
    /// <summary>One neutral bridge crossing a water body on the current map.</summary>
    public struct BridgeSpec
    {
        public int Index;
        public float2 Centre;
        public float Yaw;
        public float HalfLength;
        public float HalfWidth;
        public float Level;
    }

    /// <summary>
    /// Deterministic bridge placement. Every map with water gets up to two bridges
    /// crossing the narrowest axis of its water bodies. Pure data so nav building,
    /// rendering and tests all agree on the same footprint.
    /// </summary>
    public static class BridgeCatalog
    {
        public const float DeckMargin = 5f;
        public const float DefaultHalfWidth = 4.5f;

        public static BridgeSpec[] ForCurrentMap()
        {
            var map = MapRuntime.Current;
            return map == null ? Array.Empty<BridgeSpec>() : Build(map);
        }

        public static BridgeSpec[] Build(MapDefinition map)
        {
            var bodies = map?.WaterBodies;
            if (bodies == null || bodies.Length == 0)
            {
                return Array.Empty<BridgeSpec>();
            }

            var specs = new List<BridgeSpec>(math.min(2, bodies.Length));
            for (var i = 0; i < bodies.Length && specs.Count < 2; i++)
            {
                var body = bodies[i];
                if (body == null)
                {
                    continue;
                }

                if (body.Kind == WaterShapeKind.Circle)
                {
                    specs.Add(new BridgeSpec
                    {
                        Index = specs.Count,
                        Centre = body.Centre,
                        Yaw = 0f,
                        HalfLength = body.Radius + DeckMargin,
                        HalfWidth = DefaultHalfWidth,
                        Level = body.Level + 0.9f,
                    });
                    continue;
                }

                var points = body.Points;
                if (points == null || points.Length < 3)
                {
                    continue;
                }

                var min = new float2(float.MaxValue, float.MaxValue);
                var max = new float2(float.MinValue, float.MinValue);
                for (var p = 0; p < points.Length; p++)
                {
                    min = math.min(min, points[p]);
                    max = math.max(max, points[p]);
                }

                var centre = (min + max) * 0.5f;
                var extentX = (max.x - min.x) * 0.5f;
                var extentZ = (max.y - min.y) * 0.5f;
                var alongX = extentX <= extentZ;

                specs.Add(new BridgeSpec
                {
                    Index = specs.Count,
                    Centre = centre,
                    Yaw = alongX ? 0f : math.PI * 0.5f,
                    HalfLength = (alongX ? extentX : extentZ) + DeckMargin,
                    HalfWidth = DefaultHalfWidth,
                    Level = body.Level + 0.9f,
                });
            }

            return specs.ToArray();
        }

        /// <summary>True when the point lies on any bridge deck footprint.</summary>
        public static bool Covers(float2 point)
        {
            var specs = ForCurrentMap();
            return Covers(specs, point);
        }

        public static bool Covers(BridgeSpec[] specs, float2 point)
        {
            if (specs == null)
            {
                return false;
            }

            for (var i = 0; i < specs.Length; i++)
            {
                if (Covers(specs[i], point))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Covers(in BridgeSpec spec, float2 point)
        {
            var direction = new float2(math.cos(spec.Yaw), math.sin(spec.Yaw));
            var perpendicular = new float2(-direction.y, direction.x);
            var delta = point - spec.Centre;
            return math.abs(math.dot(delta, direction)) <= spec.HalfLength &&
                   math.abs(math.dot(delta, perpendicular)) <= spec.HalfWidth;
        }
    }

    /// <summary>Neutral, targetable bridge. Destroyed bridges stay as rubble.</summary>
    public struct Bridge : IComponentData
    {
        public int Index;
        public float2 Centre;
        public float Yaw;
        public float HalfLength;
        public float HalfWidth;
        public float Level;
        public byte Destroyed;
    }

    public struct BridgeTag : IComponentData
    {
    }

    public static class BridgeRules
    {
        public const float MaxHealth = 900f;
        public const int NeutralTeam = -1;
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Sim.Core.SimClockSystem))]
    public partial class BridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            EnsureBridges(EntityManager);
        }

        /// <summary>Creates the bridge entities for the current map once per match.</summary>
        public static void EnsureBridges(EntityManager entityManager)
        {
            using var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BridgeTag>());
            if (!existing.IsEmptyIgnoreFilter)
            {
                return;
            }

            var specs = BridgeCatalog.ForCurrentMap();
            if (specs.Length == 0)
            {
                return;
            }

            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var entity = entityManager.CreateEntity(
                    typeof(BridgeTag),
                    typeof(Bridge),
                    typeof(Health),
                    typeof(LocalTransform));
                entityManager.SetComponentData(entity, new Bridge
                {
                    Index = spec.Index,
                    Centre = spec.Centre,
                    Yaw = spec.Yaw,
                    HalfLength = spec.HalfLength,
                    HalfWidth = spec.HalfWidth,
                    Level = spec.Level,
                    Destroyed = 0,
                });
                entityManager.SetComponentData(entity, new Health
                {
                    Max = BridgeRules.MaxHealth,
                    Current = BridgeRules.MaxHealth,
                });
                entityManager.SetComponentData(
                    entity,
                    LocalTransform.FromPositionRotationScale(
                        new float3(spec.Centre.x, spec.Level, spec.Centre.y),
                        quaternion.RotateY(spec.Yaw),
                        1f));
            }
        }

        /// <summary>Marks a bridge destroyed and blocks its deck cells.</summary>
        public static void DestroyBridge(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<Bridge>(entity))
            {
                return;
            }

            var bridge = entityManager.GetComponentData<Bridge>(entity);
            if (bridge.Destroyed != 0)
            {
                return;
            }

            bridge.Destroyed = 1;
            entityManager.SetComponentData(entity, bridge);

            Navigation.NavGridQuery.BlockOrientedBox(
                entityManager,
                bridge.Centre,
                bridge.HalfLength,
                bridge.HalfWidth,
                bridge.Yaw);
            Navigation.NavGridQuery.InvalidateFlowFields(entityManager);
        }
    }
}
