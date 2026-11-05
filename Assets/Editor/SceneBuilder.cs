using System;
using System.IO;
using CQRa.Sim.Units;
using CQRa.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CQRa.EditorTools
{
    /// <summary>
    /// Generates the gameplay scene from code. No manual Editor work required.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SettingsDir = "Assets/Settings";

        public static void CreateMainScene()
        {
            try
            {
                EnsureDir("Assets/Scenes");
                EnsureDir(SettingsDir);
                EnsureDir("Assets/Materials");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                ConfigureAtmosphere();

                var terrain = CreateTerrain();
                var camera = CreateCamera(terrain);
                CreateSun();
                CreateFillLight();
                CreateUnits(terrain);
                CreateSelection(camera, terrain);
                CreateBootstrap();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[SceneBuilder] created {ScenePath} (camera {camera.name}, terrain {terrain.Size}m)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[SceneBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Diagnose()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                var cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                Debug.Log($"[Diag] camera pos={cam.transform.position} rot={cam.transform.eulerAngles} fov={cam.fieldOfView}");

                var terrain = UnityEngine.Object.FindFirstObjectByType<HeightmapTerrain>();
                Debug.Log($"[Diag] terrain present={terrain != null} size={(terrain != null ? terrain.Size : 0f)}");

                var spawner = UnityEngine.Object.FindFirstObjectByType<UnitSpawner>();
                Debug.Log($"[Diag] spawner present={spawner != null}");

                var selection = UnityEngine.Object.FindFirstObjectByType<SelectionController>();
                Debug.Log($"[Diag] selection present={selection != null}");

                var rig = UnityEngine.Object.FindFirstObjectByType<RtsCameraRig>();
                Debug.Log($"[Diag] rig present={rig != null}");

                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    Debug.Log($"[Diag] renderer {r.name} bounds={r.bounds.size}");
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

        private static void ConfigureAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.56f, 0.48f, 0.37f);
            RenderSettings.fogDensity = 0.0024f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.27f, 0.21f);
            RenderSettings.ambientIntensity = 1f;
        }

        private static HeightmapTerrain CreateTerrain()
        {
            var go = new GameObject("Terrain");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            var terrain = go.AddComponent<HeightmapTerrain>();
            var material = NewMaterial("Terrain", Color.white, 0.06f, 0f);

            terrain.Material = material;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return terrain;
        }

        private static Camera CreateCamera(HeightmapTerrain terrain)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.56f, 0.48f, 0.37f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 900f;

            go.AddComponent<AudioListener>();

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var rig = go.AddComponent<RtsCameraRig>();
            rig.Configure(terrain, cam, true);
            rig.SetPose(new Vector3(0f, 0f, -8f), 0f, 46f, 115f);

            return cam;
        }

        private static void CreateSun()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(26f, -40f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.intensity = 2.0f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.88f;
        }

        /// <summary>
        /// Bounced-dust fill from the camera side. Without this, vertical faces fall
        /// back to the cold ambient probe and warm team colours read as blue-grey.
        /// </summary>
        private static void CreateFillLight()
        {
            var go = new GameObject("FillLight");
            go.transform.rotation = Quaternion.Euler(34f, 0f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.70f, 0.56f);
            light.intensity = 0.85f;
            light.shadows = LightShadows.None;
        }

        private static void CreateUnits(HeightmapTerrain terrain)
        {
            var go = new GameObject("Units");
            var spawner = go.AddComponent<UnitSpawner>();
            var material = NewMaterial("Unit", Color.white, 0.32f, 0.18f);
            spawner.Configure(terrain, material);
        }

        private static void CreateSelection(Camera camera, HeightmapTerrain terrain)
        {
            var go = new GameObject("Selection");
            var selection = go.AddComponent<SelectionController>();
            selection.Configure(camera, terrain);
        }

        private static void CreateBootstrap()
        {
            var go = new GameObject("GameBootstrap");
            go.AddComponent<CQRa.Core.GameBootstrap>();
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
