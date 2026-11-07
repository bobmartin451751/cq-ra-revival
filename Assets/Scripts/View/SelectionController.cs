using System.Collections.Generic;
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
                else
                {
                    SelectAtPoint(_dragCurrent, additive);
                }

                _boxSelecting = false;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClearSelection();
            }

            if (mouse.rightButton.wasPressedThisFrame && _selected.Count > 0)
            {
                IssueMoveOrder(mouse.position.ReadValue());
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

        private void SelectAtPoint(Vector2 screenPosition, bool additive)
        {
            PruneSelection();

            var best = Entity.Null;
            var bestDistance = _pickRadiusPixels;

            ForEachUnit((entity, unitPosition) =>
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

            if (!additive)
            {
                ClearSelection();
            }

            if (best == Entity.Null)
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

            ForEachUnit((entity, unitPosition) =>
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

        private void ForEachUnit(System.Action<Entity, Vector3> action)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitTag>(),
                ComponentType.ReadOnly<LocalTransform>());

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

            var moving = _movingCount;
            var hint = $"Selected: {_selected.Count}   Moving: {moving}    LMB select · drag box · Shift add · RMB move · Esc clear";
            var previous = GUI.color;

            GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.72f);
            GUI.DrawTexture(new Rect(12f, Screen.height - 46f, 640f, 32f), Texture2D.whiteTexture);

            GUI.color = previous;
            GUI.Label(new Rect(22f, Screen.height - 42f, 630f, 26f), hint, _hintStyle);
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
