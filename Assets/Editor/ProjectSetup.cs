using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CQRa.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project configuration. Invoked headlessly via:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod CQRa.EditorTools.ProjectSetup.Configure
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string RendererPath = SettingsDir + "/CQRa_Renderer.asset";
        private const string PipelinePath = SettingsDir + "/CQRa_URP.asset";

        public static void Configure()
        {
            try
            {
                ConfigurePlayer();
                ConfigureUrp();
                ConfigureInput();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ProjectSetup] OK");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[ProjectSetup] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "CQRA";
            PlayerSettings.productName = "CQ R-A Revival";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            Debug.Log("[ProjectSetup] player settings applied");
        }

        private static void ConfigureUrp()
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                Debug.Log("[ProjectSetup] created URP assets");
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            Debug.Log("[ProjectSetup] URP assigned");
        }

        private static void ConfigureInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[ProjectSetup] ProjectSettings.asset not found; skipping input handler");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null)
            {
                handler.intValue = 1; // 0 = legacy, 1 = new Input System, 2 = both
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[ProjectSetup] active input handler -> Input System");
            }
        }
    }
}
