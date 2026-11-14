using System;
using CQRa.Sim.AI;
using UnityEngine;

namespace CQRa.Sim.Campaign
{
    [Serializable]
    public sealed class MissionDto
    {
        public string id;
        public string name;
        public string briefing;
        public string difficulty;
        public string objective;
        public string objectiveText;
        public float surviveSeconds;
        public string playerFaction;
        public string aiFaction;
    }

    [Serializable]
    public sealed class MissionListDto
    {
        public MissionDto[] missions;
    }

    /// <summary>
    /// Loads hand-authored campaign missions from Resources/Data/missions.json and falls
    /// back to five built-in missions when the asset is missing.
    /// </summary>
    public static class MissionCatalog
    {
        private const string ResourcePath = "Data/missions";

        private static readonly MissionDto[] Fallback =
        {
            new MissionDto
            {
                id = "m1", name = "First Contact", difficulty = "Recruit",
                objective = "destroyAll",
                objectiveText = "Destroy every enemy unit and structure.",
                briefing = "A hostile commander has dug in across the basin. Establish your base and wipe them out.",
                playerFaction = "Ashfall",
                aiFaction = "Verdant",
            },
            new MissionDto
            {
                id = "m2", name = "Hold the Line", difficulty = "Veteran",
                objective = "survive", surviveSeconds = 300f,
                objectiveText = "Survive for 5 minutes.",
                briefing = "Enemy pressure is building. Fortify, keep your harvesters running, and outlast the assault.",
                playerFaction = "Meridian",
                aiFaction = "Verdant",
            },
            new MissionDto
            {
                id = "m3", name = "Break Their Base", difficulty = "Hardened",
                objective = "destroyYard",
                objectiveText = "Destroy the enemy Construction Yard.",
                briefing = "Strike the heart of the enemy base. Kill their Construction Yard and they cannot rebuild.",
                playerFaction = "Ashfall",
                aiFaction = "Meridian",
            },
            new MissionDto
            {
                id = "m4", name = "Iron Tide", difficulty = "Hardened",
                objective = "survive", surviveSeconds = 360f,
                objectiveText = "Survive for 6 minutes.",
                briefing = "The Syndicate is spending everything to break you. Hold the basin and keep your economy alive.",
                playerFaction = "Verdant",
                aiFaction = "Meridian",
            },
            new MissionDto
            {
                id = "m5", name = "The Last Compact", difficulty = "Brutal",
                objective = "destroyYard",
                objectiveText = "Destroy the Ashfall Construction Yard.",
                briefing = "Ashfall has committed its last field command. Break their yard and end the campaign.",
                playerFaction = "Meridian",
                aiFaction = "Ashfall",
            },
        };

        public static MissionDto[] Missions { get; private set; } = Fallback;
        public static bool LoadedFromDisk { get; private set; }

        public static void Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Missions = Fallback;
                LoadedFromDisk = false;
                return;
            }

            try
            {
                var dto = JsonUtility.FromJson<MissionListDto>(asset.text);
                if (dto != null && dto.missions != null && dto.missions.Length > 0)
                {
                    Missions = dto.missions;
                    LoadedFromDisk = true;
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Campaign] failed to parse missions: {e.Message}");
            }

            Missions = Fallback;
            LoadedFromDisk = false;
        }

        public static AiDifficulty DifficultyFor(MissionDto mission)
        {
            return Enum.TryParse(mission.difficulty, true, out AiDifficulty difficulty)
                ? difficulty
                : AiDifficulty.Veteran;
        }

        public static bool IsCompleted(int index)
        {
            return PlayerPrefs.GetInt(Key(index), 0) != 0;
        }

        public static void MarkCompleted(int index)
        {
            PlayerPrefs.SetInt(Key(index), 1);
            PlayerPrefs.Save();
        }

        public static bool IsUnlocked(int index)
        {
            return index == 0 || IsCompleted(index - 1);
        }

        private static string Key(int index)
        {
            return $"cq_mission_{index}_done";
        }
    }
}
