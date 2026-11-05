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

            if (_logOnBoot)
            {
                Debug.Log($"[CQRa] bootstrap ok | sim={SimHz}Hz | {Screen.width}x{Screen.height} | {SystemInfo.graphicsDeviceType}");
            }
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
