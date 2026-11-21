using System;
using System.IO;
using CQRa.Sim.Base;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CQRa.Sim.Core
{
    [Serializable]
    public sealed class SavedUnit
    {
        public string kind;
        public int team;
        public float x;
        public float y;
        public float z;
        public float health;
        public int kills;
    }

    [Serializable]
    public sealed class SavedBuilding
    {
        public string kind;
        public int team;
        public float x;
        public float y;
        public float z;
        public float health;
    }

    [Serializable]
    public sealed class MatchSaveData
    {
        public int version = 1;
        public string mapId;
        public string playerFaction;
        public string aiFaction;
        public int teamCount;
        public string teamMode;
        public int difficulty;
        public int team0Credits;
        public int team1Credits;
        public SavedUnit[] units;
        public SavedBuilding[] buildings;
    }

    /// <summary>
    /// Versioned match snapshot capture. Phase B stores serialisable data; applying a
    /// snapshot back into a fresh scene is handled by the load pipeline.
    /// </summary>
    public static class MatchSaveSerializer
    {
        public const int CurrentVersion = 1;

        public static MatchSaveData Capture(EntityManager entityManager)
        {
            var map = MapRuntime.Current;
            var save = new MatchSaveData
            {
                version = CurrentVersion,
                mapId = map?.Id ?? "ash-basin",
                playerFaction = FactionRuntime.Player.ToString(),
                aiFaction = FactionRuntime.Ai.ToString(),
                teamCount = MatchRuntime.TeamCount,
                teamMode = MatchRuntime.Mode.ToString(),
            };

            if (BaseCommands.TryGetEconomy(entityManager, out _, out var economy))
            {
                save.team0Credits = Economy.GetCredits(economy, 0);
                save.team1Credits = Economy.GetCredits(economy, 1);
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<UnitTag>(),
                       ComponentType.ReadOnly<UnitInfo>(),
                       ComponentType.ReadOnly<Health>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            using (var units = query.ToEntityArray(Allocator.Temp))
            {
                save.units = new SavedUnit[units.Length];
                for (var i = 0; i < units.Length; i++)
                {
                    var info = entityManager.GetComponentData<UnitInfo>(units[i]);
                    var health = entityManager.GetComponentData<Health>(units[i]);
                    var position = entityManager.GetComponentData<LocalTransform>(units[i]).Position;
                    var kills = entityManager.HasComponent<Veterancy>(units[i])
                        ? entityManager.GetComponentData<Veterancy>(units[i]).Kills
                        : 0;
                    save.units[i] = new SavedUnit
                    {
                        kind = info.Kind.ToString(),
                        team = info.Team,
                        x = position.x,
                        y = position.y,
                        z = position.z,
                        health = health.Current,
                        kills = kills,
                    };
                }
            }

            using (var query = entityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<BuildingTag>(),
                       ComponentType.ReadOnly<BuildingInfo>(),
                       ComponentType.ReadOnly<Health>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            using (var buildings = query.ToEntityArray(Allocator.Temp))
            {
                save.buildings = new SavedBuilding[buildings.Length];
                for (var i = 0; i < buildings.Length; i++)
                {
                    var info = entityManager.GetComponentData<BuildingInfo>(buildings[i]);
                    var health = entityManager.GetComponentData<Health>(buildings[i]);
                    var position = entityManager.GetComponentData<LocalTransform>(buildings[i]).Position;
                    save.buildings[i] = new SavedBuilding
                    {
                        kind = info.Kind.ToString(),
                        team = info.Team,
                        x = position.x,
                        y = position.y,
                        z = position.z,
                        health = health.Current,
                    };
                }
            }

            return save;
        }

        public static string ToJson(MatchSaveData save)
        {
            return JsonUtility.ToJson(save, true);
        }

        public static MatchSaveData FromJson(string json)
        {
            return JsonUtility.FromJson<MatchSaveData>(json);
        }

        public static void SaveToFile(EntityManager entityManager, string path)
        {
            File.WriteAllText(path, ToJson(Capture(entityManager)));
        }

        public static MatchSaveData LoadFromFile(string path)
        {
            return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
        }
    }
}
