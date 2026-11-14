using System;
using CQRa.Sim.Content;
using UnityEngine;

namespace CQRa.Core
{
    /// <summary>
    /// Development/screenshot hook: <c>-map N</c> selects a skirmish map before the
    /// scene's terrain and water are generated.
    /// </summary>
    public static class MapStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyCommandLineMap()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-map", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(args[i + 1], out var index))
                {
                    MapRuntime.Select(index);
                }

                return;
            }
        }
    }
}
