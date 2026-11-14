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
        public string mapId;
        public int teamCount = 2;
        public string teamMode = "FreeForAll";
    }

    [Serializable]
    public sealed class MissionListDto
    {
        public MissionDto[] missions;
    }

    /// <summary>
    /// Loads hand-authored campaign missions from Resources/Data/missions.json and falls
    /// back to fifteen built-in missions when the asset is missing.
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
                mapId = "ash-basin",
                teamCount = 2,
            },
            new MissionDto
            {
                id = "m2", name = "Hold the Line", difficulty = "Veteran",
                objective = "survive", surviveSeconds = 300f,
                objectiveText = "Survive for 5 minutes.",
                briefing = "Enemy pressure is building. Fortify, keep your harvesters running, and outlast the assault.",
                playerFaction = "Meridian",
                aiFaction = "Verdant",
                mapId = "rusted-flats",
                teamCount = 2,
            },
            new MissionDto
            {
                id = "m3", name = "Break Their Base", difficulty = "Hardened",
                objective = "destroyYard",
                objectiveText = "Destroy the enemy Construction Yard.",
                briefing = "Strike the heart of the enemy base. Kill their Construction Yard and they cannot rebuild.",
                playerFaction = "Ashfall",
                aiFaction = "Meridian",
                mapId = "broken-ridge",
                teamCount = 2,
            },
            new MissionDto
            {
                id = "m4", name = "Iron Tide", difficulty = "Hardened",
                objective = "survive", surviveSeconds = 360f,
                objectiveText = "Survive for 6 minutes.",
                briefing = "The Syndicate is spending everything to break you. Hold the basin and keep your economy alive.",
                playerFaction = "Verdant",
                aiFaction = "Meridian",
                mapId = "twin-shallows",
                teamCount = 2,
            },
            new MissionDto
            {
                id = "m5", name = "The Last Compact", difficulty = "Brutal",
                objective = "destroyYard",
                objectiveText = "Destroy the Ashfall Construction Yard.",
                briefing = "Ashfall has committed its last field command. Break their yard and end the campaign.",
                playerFaction = "Meridian",
                aiFaction = "Ashfall",
                mapId = "flooded-delta",
                teamCount = 2,
            },
            new MissionDto
            {
                id = "m6", name = "Industrial Push", difficulty = "Veteran",
                objective = "destroyAll",
                objectiveText = "Destroy every enemy unit and structure.",
                briefing = "Three commanders contest the Ashfall works. Secure the basin and break their production.",
                playerFaction = "Ashfall",
                aiFaction = "Verdant",
                mapId = "ash-basin-11",
                teamCount = 3,
            },
            new MissionDto
            {
                id = "m7", name = "Dry Lightning", difficulty = "Veteran",
                objective = "destroyYard",
                objectiveText = "Destroy the enemy Construction Yard.",
                briefing = "No water, no cover, no retreat. Cross the flats and hit the yard before reinforcements arrive.",
                playerFaction = "Verdant",
                aiFaction = "Meridian",
                mapId = "rusted-flats-12",
                teamCount = 4,
            },
            new MissionDto
            {
                id = "m8", name = "Ridge War", difficulty = "Hardened",
                objective = "survive", surviveSeconds = 420f,
                objectiveText = "Survive for 7 minutes.",
                briefing = "Three factions are fighting over the ridge lakes. Hold your shore and outlast them.",
                playerFaction = "Meridian",
                aiFaction = "Ashfall",
                mapId = "broken-ridge-13",
                teamCount = 4,
            },
            new MissionDto
            {
                id = "m9", name = "Twin Blockade", difficulty = "Hardened",
                objective = "destroyYard",
                objectiveText = "Destroy every enemy Construction Yard.",
                briefing = "Two lakes, two fleets, one objective. Break the blockade and take the yards.",
                playerFaction = "Ashfall",
                aiFaction = "Verdant",
                mapId = "twin-shallows-14",
                teamCount = 4,
            },
            new MissionDto
            {
                id = "m10", name = "Delta Run", difficulty = "Hardened",
                objective = "destroyAll",
                objectiveText = "Destroy every enemy unit and structure.",
                briefing = "The delta is flooded and contested. Use the water or lose the coast.",
                playerFaction = "Verdant",
                aiFaction = "Meridian",
                mapId = "flooded-delta-15",
                teamCount = 4,
            },
            new MissionDto
            {
                id = "m11", name = "Long Front", difficulty = "Hardened",
                objective = "survive", surviveSeconds = 480f,
                objectiveText = "Survive for 8 minutes.",
                briefing = "A six-way front is collapsing. Hold the line while the others exhaust themselves.",
                playerFaction = "Ashfall",
                aiFaction = "Meridian",
                mapId = "ash-basin-21",
                teamCount = 6,
            },
            new MissionDto
            {
                id = "m12", name = "Rust Bowl", difficulty = "Brutal",
                objective = "destroyYard",
                objectiveText = "Destroy the enemy Construction Yards.",
                briefing = "Six armies, no water, and one dry bowl. Break the largest yard first.",
                playerFaction = "Verdant",
                aiFaction = "Ashfall",
                mapId = "rusted-flats-22",
                teamCount = 6,
            },
            new MissionDto
            {
                id = "m13", name = "Ring of Steel", difficulty = "Brutal",
                objective = "destroyAll",
                objectiveText = "Destroy every enemy unit and structure.",
                briefing = "The ridge lake is surrounded. Take the ring and close it.",
                playerFaction = "Meridian",
                aiFaction = "Verdant",
                mapId = "broken-ridge-23",
                teamCount = 6,
            },
            new MissionDto
            {
                id = "m14", name = "Four Lakes", difficulty = "Brutal",
                objective = "destroyYard",
                objectiveText = "Destroy every enemy Construction Yard.",
                briefing = "Six factions and two lakes. Split your fleet or lose the shore.",
                playerFaction = "Ashfall",
                aiFaction = "Verdant",
                mapId = "twin-shallows-24",
                teamCount = 6,
            },
            new MissionDto
            {
                id = "m15", name = "The Last Compact", difficulty = "Brutal",
                objective = "destroyAll",
                objectiveText = "Destroy every enemy unit and structure.",
                briefing = "Eight commanders enter the delta. One alliance leaves. End the campaign.",
                playerFaction = "Meridian",
                aiFaction = "Ashfall",
                mapId = "broken-ridge-33",
                teamCount = 8,
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
