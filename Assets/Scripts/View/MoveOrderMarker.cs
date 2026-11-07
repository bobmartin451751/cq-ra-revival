using UnityEngine;
using UnityEngine.Rendering;

namespace CQRa.View
{
    /// <summary>
    /// Short-lived ground ping shown where a move order was issued.
    /// Purely presentational; the order itself lives in ECS.
    /// </summary>
    public sealed class MoveOrderMarker : MonoBehaviour
    {
        [SerializeField] private HeightmapTerrain _terrain;
        [SerializeField] private Material _template;
        [SerializeField] private float _duration = 0.85f;
        [SerializeField] private float _outerRadius = 3.4f;
        [SerializeField] private float _innerRadius = 2.5f;

        private GameObject _ring;
        private Mesh _mesh;
        private Material _material;
        private float _remaining;

        public void Configure(HeightmapTerrain terrain, Material template)
        {
            _terrain = terrain;
            _template = template;
        }

        private void Awake()
        {
            Build();
            _ring.SetActive(false);
        }

        private void Build()
        {
            _mesh = ProceduralMeshes.Ring(_outerRadius, _innerRadius, 48);

            var shader = _template != null
                ? _template.shader
                : Shader.Find("Universal Render Pipeline/Lit");

            _material = new Material(shader) { name = "MoveMarker" };
            _material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.20f));
            _material.SetFloat("_Smoothness", 0.35f);
            _material.SetFloat("_Metallic", 0f);
            _material.EnableKeyword("_EMISSION");
            _material.SetColor("_EmissionColor", new Color(0.9f, 0.55f, 0.10f));

            _ring = new GameObject("Ring");
            _ring.transform.SetParent(transform, false);

            _ring.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = _ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Show(Vector3 worldPosition)
        {
            if (_ring == null)
            {
                return;
            }

            var y = _terrain != null
                ? _terrain.SampleHeight(worldPosition.x, worldPosition.z)
                : worldPosition.y;

            transform.position = new Vector3(worldPosition.x, y + 0.08f, worldPosition.z);
            _remaining = _duration;
            _ring.SetActive(true);
        }

        private void Update()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            var t = 1f - Mathf.Clamp01(_remaining / _duration);
            var scale = Mathf.Lerp(1.25f, 0.85f, t);
            _ring.transform.localScale = new Vector3(scale, 1f, scale);

            if (_remaining <= 0f)
            {
                _ring.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
