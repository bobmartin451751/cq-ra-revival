using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CQRa.EditorTools
{
    /// <summary>
    /// Generates the Phase 1 scene from code and renders a PNG for visual verification.
    /// No manual Editor work required.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SettingsDir = "Assets/Settings";
        private const string ShotDir = ".temp/screens";

        public static void CreateMainScene()
        {
            try
            {
                EnsureDir("Assets/Scenes");
                EnsureDir(SettingsDir);
                EnsureDir("Assets/Materials");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camera = CreateCamera();
                CreateSun();
                CreateGround();
                CreateScaleReferences();
                CreateBootstrap();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[SceneBuilder] created {ScenePath} (camera {camera.name})");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[SceneBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureScreenshot()
        {
            try
            {
                if (!File.Exists(ScenePath))
                {
                    throw new FileNotFoundException("Main scene missing; run CreateMainScene first", ScenePath);
                }

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                var cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                {
                    throw new InvalidOperationException("No camera in scene");
                }

                const int w = 1600;
                const int h = 900;

                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                rt.Create();

                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;

                cam.targetTexture = rt;
                cam.aspect = (float)w / h;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                RenderTexture.active = prevActive;
                cam.targetTexture = prevTarget;

                var outPath = Path.GetFullPath(Path.Combine(ShotDir, "phase1.png"));
                EnsureDir(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());

                UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);

                Debug.Log($"[Screenshot] wrote {outPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Screenshot] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Diagnose()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                var cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                Debug.Log($"[Diag] camera pos={cam.transform.position} rot={cam.transform.eulerAngles} fov={cam.fieldOfView} aspect={cam.aspect}");

                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    var b = r.bounds;
                    var mat = r.sharedMaterial;
                    var col = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : "?";
                    Debug.Log($"[Diag] {r.name} center={b.center} size={b.size} enabled={r.enabled} mat={mat?.name} color={col}");
                }

                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                Debug.Log($"[Diag] lights={lights.Length}");
                foreach (var l in lights)
                {
                    Debug.Log($"[Diag] light {l.name} type={l.type} intensity={l.intensity} rot={l.transform.eulerAngles}");
                }

                Debug.Log($"[Diag] active SRP={GraphicsSettings.defaultRenderPipeline?.name}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Diag] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 58f, -58f);
            go.transform.LookAt(new Vector3(0f, 0f, 8f));

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.55f, 0.44f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 600f;

            go.AddComponent<AudioListener>();

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            return cam;
        }

        private static void CreateSun()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.90f, 0.74f);
            light.intensity = 1.7f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
        }

        private static void CreateGround()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.localScale = new Vector3(36f, 1f, 36f); // 360m x 360m

            var mat = NewMaterial("Ground", new Color(0.42f, 0.34f, 0.24f), 0.92f, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider); // no physics for terrain
            }
        }

        /// <summary>
        /// Real-world scale markers: 1.2m infantry, 7m tank, 40m refinery.
        /// Keeps every future procedural mesh honest about proportions.
        /// </summary>
        private static void CreateScaleReferences()
        {
            var rust = NewMaterial("Scale_Rust", new Color(0.55f, 0.26f, 0.14f), 0.5f, 0.35f);
            var steel = NewMaterial("Scale_Steel", new Color(0.33f, 0.34f, 0.35f), 0.45f, 0.6f);

            MakeBox("Ref_Refinery", new Vector3(-26f, 4f, 10f), new Vector3(40f, 8f, 30f), rust);
            MakeBox("Ref_Tank_A", new Vector3(-6f, 1.25f, 2f), new Vector3(7f, 2.5f, 3.2f), steel);
            MakeBox("Ref_Tank_B", new Vector3(3f, 1.25f, 4f), new Vector3(7f, 2.5f, 3.2f), steel);
            MakeBox("Ref_Tank_C", new Vector3(11f, 1.25f, 1f), new Vector3(7f, 2.5f, 3.2f), steel);
            MakeBox("Ref_Infantry", new Vector3(18f, 0.6f, 8f), new Vector3(1.2f, 1.2f, 1.2f), rust);
        }

        private static void CreateBootstrap()
        {
            var go = new GameObject("GameBootstrap");
            go.AddComponent<CQRa.Core.GameBootstrap>();
        }

        private static void MakeBox(string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static Material NewMaterial(string name, Color color, float smoothness, float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader not found");
            }

            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            var path = $"Assets/Materials/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
