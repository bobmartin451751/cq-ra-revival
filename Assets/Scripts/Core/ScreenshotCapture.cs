using System;
using System.Globalization;
using System.IO;
using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using CQRa.View;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CQRa.Core
{
    /// <summary>
    /// Development-only capture rig. Activated by passing <c>-screenshot &lt;path&gt;</c>
    /// on the command line. Renders the main camera through the normal URP frame loop
    /// into a RenderTexture, writes a PNG, then exits the process.
    /// </summary>
    public sealed class ScreenshotCapture : MonoBehaviour
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const int WarmupFrames = 4;

        private Camera _camera;
        private RenderTexture _target;
        private string _path;
        private bool _selectAll;
        private bool _attack;
        private bool _base;
        private bool _army;
        private bool _siege;
        private bool _ai;
        private bool _defense;
        private bool _air;
        private int _aiLevel;
        private float2? _moveTo;
        private int _moveTeam;
        private int _captureFrame = WarmupFrames;
        private int _frames;

        private void Start()
        {
            _path = ReadArg("-screenshot");
            if (string.IsNullOrEmpty(_path))
            {
                enabled = false;
                return;
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("[Capture] no main camera");
                Quit(1);
                return;
            }

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "CaptureTarget"
            };
            _target.Create();

            _camera.targetTexture = _target;
            _camera.aspect = (float)Width / Height;

            _selectAll = HasArg("-selectall");
            _attack = HasArg("-attack");
            _base = HasArg("-base");
            _army = HasArg("-army");
            _siege = HasArg("-siege");
            _ai = HasArg("-ai");
            _defense = HasArg("-defense");
            _air = HasArg("-air");
            _aiLevel = ReadIntArg("-ailevel", (int)AiDifficulty.Veteran);
            _moveTo = ReadMoveTarget();
            _moveTeam = ReadIntArg("-moveteam", 0);

            var waitFrames = ReadIntArg("-waitframes", WarmupFrames);
            _captureFrame = Mathf.Max(WarmupFrames, waitFrames);

            Debug.Log($"[Capture] armed -> {_path} (capture frame {_captureFrame})");
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            _frames++;

            // Units are spawned in Start, so wait a frame before touching them.
            if (_frames == 2)
            {
                if (_selectAll)
                {
                    SelectAllUnits();
                }

                if (_moveTo.HasValue)
                {
                    IssueMoveOrder(_moveTo.Value, _moveTeam);
                }

                if (_attack)
                {
                    IssueAttackOrder(_moveTeam);
                }

                if (_ai)
                {
                    EnableAi();
                }

                if (_base)
                {
                    BuildBases();
                }

                if (_army || _siege || _air)
                {
                    BuildArmies();
                }

                if (_air)
                {
                    SpawnAirDemo();
                }

                if (_defense)
                {
                    BuildDefenseDemo();
                }

                if (_siege)
                {
                    SiegeEnemyBase();
                }
            }

            if (_frames < _captureFrame)
            {
                return;
            }

            try
            {
                var previous = RenderTexture.active;
                RenderTexture.active = _target;

                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();

                RenderTexture.active = previous;

                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(_path, tex.EncodeToPNG());
                Destroy(tex);

                Debug.Log($"[Capture] wrote {_path} ({new FileInfo(_path).Length} bytes)");
                Quit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Capture] failed: " + e);
                Quit(1);
            }
        }

        private void OnDestroy()
        {
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }

        private static bool HasArg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Development helper: enables selection on every unit for visual capture.</summary>
        private static void SelectAllUnits()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitTag>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<Selected>(entity))
                {
                    entityManager.SetComponentEnabled<Selected>(entity, true);
                }
            }

            Debug.Log($"[Capture] selected {entities.Length} units");
        }

        /// <summary>
        /// Development helper used by the Phase 3 movement screenshot: orders every unit
        /// on a team to advance on a ground point, then drops the selection so the shot
        /// shows normal team colours.
        /// </summary>
        private void IssueMoveOrder(float2 destination, int team)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var terrain = UnityEngine.Object.FindFirstObjectByType<HeightmapTerrain>();
            var point = new float3(
                destination.x,
                terrain != null ? terrain.SampleHeight(destination.x, destination.y) : 0f,
                destination.y);

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadWrite<MoveTarget>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var selected = new NativeList<Entity>(entities.Length, Allocator.Temp);
            var ordered = 0;
            try
            {
                foreach (var entity in entities)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(entity);
                    if (info.Team != team)
                    {
                        continue;
                    }

                    selected.Add(entity);

                    if (!_selectAll)
                    {
                        entityManager.SetComponentEnabled<Selected>(entity, false);
                    }
                }

                ordered = selected.Length;

                if (MoveOrderIssuer.TryIssue(entityManager, selected, point, out var resolved))
                {
                    point = resolved;
                }
            }
            finally
            {
                selected.Dispose();
            }

            Debug.Log($"[Capture] ordered {ordered} team-{team} units to {point}");
        }

        /// <summary>
        /// Development helper for the Phase 4 combat screenshot: every armed unit on a
        /// team is ordered to attack its nearest enemy, exactly as a right-click would.
        /// </summary>
        private void IssueAttackOrder(int team)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<Weapon>(),
                ComponentType.ReadWrite<CombatState>(),
                ComponentType.ReadWrite<MoveTarget>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var ordered = 0;
            foreach (var entity in entities)
            {
                var info = entityManager.GetComponentData<UnitInfo>(entity);
                if (info.Team != team || entityManager.GetComponentData<Weapon>(entity).Damage <= 0f)
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                var target = Entity.Null;
                var bestDistanceSq = float.MaxValue;

                foreach (var candidate in entities)
                {
                    if (entityManager.GetComponentData<UnitInfo>(candidate).Team == team)
                    {
                        continue;
                    }

                    var candidatePosition = entityManager.GetComponentData<LocalTransform>(candidate).Position;
                    var distanceSq = math.distancesq(position, candidatePosition);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        target = candidate;
                    }
                }

                if (target == Entity.Null)
                {
                    continue;
                }

                var state = entityManager.GetComponentData<CombatState>(entity);
                state.Target = target;
                state.Explicit = 1;
                entityManager.SetComponentData(entity, state);

                var move = entityManager.GetComponentData<MoveTarget>(entity);
                move.HasOrder = 0;
                move.Field = Entity.Null;
                entityManager.SetComponentData(entity, move);

                if (!_selectAll)
                {
                    entityManager.SetComponentEnabled<Selected>(entity, false);
                }

                ordered++;
            }

            Debug.Log($"[Capture] ordered {ordered} team-{team} units to attack");
        }

        private void SpawnAirDemo()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;

            for (var team = 0; team < 2; team++)
            {
                var startZ = team == 0 ? 30f : -30f;
                var destinationZ = team == 0 ? -18f : 18f;
                var yaw = team == 0 ? 0f : math.PI;

                for (var i = 0; i < 3; i++)
                {
                    var x = -12f + i * 12f;
                    var gunship = UnitFactory.SpawnDefault(
                        entityManager,
                        new float3(x, 0f, startZ),
                        yaw,
                        UnitKind.Gunship,
                        team);

                    var move = entityManager.GetComponentData<MoveTarget>(gunship);
                    move.Destination = new float3(x, 0f, destinationZ);
                    move.PathDestination = move.Destination;
                    move.Field = Entity.Null;
                    move.HasOrder = 1;
                    entityManager.SetComponentData(gunship, move);
                }
            }

            Debug.Log("[Capture] spawned gunship demo");
        }

        private void BuildDefenseDemo()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var built = 0;
            var defenses = 0;

            for (var team = 0; team < 2; team++)
            {
                built += BaseCommands.AutoBuildBase(entityManager, team);
                built += BaseCommands.AutoBuildProduction(entityManager, team);
                defenses += BaseCommands.AutoBuildDefenses(entityManager, team);
            }

            Debug.Log($"[Capture] built {built} structures and {defenses} defenses");
        }

        /// <summary>
        /// Development helper for the Phase 8 screenshot: give each team an AI commander
        /// and let them play the normal rules from the starting MCVs.
        /// </summary>
        private void EnableAi()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var level = (AiDifficulty)math.clamp(_aiLevel, 0, 3);

            for (var team = 0; team < MatchRuntime.TeamCount; team++)
            {
                if (HasCommander(entityManager, team))
                {
                    continue;
                }

                var entity = entityManager.CreateEntity(typeof(AiCommander));
                entityManager.SetComponentData(entity, new AiCommander
                {
                    Team = team,
                    Difficulty = level,
                    ThinkTimer = 0f,
                });
            }

            Debug.Log($"[Capture] AI enabled for both teams ({level})");
        }

        private static bool HasCommander(EntityManager entityManager, int team)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AiCommander>());
            using var commanders = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < commanders.Length; i++)
            {
                if (entityManager.GetComponentData<AiCommander>(commanders[i]).Team == team)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildBases()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var total = 0;
            for (var team = 0; team < 2; team++)
            {
                total += BaseCommands.AutoBuildBase(entityManager, team);
            }

            Debug.Log($"[Capture] built {total} base structures");
        }

        /// <summary>
        /// Development helper for the Phase 6 screenshot: build each team a full base
        /// (yard, power, refinery) plus barracks and war factory, then queue one
        /// infantryman and one tank per team through the normal production rules.
        /// </summary>
        private void BuildArmies()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var built = 0;
            var queued = 0;

            for (var team = 0; team < 2; team++)
            {
                built += BaseCommands.AutoBuildBase(entityManager, team);
                built += BaseCommands.AutoBuildProduction(entityManager, team);

                if (ProductionCommands.TryEnqueue(
                        entityManager, team, UnitKind.Infantry, Entity.Null, out var failure))
                {
                    queued++;
                }
                else
                {
                    Debug.Log($"[Capture] team{team} infantry enqueue failed: {failure}");
                }

                if (ProductionCommands.TryEnqueue(
                        entityManager, team, UnitKind.Tank, Entity.Null, out failure))
                {
                    queued++;
                }
                else
                {
                    Debug.Log($"[Capture] team{team} tank enqueue failed: {failure}");
                }
            }

            Debug.Log($"[Capture] built {built} structures, queued {queued} units");
        }

        /// <summary>
        /// Development helper for the Phase 7 screenshot: order every armed red unit to
        /// attack the nearest blue building, exercising building targeting and destruction.
        /// </summary>
        private void SiegeEnemyBase()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Weapon>(),
                ComponentType.ReadOnly<CombatState>(),
                ComponentType.ReadOnly<MoveTarget>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var buildings = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingInfo>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var units = unitQuery.ToEntityArray(Allocator.Temp);
            using var buildingEntities = buildings.ToEntityArray(Allocator.Temp);

            var ordered = 0;

            foreach (var unit in units)
            {
                var info = entityManager.GetComponentData<UnitInfo>(unit);
                if (info.Team != 0 || entityManager.GetComponentData<Weapon>(unit).Damage <= 0f)
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                var target = Entity.Null;
                var bestDistanceSq = float.MaxValue;

                foreach (var building in buildingEntities)
                {
                    if (entityManager.GetComponentData<BuildingInfo>(building).Team == 0)
                    {
                        continue;
                    }

                    var candidate = entityManager.GetComponentData<LocalTransform>(building).Position;
                    var distanceSq = math.distancesq(position, candidate);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        target = building;
                    }
                }

                if (target == Entity.Null)
                {
                    continue;
                }

                var state = entityManager.GetComponentData<CombatState>(unit);
                state.Target = target;
                state.Explicit = 1;
                entityManager.SetComponentData(unit, state);

                var move = entityManager.GetComponentData<MoveTarget>(unit);
                move.HasOrder = 0;
                move.Field = Entity.Null;
                entityManager.SetComponentData(unit, move);
                ordered++;
            }

            Debug.Log($"[Capture] ordered {ordered} red units to besiege the blue base");
        }

        private float2? ReadMoveTarget()
        {
            var text = ReadArg("-moveto");
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var parts = text.Split(',');
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                Debug.LogWarning($"[Capture] could not parse -moveto '{text}' (expected x,z)");
                return null;
            }

            return new float2(x, z);
        }

        private static int ReadIntArg(string flag, int fallback)
        {
            var text = ReadArg(flag);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static string ReadArg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
