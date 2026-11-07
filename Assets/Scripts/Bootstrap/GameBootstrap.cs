using Unity.Entities;
using UnityEngine;

namespace CQRa.Core
{
    /// <summary>
    /// Entry point. One of these lives in every gameplay scene.
    /// Phase 1 responsibility: configure the simulation clock and prove the game boots.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Simulation ticks per second. Fixed so replays stay deterministic.</summary>
        public const int SimHz = 30;

        public static GameBootstrap Instance { get; private set; }

        [SerializeField] private bool _logOnBoot = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Time.fixedDeltaTime = 1f / SimHz;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;

            ConfigureSimulationClock();

            if (_logOnBoot)
            {
                Debug.Log($"[CQRa] bootstrap ok | sim={SimHz}Hz | {Screen.width}x{Screen.height} | {SystemInfo.graphicsDeviceType}");
            }
        }

        /// <summary>
        /// Entities' fixed-step group defaults to 60 Hz. Lock it to the simulation rate
        /// so movement, pathfinding and every future gameplay system share one tick.
        /// </summary>
        private static void ConfigureSimulationClock()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[CQRa] no ECS world at boot; fixed step left at default");
                return;
            }

            var group = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (group == null)
            {
                Debug.LogWarning("[CQRa] FixedStepSimulationSystemGroup missing; fixed step left at default");
                return;
            }

            group.Timestep = 1f / SimHz;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
