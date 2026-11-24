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
        public string codex;
        public string[] next;
        public string[] requires;
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
                EnsureGraph();
                return;
            }

            try
            {
                var dto = JsonUtility.FromJson<MissionListDto>(asset.text);
                if (dto != null && dto.missions != null && dto.missions.Length > 0)
                {
                    Missions = dto.missions;
                    LoadedFromDisk = true;
                    EnsureGraph();
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Campaign] failed to parse missions: {e.Message}");
            }

            Missions = Fallback;
            LoadedFromDisk = false;
            EnsureGraph();
        }

        /// <summary>
        /// Hand-authored branching graph and codex notes. Values already present in
        /// missions.json win; missing fields are filled from this table.
        /// </summary>
        private static void EnsureGraph()
        {
            if (Missions == null)
            {
                return;
            }

            for (var i = 0; i < Missions.Length; i++)
            {
                var mission = Missions[i];
                if (mission == null)
                {
                    continue;
                }

                switch (mission.id)
                {
                    case "m1":
                        SetGraph(mission, new[] { "m2", "m3" }, Array.Empty<string>(),
                            "Ashfall holds the basin with salvaged crawlers. The Compact's doctrine is attrition: more guns, more ore, more bodies.");
                        break;
                    case "m2":
                        SetGraph(mission, new[] { "m4" }, new[] { "m1" },
                            "Meridian's coastal enclave runs on imported parts and orbital weather data. Their commanders prefer clean fields of fire.");
                        break;
                    case "m3":
                        SetGraph(mission, new[] { "m5" }, new[] { "m1" },
                            "The Verdant Combine grows its war machine. Spore silos and seeded armour make every captured basin self-supplying.");
                        break;
                    case "m4":
                        SetGraph(mission, new[] { "m6", "m7" }, new[] { "m2" },
                            "Verdant bio-tanks trade speed for regeneration. Burn their growth pods before the field matures.");
                        break;
                    case "m5":
                        SetGraph(mission, new[] { "m7" }, new[] { "m3" },
                            "Meridian's lance batteries are orbital relics. Their ground crews are few, but every shot is aimed from above.");
                        break;
                    case "m6":
                        SetGraph(mission, new[] { "m8" }, new[] { "m4" },
                            "Ashfall foundries never fully stop. Capture the works and the Compact loses its spare-parts lifeline.");
                        break;
                    case "m7":
                        SetGraph(mission, new[] { "m9" }, new[] { "m4", "m5" },
                            "The dry flats hide buried fuel lines. Whoever holds them can run an army without a single refinery.");
                        break;
                    case "m8":
                        SetGraph(mission, new[] { "m10" }, new[] { "m6" },
                            "The ridge lakes split the front into narrow lanes. Naval control decides which lane gets reinforced.");
                        break;
                    case "m9":
                        SetGraph(mission, new[] { "m11" }, new[] { "m7" },
                            "Two blockades, two supply chains. Break one and the other starves.");
                        break;
                    case "m10":
                        SetGraph(mission, new[] { "m12" }, new[] { "m8" },
                            "The delta's channels are older than any faction. Every bridge here has been rebuilt at least twice.");
                        break;
                    case "m11":
                        SetGraph(mission, new[] { "m13" }, new[] { "m9" },
                            "A six-way front means six economies. The Compact's answer is simple: out-produce everyone.");
                        break;
                    case "m12":
                        SetGraph(mission, new[] { "m14" }, new[] { "m10" },
                            "The rust bowl is a graveyard of pre-war industry. Its scrap feeds whoever can hold the centre.");
                        break;
                    case "m13":
                        SetGraph(mission, new[] { "m15" }, new[] { "m11" },
                            "The ring of steel is Meridian's final defensive line. Break the ring and the syndicate loses its sky.");
                        break;
                    case "m14":
                        SetGraph(mission, new[] { "m15" }, new[] { "m12" },
                            "Four lakes, six armies, one delta. The last compact is written in wreckage.");
                        break;
                    case "m15":
                        SetGraph(mission, Array.Empty<string>(), new[] { "m13", "m14" },
                            "The final compact. Whatever survives here sets the terms for the next decade.");
                        break;
                    default:
                        if (string.IsNullOrEmpty(mission.codex))
                        {
                            mission.codex = mission.briefing;
                        }

                        break;
                }
            }
        }

        private static void SetGraph(
            MissionDto mission,
            string[] next,
            string[] requires,
            string codex)
        {
            if (mission.next == null || mission.next.Length == 0)
            {
                mission.next = next;
            }

            if (mission.requires == null || mission.requires.Length == 0)
            {
                mission.requires = requires;
            }

            if (string.IsNullOrEmpty(mission.codex))
            {
                mission.codex = codex;
            }
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
            if (index <= 0 || index >= Missions.Length)
            {
                return index == 0;
            }

            var mission = Missions[index];
            if (mission.requires != null && mission.requires.Length > 0)
            {
                for (var i = 0; i < mission.requires.Length; i++)
                {
                    var required = FindIndex(mission.requires[i]);
                    if (required >= 0 && IsCompleted(required))
                    {
                        return true;
                    }
                }

                return false;
            }

            // Legacy/linear data without an explicit graph.
            return IsCompleted(index - 1);
        }

        public static int FindIndex(string id)
        {
            if (string.IsNullOrEmpty(id) || Missions == null)
            {
                return -1;
            }

            for (var i = 0; i < Missions.Length; i++)
            {
                if (string.Equals(Missions[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>First branch reachable after completing the given mission, or -1.</summary>
        public static int NextMissionIndex(int index)
        {
            if (Missions == null || index < 0 || index >= Missions.Length)
            {
                return -1;
            }

            var next = Missions[index].next;
            if (next != null && next.Length > 0)
            {
                for (var i = 0; i < next.Length; i++)
                {
                    var candidate = FindIndex(next[i]);
                    if (candidate >= 0)
                    {
                        return candidate;
                    }
                }

                return -1;
            }

            return index + 1 < Missions.Length ? index + 1 : -1;
        }

        private static string Key(int index)
        {
            return $"cq_mission_{index}_done";
        }
    }
}
