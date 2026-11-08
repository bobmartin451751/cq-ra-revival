using UnityEngine;

namespace CQRa.Sim.Content
{
    /// <summary>
    /// Loads the JSON content table at scene start and compiles it into the static
    /// ContentDatabase blob. Falls back to defaults when the asset is missing so tests,
    /// captures and the editor never break.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class ContentLoader : MonoBehaviour
    {
        [SerializeField] private string _resourcePath = "Data/content";

        private void Awake()
        {
            if (ContentDatabase.HasLoaded)
            {
                return;
            }

            var asset = Resources.Load<TextAsset>(_resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Content] Resources/{_resourcePath}.json not found; using defaults");
                return;
            }

            if (ContentDatabase.LoadFromJson(asset.text))
            {
                Debug.Log($"[Content] loaded Resources/{_resourcePath}.json");
            }
            else
            {
                Debug.LogError($"[Content] failed to load Resources/{_resourcePath}.json");
            }
        }

        private void OnDestroy()
        {
            ContentDatabase.ResetToDefaults();
        }
    }
}
