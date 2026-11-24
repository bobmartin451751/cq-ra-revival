using System;
using System.IO;
using UnityEngine;

namespace CQRa.Sim.Content
{
    /// <summary>
    /// Serialisable editor settings. The full map is regenerated from these four values,
    /// so custom maps stay tiny and always match the deterministic generator.
    /// </summary>
    [Serializable]
    public sealed class CustomMapData
    {
        public int version = 1;
        public int seed;
        public int tier;
        public int water;
        public int teams;
    }

    public static class CustomMapStore
    {
        public const int CurrentVersion = 1;

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, "cqra-custom-map.json");

        public static CustomMapData Capture(int seed, MapSizeTier tier, bool water, int teams)
        {
            return new CustomMapData
            {
                version = CurrentVersion,
                seed = seed,
                tier = (int)tier,
                water = water ? 1 : 0,
                teams = teams,
            };
        }

        public static string ToJson(CustomMapData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static CustomMapData FromJson(string json)
        {
            var data = JsonUtility.FromJson<CustomMapData>(json);
            return data != null && data.version == CurrentVersion ? data : null;
        }

        public static void Save(CustomMapData data, string path)
        {
            File.WriteAllText(path, ToJson(data));
        }

        public static CustomMapData Load(string path)
        {
            return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
        }

        public static MapDefinition BuildMap(CustomMapData data)
        {
            if (data == null)
            {
                return null;
            }

            var tier = (MapSizeTier)Mathf.Clamp(data.tier, 0, 3);
            return CustomMapFactory.Build(
                data.seed,
                tier,
                data.water != 0,
                data.teams);
        }
    }
}
