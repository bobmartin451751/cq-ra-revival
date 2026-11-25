using CQRa.Sim.Content;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CQRa.Core
{
    /// <summary>F3 development overlay: frame times, entities and world diagnostics.</summary>
    public sealed class PerformanceHud : MonoBehaviour
    {
        private readonly PerformanceStats _stats = new PerformanceStats(180);
        private bool _visible;
        private float _timer;
        private int _units;
        private int _buildings;
        private int _entities;
        private int _fps;
        private float _averageMs;
        private float _worstMs;
        private string _weather = "Clear";
        private long _memoryMb;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }

            _stats.Add(Time.unscaledDeltaTime * 1000f);

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
            {
                return;
            }

            _timer = 0.5f;
            _fps = Mathf.RoundToInt(_stats.AverageFps());
            _averageMs = _stats.AverageMs();
            _worstMs = _stats.WorstMs();
            _memoryMb = System.GC.GetTotalMemory(false) / (1024L * 1024L);

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
            using var bridgeQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CQRa.Sim.Bridges.BridgeTag>());
            using var flowQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CQRa.Sim.Navigation.FlowFieldMeta>());
            _units = unitQuery.CalculateEntityCount();
            _buildings = buildingQuery.CalculateEntityCount();
            _entities = entityManager.UniversalQuery.CalculateEntityCount();
            _bridges = bridgeQuery.CalculateEntityCount();
            _flowFields = flowQuery.CalculateEntityCount();

            var weather = Object.FindFirstObjectByType<CQRa.View.WeatherDirector>();
            _weather = weather != null
                ? WeatherCatalog.DisplayName(weather.Kind)
                : "Clear";
        }

        private int _bridges;
        private int _flowFields;

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.78f);
            GUI.DrawTexture(new Rect(10f, 10f, 240f, 208f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, 14f, 228f, 20f), $"FPS: {_fps}  ({_averageMs:0.00} ms)");
            GUI.Label(new Rect(18f, 34f, 228f, 20f), $"Worst: {_worstMs:0.00} ms");
            GUI.Label(new Rect(18f, 54f, 228f, 20f), $"Entities: {_entities}");
            GUI.Label(new Rect(18f, 74f, 228f, 20f), $"Units: {_units}  Buildings: {_buildings}");
            GUI.Label(new Rect(18f, 94f, 228f, 20f), $"Bridges: {_bridges}  Flow fields: {_flowFields}");
            GUI.Label(new Rect(18f, 114f, 228f, 20f), $"Weather: {_weather}");
            GUI.Label(new Rect(18f, 134f, 228f, 20f), $"Managed: {_memoryMb} MB");
            GUI.Label(new Rect(18f, 154f, 228f, 20f), $"Sim: {1f / GameBootstrap.SimHz:0.000}s fixed");
            GUI.Label(new Rect(18f, 178f, 228f, 20f), "F3: hide");
        }
    }
}
