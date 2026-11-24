using System;
using System.IO;
using CQRa.Sim.Content;
using Unity.Entities;
using UnityEngine;

namespace CQRa.Sim.Core
{
    /// <summary>Three named save slots with a stable on-disk format.</summary>
    public static class SaveSlots
    {
        public const int Count = 3;

        private static int _current;

        public static int CurrentSlot => _current;

        public static void Select(int slot)
        {
            _current = Mathf.Clamp(slot, 0, Count - 1);
        }

        public static string PathFor(int slot)
        {
            slot = Mathf.Clamp(slot, 0, Count - 1);
            return Path.Combine(
                Application.persistentDataPath,
                $"cqra-save-slot{slot + 1}.json");
        }

        public static bool Exists(int slot)
        {
            return File.Exists(PathFor(slot));
        }

        public static MatchSaveData Load(int slot)
        {
            return MatchSaveSerializer.LoadFromFile(PathFor(slot));
        }

        public static bool Save(EntityManager entityManager, int slot)
        {
            slot = Mathf.Clamp(slot, 0, Count - 1);
            var data = MatchSaveSerializer.Capture(entityManager);
            File.WriteAllText(PathFor(slot), MatchSaveSerializer.ToJson(data));
            return true;
        }

        public static string Describe(int slot)
        {
            var data = Load(slot);
            if (data == null)
            {
                return $"SLOT {slot + 1}: EMPTY";
            }

            var mapIndex = MapCatalog.FindIndex(data.mapId);
            var mapName = mapIndex >= 0 ? MapCatalog.Get(mapIndex).Name : data.mapId;
            var stamp = "saved";
            if (DateTime.TryParse(data.savedUtc, out var parsed))
            {
                stamp = parsed.ToLocalTime().ToString("MMM d HH:mm");
            }

            return $"SLOT {slot + 1}: {mapName}  {data.teamCount}p  {data.difficulty} AI  {stamp}";
        }
    }
}
