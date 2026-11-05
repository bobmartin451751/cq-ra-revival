using System;
using System.IO;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CQRa.Core
{
    /// <summary>
    /// Development-only capture rig. Activated by passing <c>-screenshot &lt;path&gt;</c>
    /// on the command line. Renders the main camera through the normal URP frame loop
    /// into a RenderTexture, writes a PNG, then exits the process.
    /// </summary>
    public sealed class ScreenshotCapture : MonoBehaviour
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const int WarmupFrames = 4;

        private Camera _camera;
        private RenderTexture _target;
        private string _path;
        private bool _selectAll;
        private int _frames;

        private void Start()
        {
            _path = ReadArg("-screenshot");
            if (string.IsNullOrEmpty(_path))
            {
                enabled = false;
                return;
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("[Capture] no main camera");
                Quit(1);
                return;
            }

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "CaptureTarget"
            };
            _target.Create();

            _camera.targetTexture = _target;
            _camera.aspect = (float)Width / Height;

            _selectAll = HasArg("-selectall");

            Debug.Log($"[Capture] armed -> {_path}");
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            _frames++;

            // Units are spawned in Start, so wait a frame before selecting them.
            if (_selectAll && _frames == 2)
            {
                SelectAllUnits();
            }

            if (_frames < WarmupFrames)
            {
                return;
            }

            try
            {
                var previous = RenderTexture.active;
                RenderTexture.active = _target;

                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();

                RenderTexture.active = previous;

                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(_path, tex.EncodeToPNG());
                Destroy(tex);

                Debug.Log($"[Capture] wrote {_path} ({new FileInfo(_path).Length} bytes)");
                Quit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Capture] failed: " + e);
                Quit(1);
            }
        }

        private void OnDestroy()
        {
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }

        private static bool HasArg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Development helper: enables selection on every unit for visual capture.</summary>
        private static void SelectAllUnits()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UnitTag>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<Selected>(entity))
                {
                    entityManager.SetComponentEnabled<Selected>(entity, true);
                }
            }

            Debug.Log($"[Capture] selected {entities.Length} units");
        }

        private static string ReadArg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
