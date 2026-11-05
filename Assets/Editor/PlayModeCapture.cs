using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CQRa.EditorTools
{
    /// <summary>
    /// Opens the main scene, installs the capture rig, and enters play mode.
    /// The rig itself writes the PNG and exits the process.
    /// Invoke with: -executeMethod CQRa.EditorTools.PlayModeCapture.Run -screenshot &lt;path&gt;
    /// </summary>
    public static class PlayModeCapture
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                var go = new GameObject("CaptureRig");
                go.AddComponent<CQRa.Core.ScreenshotCapture>();

                Debug.Log("[Capture] entering play mode");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogError("[Capture] setup failed: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
