using System.Collections.Generic;
using CQRa.Sim.Base;
using CQRa.Sim.Navigation;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CQRa.View
{
    /// <summary>
    /// Click and marquee selection for ECS units.
    ///
    /// Picking is screen-space: unit positions are projected to pixels and compared
    /// against the cursor, so no physics colliders or raycasts are involved.
    /// Box-selection rectangle is drawn with IMGUI, which keeps Phase 2 dependency-free;
    /// the final HUD moves to UI Toolkit in Phase 8.
    /// </summary>
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private HeightmapTerrain _terrain;
        [SerializeField] private MoveOrderMarker _moveMarker;
        [SerializeField] private float _pickRadiusPixels = 34f;
        [SerializeField] private float _dragThresholdPixels = 6f;

        private readonly List<Entity> _selected = new List<Entity>();

        private Vector2 _dragStart;
        private Vector2 _dragCurrent;
        private bool _dragging;
        private bool _boxSelecting;
        private int _movingCount;

        private GUIStyle _hintStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _panelStyle;

        private int _armedBuild = -1;
        private string _message;
        private float _messageUntil;
        private Entity _selectedProducer = Entity.Null;

        public IReadOnlyList<Entity> Selected => _selected;

        public void Configure(Camera camera, HeightmapTerrain terrain)
        {
            _camera = camera;
            _terrain = terrain;
        }

        public void Configure(Camera camera, HeightmapTerrain terrain, MoveOrderMarker moveMarker)
        {
            _camera = camera;
            _terrain = terrain;
            _moveMarker = moveMarker;
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<HeightmapTerrain>();
            }
        }

        private void Update()
        {
            // Destroyed units must leave the selection immediately (HUD counts, orders).
            PruneSelection();

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var keyboard = Keyboard.current;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragging = true;
                _boxSelecting = false;
                _dragStart = mouse.position.ReadValue();
                _dragCurrent = _dragStart;
            }

            if (_dragging)
            {
                _dragCurrent = mouse.position.ReadValue();

                if (!_boxSelecting &&
                    (_dragCurrent - _dragStart).sqrMagnitude > _dragThresholdPixels * _dragThresholdPixels)
                {
                    _boxSelecting = true;
                }
            }

            if (_dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                var additive = keyboard != null && keyboard.shiftKey.isPressed;

                if (_boxSelecting)
                {
                    SelectInBox(_dragStart, _dragCurrent, additive);
                }
                else if (_armedBuild >= 0)
                {
                    TryPlaceAtPoint(_dragCurrent);
                }
                else
                {
                    SelectAtPoint(_dragCurrent, additive);
                }

                _boxSelecting = false;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_armedBuild >= 0)
                {
                    _armedBuild = -1;
                }
                else
                {
                    ClearSelection();
                }
            }

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    ArmBuild(0);
                }

                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    ArmBuild(1);
                }

                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    ArmBuild(2);
                }

                if (keyboard.digit4Key.wasPressedThisFrame)
                {
                    ArmBuild(3);
                }

                if (keyboard.dKey.wasPressedThisFrame)
                {
                    DeploySelectedMcvs();
                }

                if (keyboard.zKey.wasPressedThisFrame)
                {
                    TryProduce(UnitKind.Infantry);
                }

                if (keyboard.xKey.wasPressedThisFrame)
                {
                    TryProduce(UnitKind.Tank);
                }

                if (keyboard.cKey.wasPressedThisFrame)
                {
                    TryProduce(UnitKind.Harvester);
                }

                if (keyboard.vKey.wasPressedThisFrame)
                {
                    TryProduce(UnitKind.Mcv);
                }

                if (keyboard.rKey.wasPressedThisFrame)
                {
                    ToggleRepairSelected();
                }

                if (keyboard.tKey.wasPressedThisFrame)
                {
                    SellSelectedBuildings();
                }
            }

            _selectedProducer = FindSelectedProducer();

            if (mouse.rightButton.wasPressedThisFrame && _selected.Count > 0)
            {
                var screenPosition = mouse.position.ReadValue();
                if (!TryIssueAttackOrder(screenPosition) &&
                    !TrySetRally(screenPosition))
                {
                    IssueMoveOrder(screenPosition);
                }
            }

            _movingCount = CountMovingUnits();
        }

        /// <summary>
        /// Right-click order: pick the terrain under the cursor, snap it to the nearest
        /// walkable nav cell, then write one MoveTarget per selected unit. The flow
        /// field is built lazily by <see cref="FlowFieldSystem"/> on the next fixed tick.
        /// </summary>
        private void IssueMoveOrder(Vector2 screenPosition)
        {
            if (!TerrainPicker.TryPick(_camera, screenPosition, _terrain, out var point))
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var destination = new float3(point.x, point.y, point.z);

            PruneSelection();

            var units = new NativeList<Entity>(_selected.Count, Allocator.Temp);
            try
            {
                for (var i = 0; i < _selected.Count; i++)
                {
                    var entity = _selected[i];
                    if (entityManager.Exists(entity) && entityManager.HasComponent<MoveTarget>(entity))
                    {
                        units.Add(entity);
                    }
                }

                if (MoveOrderIssuer.TryIssue(entityManager, units, destination, out var resolved))
                {
                    destination = resolved;
                }
            }
            finally
            {
                units.Dispose();
            }

            if (_moveMarker != null)
            {
                _moveMarker.Show(new Vector3(destination.x, destination.y, destination.z));
            }
        }

        /// <summary>
        /// Right-click on an enemy unit issues an attack order to every selected armed
        /// unit. The target is explicit, so a unit outside weapon range will chase;
        /// the combat system clears the order when the target dies.
        /// </summary>
        private bool TryIssueAttackOrder(Vector2 screenPosition)
        {
            if (!TryPickSelectable(screenPosition, out var target))
            {
                return false;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.HasComponent<UnitInfo>(target))
            {
                return false;
            }

            var targetTeam = entityManager.GetComponentData<UnitInfo>(target).Team;
            var ordered = 0;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<UnitInfo>(entity) ||
                    !entityManager.HasComponent<Weapon>(entity) ||
                    !entityManager.HasComponent<CombatState>(entity) ||
                    entityManager.GetComponentData<UnitInfo>(entity).Team == targetTeam)
                {
                    continue;
                }

                if (entityManager.GetComponentData<Weapon>(entity).Damage <= 0f)
                {
                    continue;
                }

                var state = entityManager.GetComponentData<CombatState>(entity);
                state.Target = target;
                state.Explicit = 1;
                entityManager.SetComponentData(entity, state);

                // Stop moving so the unit engages immediately (or chases in combat).
                var move = entityManager.GetComponentData<MoveTarget>(entity);
                move.HasOrder = 0;
                move.Field = Entity.Null;
                entityManager.SetComponentData(entity, move);

                ordered++;
            }

            if (ordered == 0)
            {
                return false;
            }

            if (_moveMarker != null)
            {
                var position = entityManager.GetComponentData<LocalTransform>(target).Position;
                _moveMarker.Show(new Vector3(position.x, position.y, position.z));
            }

            return true;
        }

        /// <summary>Screen-space pick of the closest unit under the cursor.</summary>
        private void ArmBuild(int index)
        {
            if (index < 0 || index >= BuildCatalog.PlayerBuildable.Length)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated ||
                BaseCommands.FindConstructionYard(world.EntityManager, BuildCatalog.PlayerTeam) == Entity.Null)
            {
                ShowMessage("Deploy an MCV first (select it, press D)");
                return;
            }

            _armedBuild = index;
            ShowMessage($"Placing {BuildCatalog.DisplayName(BuildCatalog.PlayerBuildable[index])} - left-click to place, Esc to cancel");
        }

        private Entity FindSelectedProducer()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return Entity.Null;
            }

            var entityManager = world.EntityManager;
            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (entityManager.Exists(entity) &&
                    entityManager.HasComponent<ProductionTag>(entity) &&
                    entityManager.HasComponent<BuildingInfo>(entity))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private void TryProduce(UnitKind kind)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var spec = ProductionCatalog.Get(kind);
            if (ProductionCommands.TryEnqueue(
                    world.EntityManager,
                    BuildCatalog.PlayerTeam,
                    kind,
                    _selectedProducer,
                    out var failure))
            {
                ShowMessage($"{spec.Label} queued  (-{spec.Cost})");
            }
            else
            {
                ShowMessage(ProductionFailureText(failure));
            }
        }

        private void ToggleRepairSelected()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var toggled = 0;
            var failure = BuildingCommandFailure.None;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<BuildingInfo>(entity))
                {
                    continue;
                }

                if (BaseCommands.TryToggleRepair(entityManager, entity, out failure))
                {
                    toggled++;
                }
            }

            ShowMessage(toggled > 0
                ? "Repair toggled"
                : BuildingCommandText(failure));
        }

        private void SellSelectedBuildings()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var refund = 0;
            var sold = 0;
            var failure = BuildingCommandFailure.None;

            for (var i = _selected.Count - 1; i >= 0; i--)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<BuildingInfo>(entity))
                {
                    continue;
                }

                if (BaseCommands.TrySell(entityManager, entity, out var value, out failure))
                {
                    refund += value;
                    sold++;
                    _selected.RemoveAt(i);
                }
            }

            ShowMessage(sold > 0
                ? $"Sold {sold} building(s) for {refund} credits"
                : BuildingCommandText(failure));
        }

        private static string BuildingCommandText(BuildingCommandFailure failure)
        {
            switch (failure)
            {
                case BuildingCommandFailure.NotABuilding:
                    return "Select a building first";
                case BuildingCommandFailure.NoEconomy:
                    return "Economy not initialised";
                case BuildingCommandFailure.AlreadyFull:
                    return "Building is at full health";
                case BuildingCommandFailure.CannotSell:
                    return "The Construction Yard cannot be sold";
                default:
                    return string.Empty;
            }
        }

        private static string ProductionFailureText(ProductionFailure failure)
        {
            switch (failure)
            {
                case ProductionFailure.NoBuilding:
                    return $"Build a {BuildCatalog.DisplayName(BuildingKind.Barracks)} or " +
                           $"{BuildCatalog.DisplayName(BuildingKind.WarFactory)} first";
                case ProductionFailure.NotEnoughCredits:
                    return "Not enough credits";
                case ProductionFailure.QueueFull:
                    return "Production queue is full";
                case ProductionFailure.NoEconomy:
                    return "Economy not initialised";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Right-click with production buildings selected (and no units) sets their rally
        /// point. Returns false when the selection is not building-only.
        /// </summary>
        private bool TrySetRally(Vector2 screenPosition)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            var hasProducer = false;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                if (entityManager.HasComponent<UnitTag>(entity))
                {
                    return false; // mixed selection with units: treat as a move order
                }

                if (entityManager.HasComponent<ProductionTag>(entity))
                {
                    hasProducer = true;
                }
            }

            if (!hasProducer ||
                !TerrainPicker.TryPick(_camera, screenPosition, _terrain, out var point))
            {
                return false;
            }

            var rally = new float3(point.x, point.y, point.z);
            var applied = 0;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (entityManager.Exists(entity) && entityManager.HasComponent<RallyPoint>(entity))
                {
                    entityManager.SetComponentData(entity, new RallyPoint
                    {
                        Value = rally,
                        HasValue = 1,
                    });
                    applied++;
                }
            }

            if (applied == 0)
            {
                return false;
            }

            if (_moveMarker != null)
            {
                _moveMarker.Show(new Vector3(rally.x, rally.y, rally.z));
            }

            ShowMessage("Rally point set");
            return true;
        }

        private void TryPlaceAtPoint(Vector2 screenPosition)
        {
            if (!TerrainPicker.TryPick(_camera, screenPosition, _terrain, out var point))
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var kind = BuildCatalog.PlayerBuildable[_armedBuild];
            if (BaseCommands.TryPlaceBuilding(
                    world.EntityManager,
                    BuildCatalog.PlayerTeam,
                    kind,
                    new Unity.Mathematics.float3(point.x, point.y, point.z),
                    out var failure))
            {
                _armedBuild = -1;
                ShowMessage($"{BuildCatalog.DisplayName(kind)} placed");
            }
            else
            {
                ShowMessage(FailureText(failure));
            }
        }

        private void DeploySelectedMcvs()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            PruneSelection();
            var entityManager = world.EntityManager;
            var deployed = 0;
            var hadMcv = false;
            var failure = BuildFailure.None;

            for (var i = _selected.Count - 1; i >= 0; i--)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<UnitInfo>(entity) ||
                    entityManager.GetComponentData<UnitInfo>(entity).Kind != UnitKind.Mcv)
                {
                    continue;
                }

                hadMcv = true;

                if (BaseCommands.TryDeployMcv(entityManager, entity, out failure))
                {
                    deployed++;
                    _selected.RemoveAt(i);
                }
            }

            if (deployed > 0)
            {
                ShowMessage("Construction Yard deployed");
            }
            else if (!hadMcv)
            {
                ShowMessage("Select an MCV first");
            }
            else
            {
                ShowMessage(FailureText(failure));
            }
        }

        private static string FailureText(BuildFailure failure)
        {
            switch (failure)
            {
                case BuildFailure.NoEconomy:
                    return "Economy not initialised";
                case BuildFailure.NotEnoughCredits:
                    return "Not enough credits";
                case BuildFailure.NoConstructionYard:
                    return "No Construction Yard";
                case BuildFailure.OutsideBuildRadius:
                    return "Too far from the Construction Yard";
                case BuildFailure.BadTerrain:
                    return "Cannot build there";
                case BuildFailure.Occupied:
                    return "That spot is occupied";
                case BuildFailure.AlreadyDeployed:
                    return "Construction Yard already deployed";
                case BuildFailure.NotAnMcv:
                    return "That is not an MCV";
                default:
                    return string.Empty;
            }
        }

        private void ShowMessage(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + 2.5f;
        }

        private bool TryPickSelectable(Vector2 screenPosition, out Entity picked)
        {
            var best = Entity.Null;
            var bestDistance = _pickRadiusPixels;

            ForEachSelectable((entity, unitPosition) =>
            {
                if (!TryProject(unitPosition, out var projected))
                {
                    return;
                }

                var distance = (projected - screenPosition).magnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = entity;
                }
            });

            picked = best;
            return best != Entity.Null;
        }

        private void SelectAtPoint(Vector2 screenPosition, bool additive)
        {
            PruneSelection();

            var hasPicked = TryPickSelectable(screenPosition, out var best);

            if (!additive)
            {
                ClearSelection();
            }

            if (!hasPicked)
            {
                return;
            }

            if (additive && _selected.Contains(best))
            {
                RemoveFromSelection(best);
            }
            else
            {
                AddToSelection(best);
            }
        }

        private void SelectInBox(Vector2 a, Vector2 b, bool additive)
        {
            PruneSelection();

            if (!additive)
            {
                ClearSelection();
            }

            var min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            var max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

            ForEachSelectable((entity, unitPosition) =>
            {
                if (!TryProject(unitPosition, out var projected))
                {
                    return;
                }

                if (projected.x >= min.x && projected.x <= max.x &&
                    projected.y >= min.y && projected.y <= max.y)
                {
                    AddToSelection(entity);
                }
            });
        }

        private void ForEachSelectable(System.Action<Entity, Vector3> action)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<LocalTransform>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitTag>(),
                    ComponentType.ReadOnly<BuildingTag>(),
                },
            });

            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<LocalTransform>(entity))
                {
                    continue;
                }

                action(entity, entityManager.GetComponentData<LocalTransform>(entity).Position);
            }
        }

        private bool TryProject(Vector3 worldPosition, out Vector2 screenPosition)
        {
            screenPosition = default;

            if (_camera == null)
            {
                return false;
            }

            var projected = _camera.WorldToScreenPoint(worldPosition);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPosition = new Vector2(projected.x, projected.y);
            return true;
        }

        private void AddToSelection(Entity entity)
        {
            if (!_selected.Contains(entity))
            {
                _selected.Add(entity);
            }

            SetSelectedFlag(entity, true);
        }

        private void RemoveFromSelection(Entity entity)
        {
            _selected.Remove(entity);
            SetSelectedFlag(entity, false);
        }

        private void ClearSelection()
        {
            for (var i = 0; i < _selected.Count; i++)
            {
                SetSelectedFlag(_selected[i], false);
            }

            _selected.Clear();
        }

        private void PruneSelection()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _selected.Clear();
                return;
            }

            var entityManager = world.EntityManager;
            for (var i = _selected.Count - 1; i >= 0; i--)
            {
                if (!entityManager.Exists(_selected[i]))
                {
                    _selected.RemoveAt(i);
                }
            }
        }

        private void SetSelectedFlag(Entity entity, bool selected)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<Selected>(entity))
            {
                return;
            }

            entityManager.SetComponentEnabled<Selected>(entity, selected);
        }

        private void OnGUI()
        {
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    normal = { textColor = new Color(0.95f, 0.92f, 0.82f) },
                };
            }

            if (_boxSelecting)
            {
                DrawMarquee(_dragStart, _dragCurrent);
            }

            DrawHealthBars();

            var moving = _movingCount;
            var healthText = SelectedHealthText();
            var hint = $"Selected: {_selected.Count}   Moving: {moving}{healthText}    1-4 build · D deploy · Z/X/C/V produce · R repair · T sell · RMB rally/move/attack · Esc clear";

            var match = ReadMatchState();
            if (match.HasValue)
            {
                var state = match.Value;
                var teamText = $"   R:{state.Team0Alive}  B:{state.Team1Alive}";
                var previousColor = GUI.color;

                GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.72f);
                GUI.DrawTexture(new Rect(12f, 12f, 220f, 30f), Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUI.Label(new Rect(22f, 16f, 210f, 24f), teamText, _hintStyle);

                if (state.GameOver != 0)
                {
                    DrawGameOverBanner(state.Winner);
                }
            }

            if (ReadEconomy(out var economy))
            {
                var powerMade = Economy.GetPowerMade(economy, BuildCatalog.PlayerTeam);
                var powerUsed = Economy.GetPowerUsed(economy, BuildCatalog.PlayerTeam);
                var economyText =
                    $"Credits: {Economy.GetCredits(economy, BuildCatalog.PlayerTeam)}" +
                    $"    Power: {powerUsed}/{powerMade}";

                var previousColor = GUI.color;
                GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.72f);
                GUI.DrawTexture(new Rect(12f, 48f, 300f, 30f), Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUI.Label(new Rect(22f, 52f, 290f, 24f), economyText, _hintStyle);
            }

            DrawBuildPanel();
            DrawProductionPanel();

            if (_message != null && Time.unscaledTime < _messageUntil)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.78f);
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 260f, Screen.height - 96f, 520f, 30f), Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height - 92f, 500f, 24f), _message, _hintStyle);
            }

            var previous = GUI.color;

            GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.72f);
            GUI.DrawTexture(new Rect(12f, Screen.height - 46f, 700f, 32f), Texture2D.whiteTexture);

            GUI.color = previous;
            GUI.Label(new Rect(22f, Screen.height - 42f, 690f, 26f), hint, _hintStyle);
        }

        private string SelectedHealthText()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || _selected.Count == 0)
            {
                return string.Empty;
            }

            var entityManager = world.EntityManager;
            var entity = _selected[0];
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<Health>(entity))
            {
                return string.Empty;
            }

            var health = entityManager.GetComponentData<Health>(entity);
            var repairing = entityManager.HasComponent<RepairState>(entity) &&
                            entityManager.GetComponentData<RepairState>(entity).Active != 0;
            var suffix = repairing ? "  [REPAIRING]" : string.Empty;
            return $"   HP: {Mathf.CeilToInt(health.Current)}/{Mathf.CeilToInt(health.Max)}{suffix}";
        }

        private static MatchState? ReadMatchState()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return null;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return null;
            }

            return entityManager.GetComponentData<MatchState>(query.GetSingletonEntity());
        }

        private static bool ReadEconomy(out EconomyState state)
        {
            state = default;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EconomyState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            state = entityManager.GetComponentData<EconomyState>(query.GetSingletonEntity());
            return true;
        }

        private void DrawHealthBars()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || _camera == null)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<UnitTag>(),
                    ComponentType.ReadOnly<BuildingTag>(),
                },
            });

            using var entities = query.ToEntityArray(Allocator.Temp);
            var previous = GUI.color;

            foreach (var entity in entities)
            {
                var health = entityManager.GetComponentData<Health>(entity);
                if (health.Current >= health.Max - 0.01f)
                {
                    continue;
                }

                var position = entityManager.GetComponentData<LocalTransform>(entity).Position;
                var isBuilding = entityManager.HasComponent<BuildingTag>(entity);
                var projected = _camera.WorldToScreenPoint(
                    new Vector3(position.x, position.y + (isBuilding ? 12f : 3f), position.z));

                if (projected.z <= 0f)
                {
                    continue;
                }

                var width = isBuilding ? 64f : 34f;
                var height = isBuilding ? 7f : 5f;
                var x = projected.x - width * 0.5f;
                var y = Screen.height - projected.y;

                var fraction = Mathf.Clamp01(health.Current / Mathf.Max(1f, health.Max));

                GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.75f);
                GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, height + 2f), Texture2D.whiteTexture);

                GUI.color = Color.Lerp(
                    new Color(0.72f, 0.16f, 0.12f),
                    new Color(0.42f, 0.72f, 0.24f),
                    fraction);
                GUI.DrawTexture(new Rect(x, y, width * fraction, height), Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        private void DrawBuildPanel()
        {
            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(0.95f, 0.92f, 0.82f) },
                };
            }

            var width = 310f;
            var height = 30f + BuildCatalog.PlayerBuildable.Length * 24f + 24f;
            var x = Screen.width - width - 12f;
            var y = Screen.height - height - 54f;

            var previous = GUI.color;
            GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.74f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 22f), "BUILD", _panelStyle);

            for (var i = 0; i < BuildCatalog.PlayerBuildable.Length; i++)
            {
                var kind = BuildCatalog.PlayerBuildable[i];
                var spec = BuildCatalog.Get(kind);
                var marker = _armedBuild == i ? ">" : " ";
                var line = $"{marker} [{i + 1}] {BuildCatalog.DisplayName(kind),-16} {spec.Cost}";
                GUI.Label(new Rect(x + 10f, y + 30f + i * 24f, width - 20f, 22f), line, _panelStyle);
            }

            GUI.Label(
                new Rect(x + 10f, y + 30f + BuildCatalog.PlayerBuildable.Length * 24f, width - 20f, 22f),
                "   [D] Deploy MCV  [Esc] Cancel",
                _panelStyle);
        }

        private void DrawProductionPanel()
        {
            var width = 400f;
            var height = 92f;
            var x = 12f;
            var y = Screen.height - height - 54f;

            var previous = GUI.color;
            GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.74f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(
                new Rect(x + 10f, y + 6f, width - 20f, 22f),
                "PRODUCTION   [Z] Infantry 100   [X] Tank 350",
                _panelStyle);
            GUI.Label(
                new Rect(x + 10f, y + 30f, width - 20f, 22f),
                $"             [C] Harvester 500   [V] MCV 1000",
                _panelStyle);

            var queueText = "Select a Barracks or War Factory to see its queue";
            if (_selectedProducer != Entity.Null && ReadQueueText(_selectedProducer, out var text))
            {
                queueText = "Queue: " + text;
            }

            GUI.Label(new Rect(x + 10f, y + 56f, width - 20f, 22f), queueText, _panelStyle);
        }

        private static bool ReadQueueText(Entity producer, out string text)
        {
            text = string.Empty;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(producer) ||
                !entityManager.HasComponent<ProductionOrder>(producer))
            {
                return false;
            }

            var queue = entityManager.GetBuffer<ProductionOrder>(producer, true);
            if (queue.Length == 0)
            {
                text = "idle";
                return true;
            }

            var parts = new List<string>(queue.Length);
            for (var i = 0; i < queue.Length; i++)
            {
                var order = queue[i];
                var label = ProductionCatalog.Get(order.Kind).Label;
                parts.Add(i == 0
                    ? $"{label} {order.Remaining:0.0}s"
                    : $"{label} {order.Total:0.0}s");
            }

            text = string.Join(" · ", parts);
            return true;
        }

        private void DrawGameOverBanner(byte winner)
        {
            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 42,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.98f, 0.86f, 0.55f) },
                };
            }

            var text = winner switch
            {
                0 => "RED TEAM WINS",
                1 => "BLUE TEAM WINS",
                _ => "DRAW",
            };

            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.02f, 0.78f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.42f, Screen.width, 86f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(0f, Screen.height * 0.42f + 18f, Screen.width, 50f), text, _bannerStyle);
        }

        private int CountMovingUnits()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return 0;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MoveTarget>(),
                ComponentType.ReadOnly<UnitTag>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var moving = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                if (entityManager.GetComponentData<MoveTarget>(entities[i]).HasOrder != 0)
                {
                    moving++;
                }
            }

            return moving;
        }

        private static void DrawMarquee(Vector2 start, Vector2 end)
        {
            var xMin = Mathf.Min(start.x, end.x);
            var xMax = Mathf.Max(start.x, end.x);
            var yMin = Mathf.Min(start.y, end.y);
            var yMax = Mathf.Max(start.y, end.y);

            // IMGUI uses a top-left origin; Input System screen coordinates use bottom-left.
            var rect = new Rect(xMin, Screen.height - yMax, xMax - xMin, yMax - yMin);
            var previous = GUI.color;

            GUI.color = new Color(0.98f, 0.82f, 0.45f, 0.14f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = new Color(0.98f, 0.82f, 0.45f, 0.85f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1.5f, rect.width, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1.5f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1.5f, rect.y, 1.5f, rect.height), Texture2D.whiteTexture);

            GUI.color = previous;
        }
    }
}
