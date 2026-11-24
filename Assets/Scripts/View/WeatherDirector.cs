using CQRa.Sim.Content;
using UnityEngine;

namespace CQRa.View
{
    /// <summary>
    /// Visual-only weather. A deterministic per-map weather kind drives a particle
    /// effect and scene tint. Nothing here touches ECS, vision, pathing or damage.
    /// </summary>
    public sealed class WeatherDirector : MonoBehaviour
    {
        private ParticleSystem _particles;
        private Material _material;
        private Camera _camera;

        public WeatherKind Kind { get; private set; }

        private void Start()
        {
            _camera = Camera.main;
            Kind = WeatherCatalog.ForCurrentMap();
            Build(Kind);
        }

        private void Build(WeatherKind kind)
        {
            ApplySceneTint(kind);
            if (kind == WeatherKind.Clear)
            {
                return;
            }

            _particles = gameObject.AddComponent<ParticleSystem>();
            var main = _particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = kind == WeatherKind.Dust ? 1600 : 4000;

            var emission = _particles.emission;
            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(90f, 1f, 90f);

            var velocity = _particles.velocityOverLifetime;
            velocity.enabled = true;

            var renderer = GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _material = new Material(shader) { name = $"Weather_{kind}" };
            renderer.sharedMaterial = _material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            switch (kind)
            {
                case WeatherKind.Rain:
                    main.startLifetime = 1.6f;
                    main.startSize = 0.16f;
                    main.startSpeed = 0f;
                    main.startColor = new Color(0.68f, 0.78f, 0.86f, 0.55f);
                    emission.rateOverTime = 2200f;
                    velocity.y = -34f;
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 5f;
                    renderer.velocityScale = 0.08f;
                    _material.SetColor("_BaseColor", new Color(0.68f, 0.78f, 0.86f, 0.55f));
                    break;

                case WeatherKind.Snow:
                    main.startLifetime = 9f;
                    main.startSize = 0.22f;
                    main.startSpeed = 0f;
                    main.startColor = new Color(0.95f, 0.96f, 0.98f, 0.8f);
                    emission.rateOverTime = 900f;
                    velocity.x = 1.6f;
                    velocity.y = -3.2f;
                    velocity.z = 0.8f;
                    _material.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.98f, 0.8f));
                    break;

                case WeatherKind.Dust:
                    main.startLifetime = 6f;
                    main.startSize = 0.5f;
                    main.startSpeed = 0f;
                    main.startColor = new Color(0.62f, 0.50f, 0.32f, 0.35f);
                    emission.rateOverTime = 700f;
                    velocity.x = 11f;
                    velocity.y = -0.6f;
                    velocity.z = 2.2f;
                    _material.SetColor("_BaseColor", new Color(0.62f, 0.50f, 0.32f, 0.35f));
                    break;
            }
        }

        private static void ApplySceneTint(WeatherKind kind)
        {
            RenderSettings.fog = kind != WeatherKind.Clear;
            switch (kind)
            {
                case WeatherKind.Rain:
                    RenderSettings.fogColor = new Color(0.42f, 0.47f, 0.52f);
                    RenderSettings.fogDensity = 0.0016f;
                    RenderSettings.ambientLight = new Color(0.68f, 0.72f, 0.80f);
                    break;
                case WeatherKind.Snow:
                    RenderSettings.fogColor = new Color(0.72f, 0.76f, 0.82f);
                    RenderSettings.fogDensity = 0.0020f;
                    RenderSettings.ambientLight = new Color(0.80f, 0.82f, 0.88f);
                    break;
                case WeatherKind.Dust:
                    RenderSettings.fogColor = new Color(0.62f, 0.50f, 0.34f);
                    RenderSettings.fogDensity = 0.0024f;
                    RenderSettings.ambientLight = new Color(0.74f, 0.66f, 0.54f);
                    break;
                default:
                    RenderSettings.fogDensity = 0.002f;
                    RenderSettings.ambientLight = new Color(0.58f, 0.58f, 0.56f);
                    break;
            }

            if (RenderSettings.fog)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }
        }

        private void LateUpdate()
        {
            if (_particles == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                // Keep the emitter centred above the camera so weather is always visible.
                transform.position = _camera.transform.position + Vector3.up * 26f;
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
