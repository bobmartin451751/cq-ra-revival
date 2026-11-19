using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CQRa.Core
{
    /// <summary>F3 development overlay for quick 8-player performance checks.</summary>
    public sealed class PerformanceHud : MonoBehaviour
    {
        private bool _visible;
        private float _timer;
        private int _units;
        private int _buildings;
        private int _fps;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
            {
                return;
            }

            _timer = 0.5f;
            _fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CQRa.Sim.Units.UnitTag>());
            using var buildingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CQRa.Sim.Base.BuildingTag>());
            _units = unitQuery.CalculateEntityCount();
            _buildings = buildingQuery.CalculateEntityCount();
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.78f);
            GUI.DrawTexture(new Rect(10f, 10f, 190f, 100f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, 14f, 178f, 20f), $"FPS: {_fps}");
            GUI.Label(new Rect(18f, 34f, 178f, 20f), $"Units: {_units}");
            GUI.Label(new Rect(18f, 54f, 178f, 20f), $"Buildings: {_buildings}");
            GUI.Label(new Rect(18f, 74f, 178f, 20f), $"F3: hide");
        }
    }
}
