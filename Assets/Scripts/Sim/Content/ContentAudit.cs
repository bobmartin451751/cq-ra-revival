using System.Collections.Generic;
using System.Text;
using CQRa.Sim.Campaign;
using CQRa.Sim.Factions;
using CQRa.Sim.Base;
using Unity.Mathematics;

namespace CQRa.Sim.Content
{
    /// <summary>
    /// Pure content validation shared by EditMode tests and the editor tooling menu.
    /// Returns human-readable problems; an empty list means the content is clean.
    /// </summary>
    public static class ContentAudit
    {
        public static List<string> ValidateAll()
        {
            var issues = new List<string>();
            ValidateMaps(issues);
            ValidateMissions(issues);
            ValidateFactions(issues);
            return issues;
        }

        public static List<string> ValidateMaps()
        {
            var issues = new List<string>();
            ValidateMaps(issues);
            return issues;
        }

        public static List<string> ValidateMissions()
        {
            var issues = new List<string>();
            ValidateMissions(issues);
            return issues;
        }

        public static List<string> ValidateFactions()
        {
            var issues = new List<string>();
            ValidateFactions(issues);
            return issues;
        }

        private static void ValidateMaps(List<string> issues)
        {
            var ids = new HashSet<string>();
            var seeds = new HashSet<int>();

            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                if (map == null)
                {
                    issues.Add($"map {i}: missing definition");
                    continue;
                }

                var where = $"map '{map.Id}'";
                if (string.IsNullOrEmpty(map.Id))
                {
                    issues.Add($"map {i}: empty id");
                }
                else if (!ids.Add(map.Id))
                {
                    issues.Add($"{where}: duplicate id");
                }

                if (!seeds.Add(map.Seed))
                {
                    issues.Add($"{where}: duplicate seed {map.Seed}");
                }

                if (map.WorldSize <= 0f || map.BasinRadius <= 0f ||
                    map.BasinRadius >= map.WorldSize * 0.5f)
                {
                    issues.Add($"{where}: invalid world/basin size");
                }

                var spawnCount = map.Spawns != null && map.Spawns.Length > 0
                    ? map.Spawns.Length
                    : 2;
                if (spawnCount != map.MaxPlayers)
                {
                    issues.Add($"{where}: {spawnCount} spawns for {map.MaxPlayers} players");
                }

                for (var team = 0; team < spawnCount; team++)
                {
                    var spawn = map.SpawnFor(team);
                    if (math.length(spawn.Position) >= map.BasinRadius)
                    {
                        issues.Add($"{where}: spawn {team} outside the basin");
                    }

                    if (ContainsWater(map, spawn.Position))
                    {
                        issues.Add($"{where}: spawn {team} in water");
                    }
                }

                if ((map.Obstacles?.Length ?? 0) < 4)
                {
                    issues.Add($"{where}: fewer than 4 scenery obstacles");
                }

                if ((map.OrePositions?.Length ?? 0) < 8)
                {
                    issues.Add($"{where}: fewer than 8 ore fields");
                }

                if ((map.CratePositions?.Length ?? 0) < 4)
                {
                    issues.Add($"{where}: fewer than 4 crates");
                }

                ValidatePoints(issues, map, where, map.OrePositions, "ore");
                ValidatePoints(issues, map, where, map.CratePositions, "crate");
            }
        }

        private static void ValidatePoints(
            List<string> issues,
            MapDefinition map,
            string where,
            float2[] points,
            string label)
        {
            if (points == null)
            {
                return;
            }

            var limit = map.WorldSize * 0.48f;
            for (var i = 0; i < points.Length; i++)
            {
                if (math.length(points[i]) > limit)
                {
                    issues.Add($"{where}: {label} {i} outside the playable area");
                }

                if (ContainsWater(map, points[i]))
                {
                    issues.Add($"{where}: {label} {i} in water");
                }
            }
        }

        private static void ValidateMissions(List<string> issues)
        {
            MissionCatalog.Load();
            var missions = MissionCatalog.Missions;
            if (missions == null || missions.Length == 0)
            {
                issues.Add("campaign: no missions");
                return;
            }

            var ids = new HashSet<string>();
            for (var i = 0; i < missions.Length; i++)
            {
                var mission = missions[i];
                var where = $"mission '{mission.id}'";
                if (string.IsNullOrEmpty(mission.id) || !ids.Add(mission.id))
                {
                    issues.Add($"mission {i}: empty or duplicate id");
                }

                if (MapCatalog.FindIndex(mission.mapId) < 0)
                {
                    issues.Add($"{where}: unknown map '{mission.mapId}'");
                }

                if (string.IsNullOrEmpty(mission.codex))
                {
                    issues.Add($"{where}: missing codex");
                }

                if (mission.teamCount < 2 || mission.teamCount > 8)
                {
                    issues.Add($"{where}: invalid team count {mission.teamCount}");
                }

                ValidateLinks(issues, where, mission.next, "next", mission.id);
            }
        }

        private static void ValidateLinks(
            List<string> issues,
            string where,
            string[] links,
            string label,
            string self)
        {
            if (links == null)
            {
                return;
            }

            for (var i = 0; i < links.Length; i++)
            {
                if (string.Equals(links[i], self))
                {
                    issues.Add($"{where}: {label} links to itself");
                }
                else if (MissionCatalog.FindIndex(links[i]) < 0)
                {
                    issues.Add($"{where}: {label} target '{links[i]}' does not exist");
                }
            }
        }

        private static void ValidateFactions(List<string> issues)
        {
            if (FactionCatalog.All == null || FactionCatalog.All.Length != 3)
            {
                issues.Add("factions: expected exactly 3 factions");
                return;
            }

            var names = new HashSet<string>();
            for (var i = 0; i < FactionCatalog.All.Length; i++)
            {
                var faction = FactionCatalog.All[i];
                var name = FactionCatalog.Name(faction);
                if (string.IsNullOrEmpty(name) || !names.Add(name))
                {
                    issues.Add($"faction {faction}: empty or duplicate name");
                }

                var unit = ProductionCatalog.UniqueFor(faction);
                if (string.IsNullOrEmpty(ProductionCatalog.UnitName(faction, unit)))
                {
                    issues.Add($"faction {faction}: missing unique unit");
                }
            }
        }

        private static bool ContainsWater(MapDefinition map, float2 point)
        {
            var bodies = map.WaterBodies;
            if (bodies == null)
            {
                return false;
            }

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body.Kind == WaterShapeKind.Circle)
                {
                    if (math.distancesq(point, body.Centre) < body.Radius * body.Radius)
                    {
                        return true;
                    }
                }
                else if (PointInPolygon(body.Points, point))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PointInPolygon(float2[] points, float2 point)
        {
            if (points == null || points.Length < 3)
            {
                return false;
            }

            var inside = false;
            for (var i = 0; i < points.Length; i++)
            {
                var j = (i + points.Length - 1) % points.Length;
                var pi = points[i];
                var pj = points[j];
                if ((pi.y > point.y) != (pj.y > point.y) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static string Format(List<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return "Content audit: no issues.";
            }

            var builder = new StringBuilder();
            builder.Append("Content audit: ").Append(issues.Count).Append(" issue(s)\n");
            for (var i = 0; i < issues.Count; i++)
            {
                builder.Append("- ").Append(issues[i]).Append('\n');
            }

            return builder.ToString();
        }
    }
}
