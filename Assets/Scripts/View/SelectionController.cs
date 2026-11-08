using System.Collections.Generic;
using CQRa.Sim.Base;
using CQRa.Sim.Factions;
using CQRa.Sim.Fog;
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
        private enum ArmedCommand : byte
        {
            None = 0,
            DeployMcv = 1,
            SetRally = 2,
            Capture = 3,
        }

        private const float SidebarWidth = 324f;

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
        private GUIStyle _panelStyle;
        private GUIStyle _sidebarTitle;
        private GUIStyle _sidebarButton;

        private int _armedBuild = -1;
        private ArmedCommand _armedCommand = ArmedCommand.None;
        private int _sidebarTab;
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
            var pointer = mouse.position.ReadValue();
            var overHud = IsPointerOverSidebar(pointer);

            if (mouse.leftButton.wasPressedThisFrame && !overHud)
            {
                _dragging = true;
                _boxSelecting = false;
                _dragStart = pointer;
                _dragCurrent = _dragStart;
            }

            if (_dragging)
            {
                _dragCurrent = pointer;

                if (!_boxSelecting &&
                    (_dragCurrent - _dragStart).sqrMagnitude > _dragThresholdPixels * _dragThresholdPixels)
                {
                    _boxSelecting = true;
                }
            }

            if (_dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;

                if (!overHud)
                {
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
                }

                _boxSelecting = false;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_armedCommand != ArmedCommand.None)
                {
                    _armedCommand = ArmedCommand.None;
                    ShowMessage("Command cancelled");
                }
                else if (_armedBuild >= 0)
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

                if (keyboard.digit5Key.wasPressedThisFrame)
                {
                    ArmBuild(4);
                }

                if (keyboard.digit6Key.wasPressedThisFrame)
                {
                    ArmBuild(5);
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

                if (keyboard.bKey.wasPressedThisFrame)
                {
                    TryProduce(UnitKind.Gunship);
                }

                if (keyboard.gKey.wasPressedThisFrame)
                {
                    TryProduce(UnitKind.Engineer);
                }

                if (keyboard.yKey.wasPressedThisFrame)
                {
                    TryProduce(ProductionCatalog.UniqueFor(
                        FactionRuntime.ForTeam(BuildCatalog.PlayerTeam)));
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

            if (mouse.rightButton.wasPressedThisFrame && !overHud && _selected.Count > 0)
            {
                var screenPosition = mouse.position.ReadValue();

                if (_armedCommand == ArmedCommand.DeployMcv)
                {
                    TryDeployMcvAtPoint(screenPosition);
                }
                else if (_armedCommand == ArmedCommand.SetRally)
                {
                    if (TrySetRally(screenPosition))
                    {
                        _armedCommand = ArmedCommand.None;
                    }
                }
                else if (_armedCommand == ArmedCommand.Capture)
                {
                    if (TryCaptureAtPoint(screenPosition))
                    {
                        _armedCommand = ArmedCommand.None;
                    }
                }
                else if (!TryIssueAttackOrder(screenPosition) &&
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
            if (!TryPickSelectable(screenPosition, false, out var target))
            {
                return false;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            if (!TryGetTeam(entityManager, target, out var targetTeam) ||
                targetTeam == BuildCatalog.PlayerTeam)
            {
                return false;
            }

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
            _armedCommand = ArmedCommand.None;
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
                // Walls stay armed so the player can chain segments.
                if (kind != BuildingKind.Wall)
                {
                    _armedBuild = -1;
                }

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
                case BuildFailure.TooFar:
                    return "Too far from the MCV";
                default:
                    return string.Empty;
            }
        }

        private void ShowMessage(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + 2.5f;
        }

        private static bool IsPointerOverSidebar(Vector2 pointer)
        {
            return pointer.x >= Screen.width - SidebarWidth;
        }

        private void ArmCommand(ArmedCommand command)
        {
            _armedBuild = -1;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;

            if (command == ArmedCommand.DeployMcv)
            {
                if (FindSelectedMcv(entityManager) == Entity.Null)
                {
                    ShowMessage("Select an MCV first");
                    return;
                }

                _armedCommand = command;
                ShowMessage("Right-click a site to deploy the Construction Yard");
                return;
            }

            if (command == ArmedCommand.SetRally)
            {
                if (_selectedProducer == Entity.Null)
                {
                    ShowMessage("Select a Barracks, War Factory or Refinery");
                    return;
                }

                _armedCommand = command;
                ShowMessage("Right-click to set the rally point");
                return;
            }

            if (command == ArmedCommand.Capture)
            {
                if (FindSelectedEngineer(entityManager) == Entity.Null)
                {
                    ShowMessage("Select an Engineer first");
                    return;
                }

                _armedCommand = command;
                ShowMessage("Right-click an enemy building within 10 m to capture it");
                return;
            }

            _armedCommand = ArmedCommand.None;
        }

        private Entity FindSelectedEngineer(EntityManager entityManager)
        {
            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<UnitInfo>(entity))
                {
                    continue;
                }

                if (entityManager.GetComponentData<UnitInfo>(entity).Kind == UnitKind.Engineer)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private bool TryCaptureAtPoint(Vector2 screenPosition)
        {
            if (!TryPickSelectable(screenPosition, false, out var target))
            {
                ShowMessage("Right-click an enemy building to capture");
                return false;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            if (!entityManager.HasComponent<BuildingInfo>(target) ||
                !entityManager.HasComponent<BuildingTag>(target))
            {
                ShowMessage("Engineers can only capture buildings");
                return false;
            }

            var engineer = FindSelectedEngineer(entityManager);
            if (engineer == Entity.Null)
            {
                ShowMessage("Select an Engineer first");
                return false;
            }

            if (BaseCommands.TryCaptureBuilding(
                    entityManager, engineer, target, out var failure))
            {
                _selected.Remove(engineer);
                ShowMessage("Building captured");
                return true;
            }

            ShowMessage(CaptureFailureText(failure));
            return false;
        }

        private static string CaptureFailureText(CaptureFailure failure)
        {
            switch (failure)
            {
                case CaptureFailure.NotAnEngineer:
                    return "Select an Engineer first";
                case CaptureFailure.NotABuilding:
                    return "Engineers can only capture buildings";
                case CaptureFailure.AlreadyOwned:
                    return "That building is already yours";
                case CaptureFailure.TooFar:
                    return "Move the Engineer closer (10 m)";
                default:
                    return string.Empty;
            }
        }

        private Entity FindSelectedMcv(EntityManager entityManager)
        {
            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<UnitInfo>(entity))
                {
                    continue;
                }

                if (entityManager.GetComponentData<UnitInfo>(entity).Kind == UnitKind.Mcv)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private void TryDeployMcvAtPoint(Vector2 screenPosition)
        {
            if (!TerrainPicker.TryPick(_camera, screenPosition, _terrain, out var point))
            {
                ShowMessage("Cannot deploy there");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var mcv = FindSelectedMcv(entityManager);
            if (mcv == Entity.Null)
            {
                _armedCommand = ArmedCommand.None;
                ShowMessage("Select an MCV first");
                return;
            }

            if (BaseCommands.TryDeployMcvAt(
                    entityManager,
                    mcv,
                    new Unity.Mathematics.float3(point.x, point.y, point.z),
                    out var failure))
            {
                _selected.Remove(mcv);
                _armedCommand = ArmedCommand.None;
                ShowMessage("Construction Yard deployed");
            }
            else
            {
                ShowMessage(FailureText(failure));
            }
        }

        private void ToggleDeploySelectedThumpers()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var toggled = 0;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<DeployState>(entity))
                {
                    continue;
                }

                if (BaseCommands.TryToggleDeploy(entityManager, entity))
                {
                    toggled++;
                }
            }

            ShowMessage(toggled > 0 ? "Thumper deployment toggled" : "No Thumper selected");
        }

        private void StopSelectedUnits()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var stopped = 0;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity) ||
                    !entityManager.HasComponent<UnitTag>(entity) ||
                    !entityManager.HasComponent<MoveTarget>(entity))
                {
                    continue;
                }

                var move = entityManager.GetComponentData<MoveTarget>(entity);
                move.HasOrder = 0;
                move.Field = Entity.Null;
                entityManager.SetComponentData(entity, move);

                if (entityManager.HasComponent<CombatState>(entity))
                {
                    entityManager.SetComponentData(entity, new CombatState
                    {
                        Target = Entity.Null,
                        Explicit = 0,
                    });
                }

                stopped++;
            }

            ShowMessage(stopped > 0 ? "Units stopped" : "No units selected");
        }

        private bool TryPickSelectable(Vector2 screenPosition, bool playerOnly, out Entity picked)
        {
            var best = Entity.Null;
            var bestDistance = _pickRadiusPixels;

            ForEachSelectable((entity, unitPosition) =>
            {
                if (playerOnly && !IsPlayerSelectable(entity))
                {
                    return;
                }

                if (!playerOnly && !IsVisibleToPlayer(unitPosition))
                {
                    return;
                }

                if (!playerOnly && IsCloakHiddenFromPlayer(entity))
                {
                    return;
                }

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

        private static bool IsVisibleToPlayer(Vector3 position)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return true;
            }

            return FogQuery.IsVisible(
                world.EntityManager,
                BuildCatalog.PlayerTeam,
                position);
        }

        public static bool IsPlayerSelectable(EntityManager entityManager, Entity entity)
        {
            return TryGetTeam(entityManager, entity, out var team) &&
                   team == BuildCatalog.PlayerTeam;
        }

        private bool IsPlayerSelectable(Entity entity)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            return IsPlayerSelectable(world.EntityManager, entity);
        }

        private static bool TryGetTeam(EntityManager entityManager, Entity entity, out int team)
        {
            if (entityManager.HasComponent<UnitInfo>(entity))
            {
                team = entityManager.GetComponentData<UnitInfo>(entity).Team;
                return true;
            }

            if (entityManager.HasComponent<BuildingInfo>(entity))
            {
                team = entityManager.GetComponentData<BuildingInfo>(entity).Team;
                return true;
            }

            team = 0;
            return false;
        }

        private static bool IsCloakHiddenFromPlayer(Entity entity)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            return CloakQuery.IsHiddenFrom(
                world.EntityManager,
                entity,
                BuildCatalog.PlayerTeam);
        }

        private void SelectAtPoint(Vector2 screenPosition, bool additive)
        {
            PruneSelection();
            _armedCommand = ArmedCommand.None;

            var hasPicked = TryPickSelectable(screenPosition, true, out var best);

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
            _armedCommand = ArmedCommand.None;

            if (!additive)
            {
                ClearSelection();
            }

            var min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            var max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

            ForEachSelectable((entity, unitPosition) =>
            {
                if (!IsPlayerSelectable(entity))
                {
                    return;
                }

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
            _armedCommand = ArmedCommand.None;

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
            EnsureStyles();

            if (_boxSelecting)
            {
                DrawMarquee(_dragStart, _dragCurrent);
            }

            DrawHealthBars();
            DrawSidebar();

            if (_message != null && Time.unscaledTime < _messageUntil)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.8f);
                GUI.DrawTexture(
                    new Rect(12f, Screen.height - 96f, 560f, 30f),
                    Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUI.Label(new Rect(22f, Screen.height - 92f, 540f, 24f), _message, _hintStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_hintStyle != null)
            {
                return;
            }

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.93f, 0.90f, 0.80f) },
            };

            _panelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.88f, 0.85f, 0.76f) },
            };

            _sidebarTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.86f, 0.55f) },
            };

            _sidebarButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.95f, 0.92f, 0.82f) },
                hover = { textColor = new Color(1f, 0.94f, 0.72f) },
            };
        }

        private void DrawSidebar()
        {
            var x = Screen.width - SidebarWidth;
            var previous = GUI.color;

            GUI.color = new Color(0.055f, 0.05f, 0.045f, 0.96f);
            GUI.DrawTexture(new Rect(x, 0f, SidebarWidth, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x + 10f, 8f, SidebarWidth - 20f, 28f), "COMMAND", _sidebarTitle);

            if (ReadEconomy(out var economy))
            {
                var credits = Economy.GetCredits(economy, BuildCatalog.PlayerTeam);
                var made = Economy.GetPowerMade(economy, BuildCatalog.PlayerTeam);
                var used = Economy.GetPowerUsed(economy, BuildCatalog.PlayerTeam);
                GUI.Label(
                    new Rect(x + 12f, 38f, SidebarWidth - 24f, 22f),
                    $"Credits: {credits}",
                    _hintStyle);
                GUI.Label(
                    new Rect(x + 12f, 58f, SidebarWidth - 24f, 22f),
                    $"Power: {used}/{made}",
                    _hintStyle);
            }

            if (ReadMatchState(out var match))
            {
                GUI.Label(
                    new Rect(x + 12f, 78f, SidebarWidth - 24f, 22f),
                    $"Red: {match.Team0Alive}   Blue: {match.Team1Alive}",
                    _hintStyle);
            }

            var mapRect = new Rect(x + 10f, 104f, SidebarWidth - 20f, 168f);
            DrawMinimap(mapRect);

            DrawSelectionSummary(x);
            DrawCommandButtons(x);
            DrawSupplyTabs(x);
        }

        private void DrawSelectionSummary(float x)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;

            if (_selected.Count == 0)
            {
                GUI.Label(
                    new Rect(x + 12f, 284f, SidebarWidth - 24f, 22f),
                    "No selection",
                    _hintStyle);
                return;
            }

            var entity = _selected[0];
            if (!entityManager.Exists(entity))
            {
                return;
            }

            string title = "Unknown";
            if (entityManager.HasComponent<UnitInfo>(entity))
            {
                title = entityManager.GetComponentData<UnitInfo>(entity).Kind.ToString();
            }
            else if (entityManager.HasComponent<BuildingInfo>(entity))
            {
                title = BuildCatalog.DisplayName(
                    entityManager.GetComponentData<BuildingInfo>(entity).Kind);
            }

            var healthText = SelectedHealthText();
            GUI.Label(
                new Rect(x + 12f, 284f, SidebarWidth - 24f, 22f),
                $"{title}  {healthText}",
                _hintStyle);
        }

        private void DrawCommandButtons(float x)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || _selected.Count == 0)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var hasMcv = false;
            var hasUnit = false;
            var hasBuilding = false;
            var hasProducer = false;
            var hasRally = false;
            var hasEngineer = false;
            var hasThumper = false;
            var thumperDeployed = false;

            for (var i = 0; i < _selected.Count; i++)
            {
                var entity = _selected[i];
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                if (entityManager.HasComponent<UnitInfo>(entity))
                {
                    hasUnit = true;
                    if (entityManager.GetComponentData<UnitInfo>(entity).Kind == UnitKind.Mcv)
                    {
                        hasMcv = true;
                    }

                    if (entityManager.GetComponentData<UnitInfo>(entity).Kind == UnitKind.Engineer)
                    {
                        hasEngineer = true;
                    }

                    if (entityManager.GetComponentData<UnitInfo>(entity).Kind == UnitKind.Thumper)
                    {
                        hasThumper = true;
                        if (entityManager.HasComponent<DeployState>(entity) &&
                            entityManager.GetComponentData<DeployState>(entity).Deployed != 0)
                        {
                            thumperDeployed = true;
                        }
                    }
                }

                if (entityManager.HasComponent<BuildingInfo>(entity))
                {
                    hasBuilding = true;
                    if (entityManager.HasComponent<RallyPoint>(entity))
                    {
                        hasRally = true;
                    }
                }

                if (entityManager.HasComponent<ProductionTag>(entity))
                {
                    hasProducer = true;
                }
            }

            var buttons = 0;
            if (hasMcv) buttons++;
            if (hasRally) buttons++;
            if (hasEngineer) buttons++;
            if (hasThumper) buttons++;
            if (hasUnit && !hasEngineer && !hasThumper) buttons++;
            if (hasBuilding) buttons += 2; // repair + sell
            if (buttons == 0)
            {
                return;
            }

            const float gap = 4f;
            var totalWidth = SidebarWidth - 24f;
            var width = (totalWidth - gap * (buttons - 1)) / buttons;
            var bx = x + 12f;
            const float by = 312f;

            if (hasMcv)
            {
                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    "DEPLOY",
                    _armedCommand == ArmedCommand.DeployMcv,
                    () => ArmCommand(ArmedCommand.DeployMcv));
                bx += width + gap;
            }

            if (hasRally)
            {
                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    "RALLY",
                    _armedCommand == ArmedCommand.SetRally,
                    () => ArmCommand(ArmedCommand.SetRally));
                bx += width + gap;
            }

            if (hasEngineer)
            {
                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    "CAPTURE",
                    _armedCommand == ArmedCommand.Capture,
                    () => ArmCommand(ArmedCommand.Capture));
                bx += width + gap;
            }

            if (hasThumper)
            {
                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    thumperDeployed ? "UNDEPLOY" : "DEPLOY",
                    false,
                    ToggleDeploySelectedThumpers);
                bx += width + gap;
            }

            if (hasUnit && !hasEngineer && !hasThumper)
            {
                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    "STOP",
                    false,
                    StopSelectedUnits);
                bx += width + gap;
            }

            if (hasBuilding)
            {
                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    "REPAIR",
                    false,
                    ToggleRepairSelected);
                bx += width + gap;

                DrawCommandButton(
                    new Rect(bx, by, width, 32f),
                    "SELL",
                    false,
                    SellSelectedBuildings);
            }
        }

        private void DrawCommandButton(Rect rect, string label, bool armed, System.Action action)
        {
            var previous = GUI.backgroundColor;
            if (armed)
            {
                GUI.backgroundColor = new Color(0.78f, 0.52f, 0.20f);
            }

            if (GUI.Button(rect, label, _sidebarButton))
            {
                action();
            }

            GUI.backgroundColor = previous;
        }

        private void DrawSupplyTabs(float x)
        {
            const float by = 354f;
            var half = (SidebarWidth - 28f) * 0.5f;

            var previous = GUI.backgroundColor;
            GUI.backgroundColor = _sidebarTab == 0
                ? new Color(0.62f, 0.44f, 0.20f)
                : new Color(0.26f, 0.24f, 0.20f);
            if (GUI.Button(new Rect(x + 12f, by, half, 28f), "STRUCTURES", _sidebarButton))
            {
                _sidebarTab = 0;
            }

            GUI.backgroundColor = _sidebarTab == 1
                ? new Color(0.62f, 0.44f, 0.20f)
                : new Color(0.26f, 0.24f, 0.20f);
            if (GUI.Button(new Rect(x + 16f + half, by, half, 28f), "UNITS", _sidebarButton))
            {
                _sidebarTab = 1;
            }

            GUI.backgroundColor = previous;

            var rowY = by + 34f;
            if (_sidebarTab == 0)
            {
                DrawStructureRows(x, rowY);
            }
            else
            {
                DrawUnitRows(x, rowY);
            }

            var rows = _sidebarTab == 0
                ? BuildCatalog.PlayerBuildable.Length
                : ProductionCatalog.RosterFor(FactionRuntime.ForTeam(BuildCatalog.PlayerTeam)).Length;
            var queueY = rowY + rows * 31f + 4f;

            var queueText = "Queue: select a producer";
            if (_selectedProducer != Entity.Null && ReadQueueText(_selectedProducer, out var text))
            {
                queueText = "Queue: " + text;
            }

            GUI.Label(
                new Rect(x + 12f, queueY, SidebarWidth - 24f, 22f),
                queueText,
                _panelStyle);

            GUI.Label(
                new Rect(x + 12f, Screen.height - 84f, SidebarWidth - 24f, 78f),
                "RMB: move / attack / execute\nShift: add Â· Esc: cancel Â· R/T: repair/sell\nG: Engineer Â· CAPTURE: RMB enemy building",
                _panelStyle);
        }

        private void DrawStructureRows(float x, float rowY)
        {
            for (var i = 0; i < BuildCatalog.PlayerBuildable.Length; i++)
            {
                var kind = BuildCatalog.PlayerBuildable[i];
                var spec = BuildCatalog.Get(kind);
                var label = $"[{i + 1}] {BuildCatalog.DisplayName(kind)}    {spec.Cost}";

                var previous = GUI.backgroundColor;
                if (_armedBuild == i)
                {
                    GUI.backgroundColor = new Color(0.78f, 0.52f, 0.20f);
                }

                if (GUI.Button(
                        new Rect(x + 12f, rowY + i * 31f, SidebarWidth - 24f, 28f),
                        label,
                        _sidebarButton))
                {
                    ArmBuild(i);
                }

                GUI.backgroundColor = previous;
            }
        }

        private void DrawUnitRows(float x, float rowY)
        {
            var faction = FactionRuntime.ForTeam(BuildCatalog.PlayerTeam);
            var roster = ProductionCatalog.RosterFor(faction);

            for (var i = 0; i < roster.Length; i++)
            {
                var kind = roster[i];
                var spec = ProductionCatalog.Get(kind);
                var name = ProductionCatalog.UnitName(faction, kind);
                var label =
                    $"[{ProductionCatalog.Hotkey(i)}] {name}    {spec.Cost}   {spec.Seconds:0}s";

                if (GUI.Button(
                        new Rect(x + 12f, rowY + i * 31f, SidebarWidth - 24f, 28f),
                        label,
                        _sidebarButton))
                {
                    TryProduce(kind);
                }
            }
        }

        private void DrawMinimap(Rect rect)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.02f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;

            var size = _terrain != null ? _terrain.Size : 360f;
            var half = size * 0.5f;

            ForEachSelectable((entity, position) =>
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return;
                }

                var entityManager = world.EntityManager;
                var team = entityManager.HasComponent<UnitInfo>(entity)
                    ? entityManager.GetComponentData<UnitInfo>(entity).Team
                    : entityManager.GetComponentData<BuildingInfo>(entity).Team;

                if (team != 0 && !IsVisibleToPlayer(position))
                {
                    return;
                }

                var nx = (position.x + half) / size;
                var nz = (position.z + half) / size;
                if (nx < 0f || nx > 1f || nz < 0f || nz > 1f)
                {
                    return;
                }

                var dot = new Rect(
                    rect.x + nx * rect.width - 2f,
                    rect.y + (1f - nz) * rect.height - 2f,
                    4f,
                    4f);

                GUI.color = team == 0
                    ? new Color(0.85f, 0.22f, 0.16f)
                    : new Color(0.28f, 0.55f, 0.78f);
                GUI.DrawTexture(dot, Texture2D.whiteTexture);
            });

            GUI.color = previous;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 120f, 20f), "RADAR", _panelStyle);
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

        private static bool ReadMatchState(out MatchState state)
        {
            state = default;
            var match = ReadMatchState();
            if (!match.HasValue)
            {
                return false;
            }

            state = match.Value;
            return true;
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
            var height = 112f;
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
                $"             [C] Harvester 500   [V] MCV 1000   [B] Gunship 900",
                _panelStyle);

            var queueText = "Select a Barracks or War Factory to see its queue";
            if (_selectedProducer != Entity.Null && ReadQueueText(_selectedProducer, out var text))
            {
                queueText = "Queue: " + text;
            }

            GUI.Label(new Rect(x + 10f, y + 58f, width - 20f, 22f), queueText, _panelStyle);
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

            text = string.Join(" Â· ", parts);
            return true;
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
