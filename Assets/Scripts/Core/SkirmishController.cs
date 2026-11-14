using System;
using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Campaign;
using CQRa.Sim.Content;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using CQRa.View;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CQRa.Core
{
    /// <summary>
    /// Phase 9 skirmish shell: a start menu with difficulty selection, a paused world
    /// until the match starts, and a match-end overlay with restart.
    ///
    /// The player always controls team 0. Starting a match creates exactly one AI
    /// commander for team 1 at the chosen difficulty, using the same rules as the player.
    /// Command-line screenshot runs skip the menu so the existing capture rigs still work.
    /// </summary>
    public sealed class SkirmishController : MonoBehaviour
    {
        [SerializeField] private SelectionController _selection;
        [SerializeField] private RtsCameraRig _cameraRig;
        [SerializeField] private UnitSpawner _unitSpawner;

        private AiDifficulty _difficulty = AiDifficulty.Veteran;
        private FactionId _playerFaction = FactionId.Ashfall;
        private FactionId _aiFaction = FactionId.Verdant;
        private int _teamCount = 2;
        private TeamMode _teamMode = TeamMode.FreeForAll;
        private bool _active;
        private bool _matchOver;
        private int _winner = 255;
        private bool _captureRun;

        private int _menuMode; // 0 = skirmish, 1 = campaign
        private int _selectedMission;
        private int _selectedMap;
        private int _mapPage;
        private int _missionPage;
        private const int MapsPerPage = 6;
        private const int MissionsPerPage = 5;
        private bool _campaignActive;
        private bool _missionWon;
        private bool _enemyYardSeen;
        private string _missionObjective = "destroyAll";
        private string _missionObjectiveText = string.Empty;
        private float _surviveSeconds;
        private float _missionElapsed;
        private static int _pendingMission = -1;
        private static bool _pendingSkirmish;
        private static AiDifficulty _pendingDifficulty;
        private static FactionId _pendingPlayerFaction;
        private static FactionId _pendingAiFaction;
        private static int _pendingTeamCount = 2;
        private static TeamMode _pendingTeamMode = TeamMode.FreeForAll;

        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _button;

        public bool IsMatchActive => _active && !_matchOver;

        public void Configure(SelectionController selection, RtsCameraRig cameraRig)
        {
            _selection = selection;
            _cameraRig = cameraRig;
        }

        public void Configure(
            SelectionController selection,
            RtsCameraRig cameraRig,
            UnitSpawner spawner)
        {
            _selection = selection;
            _cameraRig = cameraRig;
            _unitSpawner = spawner;
        }

        private void Awake()
        {
            _captureRun = HasArg("-screenshot");
            MissionCatalog.Load();
            _selectedMap = MapRuntime.SelectedIndex;
            if (_captureRun)
            {
                _teamCount = Mathf.Clamp(ReadIntArg("-teams", 2), 2, 8);
                _teamMode = HasArg("-teammode") ? TeamMode.Teams : TeamMode.FreeForAll;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                // A scene reload keeps the ECS world alive; clear the previous match
                // before UnitSpawner and EconomyAuthoring build a fresh one.
                SkirmishCommands.ResetRuntimeState(world.EntityManager);
            }

            if (_selection != null)
            {
                _selection.enabled = false;
            }

            if (_cameraRig != null)
            {
                _cameraRig.enabled = false;
            }

            if (_captureRun)
            {
                // Capture modes script their own setup; never show the menu in batch runs.
                BeginMatch(false);
            }
            else if (_pendingSkirmish)
            {
                _pendingSkirmish = false;
                _selectedMap = MapRuntime.SelectedIndex;
                _difficulty = _pendingDifficulty;
                _playerFaction = _pendingPlayerFaction;
                _aiFaction = _pendingAiFaction;
                _teamCount = _pendingTeamCount;
                _teamMode = _pendingTeamMode;
                _campaignActive = false;
                BeginMatch(true);
            }
            else if (_pendingMission >= 0)
            {
                var mission = _pendingMission;
                _pendingMission = -1;
                BeginMission(mission);
            }
            else
            {
                Time.timeScale = 0f;
            }
        }

        private void Update()
        {
            if (!_active || _matchOver)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var match = entityManager.GetComponentData<MatchState>(query.GetSingletonEntity());

            if (_campaignActive)
            {
                UpdateCampaignMission(entityManager, match);
                return;
            }

            if (match.GameOver == 0)
            {
                return;
            }

            EndMatch(match.Winner);
        }

        private void UpdateCampaignMission(EntityManager entityManager, in MatchState match)
        {
            _missionElapsed += Time.deltaTime;
            var playerWon = match.GameOver != 0 && MatchRuntime.AreAllied(match.Winner, SkirmishCommands.PlayerTeam);

            if (match.GameOver != 0 && !playerWon)
            {
                EndMission(false);
                return;
            }

            switch (_missionObjective)
            {
                case "destroyAll":
                    if (playerWon)
                    {
                        EndMission(true);
                    }

                    break;

                case "survive":
                    if (_missionElapsed >= _surviveSeconds)
                    {
                        EndMission(true);
                    }

                    break;

                case "destroyYard":
                    var enemyYard = FindEnemyYard(entityManager);
                    if (enemyYard != Entity.Null)
                    {
                        _enemyYardSeen = true;
                    }

                    if (playerWon)
                    {
                        EndMission(true);
                    }
                    else if (_enemyYardSeen && enemyYard == Entity.Null)
                    {
                        EndMission(true);
                    }

                    break;
            }
        }

        private void EndMatch(int winner)
        {
            _matchOver = true;
            _winner = winner;
            Time.timeScale = 0f;

            if (_selection != null)
            {
                _selection.enabled = false;
            }
        }

        private void EndMission(bool won)
        {
            _missionWon = won;
            if (won)
            {
                MissionCatalog.MarkCompleted(_selectedMission);
            }

            EndMatch(won ? SkirmishCommands.PlayerTeam : SkirmishCommands.AiTeam);
        }

        private static Entity FindEnemyYard(EntityManager entityManager)
        {
            for (var team = 0; team < MatchRuntime.TeamCount; team++)
            {
                if (MatchRuntime.AreAllied(team, SkirmishCommands.PlayerTeam))
                {
                    continue;
                }

                var yard = BaseCommands.FindConstructionYard(entityManager, team);
                if (yard != Entity.Null)
                {
                    return yard;
                }
            }

            return Entity.Null;
        }

        private void BeginMission(int index)
        {
            var missions = MissionCatalog.Missions;
            if (missions == null || index < 0 || index >= missions.Length)
            {
                return;
            }

            // Campaign missions load the map assigned to the mission.
            var mission = missions[index];
            var mapIndex = MapCatalog.FindIndex(mission.mapId);
            if (mapIndex < 0)
            {
                mapIndex = 0;
            }

            if (MapRuntime.SelectedIndex != mapIndex)
            {
                MapRuntime.Select(mapIndex);
                _pendingMission = index;
                Restart();
                return;
            }

            _selectedMap = mapIndex;
            _teamCount = Mathf.Clamp(
                mission.teamCount,
                2,
                MapCatalog.Get(mapIndex).MaxPlayers);
            _teamMode = System.Enum.TryParse(mission.teamMode, true, out TeamMode parsedMode)
                ? parsedMode
                : TeamMode.FreeForAll;
            _selectedMission = index;
            _difficulty = MissionCatalog.DifficultyFor(mission);
            _playerFaction = FactionCatalog.Parse(mission.playerFaction);
            _aiFaction = FactionCatalog.Parse(
                mission.aiFaction,
                FactionCatalog.DefaultOpponent(_playerFaction));
            _campaignActive = true;
            _missionWon = false;
            _enemyYardSeen = false;
            _missionObjective = mission.objective ?? "destroyAll";
            _missionObjectiveText = mission.objectiveText ?? string.Empty;
            _surviveSeconds = mission.surviveSeconds;
            _missionElapsed = 0f;
            BeginMatch(true);
        }

        private void BeginMatch(bool withAi)
        {
            _active = true;
            _matchOver = false;
            Time.timeScale = 1f;

            MatchRuntime.Configure(_teamCount, _teamMode);

            if (_selection != null)
            {
                _selection.enabled = true;
            }

            if (_cameraRig != null)
            {
                _cameraRig.enabled = true;
                var start = MapRuntime.Current.SpawnFor(0).Position;
                _cameraRig.SetPose(new Vector3(start.x, 0f, start.y), 0f, 46f, 115f);
            }

            var factions = new FactionId[MatchRuntime.MaxTeams];
            factions[0] = _playerFaction;
            factions[1] = _aiFaction;
            for (var team = 2; team < factions.Length; team++)
            {
                factions[team] = FactionCatalog.All[(team - 1) % FactionCatalog.All.Length];
            }

            FactionRuntime.SetAll(factions);

            var baseRenderer = UnityEngine.Object.FindFirstObjectByType<BaseRenderer>();
            if (baseRenderer != null)
            {
                baseRenderer.ApplyFactionColors();
            }

            if (_unitSpawner != null && _unitSpawner.SpawnedCount == 0)
            {
                _unitSpawner.Spawn();
            }

            if (!withAi)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                SkirmishCommands.StartMatch(
                    world.EntityManager,
                    _teamCount,
                    _teamMode,
                    _difficulty);
            }

            Debug.Log($"[Skirmish] match started | {_teamCount} teams | {_teamMode} | {_difficulty} AI");
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!_active)
            {
                DrawMenu();
                return;
            }

            GUI.Label(
                new Rect(Screen.width - 230f, 12f, 218f, 24f),
                $"AI: {_difficulty}",
                _label);

            if (_campaignActive)
            {
                DrawObjectiveHud();
            }

            if (_matchOver)
            {
                if (_campaignActive)
                {
                    DrawCampaignResult();
                }
                else
                {
                    DrawResult();
                }
            }
        }

        private void DrawObjectiveHud()
        {
            var missions = MissionCatalog.Missions;
            if (missions == null || missions.Length == 0)
            {
                return;
            }

            var index = Mathf.Clamp(_selectedMission, 0, missions.Length - 1);

            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.035f, 0.03f, 0.78f);
            GUI.DrawTexture(new Rect(12f, 12f, 430f, 62f), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(
                new Rect(22f, 16f, 410f, 22f),
                missions[index].name,
                _subtitle);

            var objective = _missionObjective == "survive"
                ? $"{_missionObjectiveText}  ({Mathf.CeilToInt(Mathf.Max(0f, _surviveSeconds - _missionElapsed))}s)"
                : _missionObjectiveText;

            GUI.Label(new Rect(22f, 44f, 410f, 22f), objective, _label);
        }

        private void DrawMenu()
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.035f, 0.03f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var width = 540f;
            var height = 740f;
            var x = (Screen.width - width) * 0.5f;
            var y = (Screen.height - height) * 0.5f;
            var panel = new Rect(x, y, width, height);

            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x + 30f, y + 18f, width - 60f, 54f), "C&Q R-A REVIVAL", _title);

            var half = (width - 60f) * 0.5f;
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = _menuMode == 0
                ? new Color(0.72f, 0.48f, 0.20f)
                : new Color(0.28f, 0.26f, 0.22f);
            if (GUI.Button(new Rect(x + 30f, y + 78f, half, 34f), "SKIRMISH", _button))
            {
                _menuMode = 0;
            }

            GUI.backgroundColor = _menuMode == 1
                ? new Color(0.72f, 0.48f, 0.20f)
                : new Color(0.28f, 0.26f, 0.22f);
            if (GUI.Button(new Rect(x + 34f + half, y + 78f, half, 34f), "CAMPAIGN", _button))
            {
                _menuMode = 1;
            }

            GUI.backgroundColor = previousColor;

            GUI.Label(
                new Rect(x + 30f, y + 120f, width - 60f, 24f),
                "The AI plays by exactly the same rules: no bonuses, no stat changes.",
                _label);

            if (_menuMode == 0)
            {
                DrawSkirmishMenu(x, y, width);
            }
            else
            {
                DrawCampaignMenu(x, y, width);
            }
        }

        private void DrawSkirmishMenu(float x, float y, float width)
        {
            EnsureSelectedMap();
            GUI.Label(new Rect(x + 30f, y + 150f, width - 60f, 24f), "DIFFICULTY", _label);

            var difficulties = new[]
            {
                AiDifficulty.Recruit,
                AiDifficulty.Veteran,
                AiDifficulty.Hardened,
                AiDifficulty.Brutal,
            };

            for (var i = 0; i < difficulties.Length; i++)
            {
                var difficulty = difficulties[i];
                var selected = difficulty == _difficulty;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = selected
                    ? new Color(0.72f, 0.48f, 0.20f)
                    : new Color(0.28f, 0.26f, 0.22f);

                if (GUI.Button(
                        new Rect(x + 30f + i * 121f, y + 174f, 111f, 38f),
                        difficulty.ToString(),
                        _button))
                {
                    _difficulty = difficulty;
                }

                GUI.backgroundColor = previousColor;
            }

            GUI.Label(
                new Rect(x + 30f, y + 216f, width - 60f, 30f),
                Describe(_difficulty),
                _label);

            GUI.Label(new Rect(x + 30f, y + 250f, width - 60f, 22f), "PLAYERS", _label);

            for (var i = 0; i < 7; i++)
            {
                var count = i + 2;
                var selected = count == _teamCount;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = selected
                    ? new Color(0.72f, 0.48f, 0.20f)
                    : new Color(0.28f, 0.26f, 0.22f);

                if (GUI.Button(
                        new Rect(x + 30f + i * 68f, y + 274f, 64f, 30f),
                        count.ToString(),
                        _button))
                {
                    _teamCount = count;
                    EnsureSelectedMap();
                }

                GUI.backgroundColor = previousColor;
            }

            GUI.Label(new Rect(x + 30f, y + 312f, width - 60f, 22f), "MODE", _label);

            var modes = new[] { TeamMode.FreeForAll, TeamMode.Teams };
            for (var i = 0; i < modes.Length; i++)
            {
                var selected = modes[i] == _teamMode;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = selected
                    ? new Color(0.72f, 0.48f, 0.20f)
                    : new Color(0.28f, 0.26f, 0.22f);

                if (GUI.Button(
                        new Rect(x + 30f + i * 244f, y + 336f, 234f, 30f),
                        modes[i] == TeamMode.FreeForAll ? "FREE-FOR-ALL" : "TEAMS",
                        _button))
                {
                    _teamMode = modes[i];
                }

                GUI.backgroundColor = previousColor;
            }

            GUI.Label(new Rect(x + 30f, y + 374f, width - 60f, 22f), "FACTION", _label);

            for (var i = 0; i < FactionCatalog.All.Length; i++)
            {
                var faction = FactionCatalog.All[i];
                var selected = faction == _playerFaction;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = selected
                    ? new Color(0.72f, 0.48f, 0.20f)
                    : new Color(0.28f, 0.26f, 0.22f);

                if (GUI.Button(
                        new Rect(x + 30f + i * 162f, y + 398f, 152f, 30f),
                        FactionCatalog.Name(faction),
                        _button))
                {
                    _playerFaction = faction;
                    _aiFaction = FactionCatalog.DefaultOpponent(faction);
                }

                GUI.backgroundColor = previousColor;
            }

            GUI.Label(
                new Rect(x + 30f, y + 432f, width - 60f, 34f),
                $"{FactionCatalog.Name(_playerFaction)} vs {FactionCatalog.Name(_aiFaction)}\n" +
                FactionCatalog.Description(_playerFaction),
                _label);

            GUI.Label(new Rect(x + 30f, y + 472f, width - 60f, 22f), "MAP", _label);

            var filteredCount = FilteredMapCount();
            var pages = Mathf.Max(1, Mathf.CeilToInt(filteredCount / (float)MapsPerPage));
            _mapPage = Mathf.Clamp(_mapPage, 0, pages - 1);

            if (GUI.Button(new Rect(x + width - 118f, y + 470f, 40f, 26f), "<", _button))
            {
                _mapPage = _mapPage > 0 ? _mapPage - 1 : pages - 1;
            }

            if (GUI.Button(new Rect(x + width - 74f, y + 470f, 40f, 26f), ">", _button))
            {
                _mapPage = _mapPage + 1 < pages ? _mapPage + 1 : 0;
            }

            var mapWidth = (width - 60f - 16f) / 3f;
            var first = _mapPage * MapsPerPage;
            for (var slot = 0; slot < MapsPerPage; slot++)
            {
                var filtered = first + slot;
                if (filtered >= filteredCount)
                {
                    break;
                }

                var mapIndex = FilteredMapAt(filtered);
                var map = MapCatalog.Get(mapIndex);
                var selected = mapIndex == _selectedMap;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = selected
                    ? new Color(0.72f, 0.48f, 0.20f)
                    : new Color(0.28f, 0.26f, 0.22f);

                if (GUI.Button(
                        new Rect(
                            x + 30f + (slot % 3) * (mapWidth + 8f),
                            y + 498f + (slot / 3) * 34f,
                            mapWidth,
                            30f),
                        map.Name,
                        _button))
                {
                    _selectedMap = mapIndex;
                }

                GUI.backgroundColor = previousColor;
            }

            if (filteredCount == 0)
            {
                GUI.Label(
                    new Rect(x + 30f, y + 500f, width - 60f, 26f),
                    "No maps support this player count yet.",
                    _label);
            }

            var preview = MapPreviewCache.Get(_selectedMap);
            if (preview != null)
            {
                GUI.DrawTexture(
                    new Rect(x + 30f, y + 566f, 80f, 80f),
                    preview,
                    ScaleMode.ScaleToFit);
            }

            GUI.Label(
                new Rect(x + 120f, y + 566f, width - 150f, 64f),
                $"{_mapPage + 1}/{pages}  [{MapCatalog.Get(_selectedMap).SizeTier} " +
                $"{MapCatalog.Get(_selectedMap).MinPlayers}-{MapCatalog.Get(_selectedMap).MaxPlayers}p]\n" +
                MapCatalog.Get(_selectedMap).Description,
                _label);

            if (GUI.Button(
                    new Rect(x + 30f, y + 650f, width - 60f, 44f),
                    "START MATCH",
                    _button))
            {
                var needsReload = MapRuntime.SelectedIndex != _selectedMap;
                MapRuntime.Select(_selectedMap);
                _campaignActive = false;

                if (needsReload)
                {
                    _pendingSkirmish = true;
                    _pendingDifficulty = _difficulty;
                    _pendingPlayerFaction = _playerFaction;
                    _pendingAiFaction = _aiFaction;
                    _pendingTeamCount = _teamCount;
                    _pendingTeamMode = _teamMode;
                    Restart();
                }
                else
                {
                    BeginMatch(true);
                }
            }

            GUI.Label(
                new Rect(x + 30f, y + 704f, width - 60f, 24f),
                "WASD/edges pan Â· wheel zoom Â· Q/E rotate",
                _label);
        }

        private int FilteredMapCount()
        {
            var count = 0;
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                if (map.MinPlayers <= _teamCount && map.MaxPlayers >= _teamCount)
                {
                    count++;
                }
            }

            return count;
        }

        private int FilteredMapAt(int filteredIndex)
        {
            var seen = 0;
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                if (map.MinPlayers > _teamCount || map.MaxPlayers < _teamCount)
                {
                    continue;
                }

                if (seen == filteredIndex)
                {
                    return i;
                }

                seen++;
            }

            return 0;
        }

        private void EnsureSelectedMap()
        {
            var selected = MapCatalog.Get(_selectedMap);
            if (selected.MinPlayers <= _teamCount && selected.MaxPlayers >= _teamCount)
            {
                return;
            }

            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var map = MapCatalog.Get(i);
                if (map.MinPlayers <= _teamCount && map.MaxPlayers >= _teamCount)
                {
                    _selectedMap = i;
                    _mapPage = i / MapsPerPage;
                    return;
                }
            }

            _selectedMap = 0;
            _mapPage = 0;
        }

        private void DrawCampaignMenu(float x, float y, float width)
        {
            var missions = MissionCatalog.Missions;
            if (missions == null || missions.Length == 0)
            {
                GUI.Label(new Rect(x + 30f, y + 160f, width - 60f, 24f), "No missions found.", _label);
                return;
            }

            GUI.Label(new Rect(x + 30f, y + 150f, width - 60f, 22f), "MISSIONS", _label);

            var missionPages = Mathf.Max(1, Mathf.CeilToInt(missions.Length / (float)MissionsPerPage));
            _missionPage = Mathf.Clamp(_missionPage, 0, missionPages - 1);

            if (GUI.Button(new Rect(x + width - 118f, y + 148f, 40f, 26f), "<", _button))
            {
                _missionPage = _missionPage > 0 ? _missionPage - 1 : missionPages - 1;
            }

            if (GUI.Button(new Rect(x + width - 74f, y + 148f, 40f, 26f), ">", _button))
            {
                _missionPage = _missionPage + 1 < missionPages ? _missionPage + 1 : 0;
            }

            var missionFirst = _missionPage * MissionsPerPage;
            var missionLast = Mathf.Min(missions.Length, missionFirst + MissionsPerPage);
            for (var i = missionFirst; i < missionLast; i++)
            {
                var unlocked = MissionCatalog.IsUnlocked(i);
                var completed = MissionCatalog.IsCompleted(i);
                var marker = completed ? " *" : unlocked ? string.Empty : "  [LOCKED]";
                var label = $"{i + 1}. {missions[i].name}{marker}";

                var previousColor = GUI.backgroundColor;
                if (i == _selectedMission)
                {
                    GUI.backgroundColor = new Color(0.72f, 0.48f, 0.20f);
                }
                else if (!unlocked)
                {
                    GUI.backgroundColor = new Color(0.18f, 0.17f, 0.16f);
                }
                else
                {
                    GUI.backgroundColor = new Color(0.28f, 0.26f, 0.22f);
                }

                var row = i - missionFirst;
                if (GUI.Button(
                        new Rect(x + 30f, y + 176f + row * 36f, width - 60f, 32f),
                        label,
                        _button) && unlocked)
                {
                    _selectedMission = i;
                }

                GUI.backgroundColor = previousColor;
            }

            var mission = missions[Mathf.Clamp(_selectedMission, 0, missions.Length - 1)];
            var playerFaction = FactionCatalog.Parse(mission.playerFaction);
            var aiFaction = FactionCatalog.Parse(
                mission.aiFaction,
                FactionCatalog.DefaultOpponent(playerFaction));
            var missionMap = MapCatalog.Get(MapCatalog.FindIndex(mission.mapId));
            var missionPreview = MapPreviewCache.Get(MapCatalog.FindIndex(mission.mapId));
            if (missionPreview != null)
            {
                GUI.DrawTexture(
                    new Rect(x + 30f, y + 366f, 64f, 64f),
                    missionPreview,
                    ScaleMode.ScaleToFit);
            }

            GUI.Label(
                new Rect(x + 104f, y + 366f, width - 134f, 82f),
                $"{FactionCatalog.Name(playerFaction)} vs {FactionCatalog.Name(aiFaction)}\n" +
                $"Map: {missionMap?.Name ?? "Ash Basin"}  |  {mission.teamCount} players\n" +
                mission.briefing,
                _label);

            if (GUI.Button(
                    new Rect(x + 30f, y + 456f, width - 60f, 40f),
                    "START MISSION",
                    _button) &&
                MissionCatalog.IsUnlocked(_selectedMission))
            {
                BeginMission(_selectedMission);
            }
        }

        private void DrawCampaignResult()
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.02f, 0.78f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.3f, Screen.width, 280f), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(
                new Rect(0f, Screen.height * 0.3f + 18f, Screen.width, 60f),
                _missionWon ? "MISSION COMPLETE" : "MISSION FAILED",
                _title);

            var buttonY = Screen.height * 0.3f + 110f;
            var hasNext = _missionWon && _selectedMission + 1 < MissionCatalog.Missions.Length;

            if (GUI.Button(
                    new Rect(Screen.width * 0.5f - 330f, buttonY, 200f, 44f),
                    "RETRY",
                    _button))
            {
                _pendingMission = _selectedMission;
                Restart();
            }

            if (hasNext &&
                GUI.Button(
                    new Rect(Screen.width * 0.5f - 110f, buttonY, 220f, 44f),
                    "NEXT MISSION",
                    _button))
            {
                _pendingMission = _selectedMission + 1;
                Restart();
            }

            if (GUI.Button(
                    new Rect(Screen.width * 0.5f + 150f, buttonY, 200f, 44f),
                    "MAIN MENU",
                    _button))
            {
                _pendingMission = -1;
                Restart();
            }
        }

        private void DrawResult()
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.02f, 0.72f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.34f, Screen.width, 220f), Texture2D.whiteTexture);
            GUI.color = previous;

            string text = SkirmishCommands.ResultText(_winner);

            GUI.Label(
                new Rect(0f, Screen.height * 0.34f + 18f, Screen.width, 60f),
                text,
                _title);

            if (GUI.Button(
                    new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.34f + 100f, 220f, 46f),
                    "RESTART",
                    _button))
            {
                Restart();
            }
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static string Describe(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Recruit:
                    return "Slow decisions, small armies, infantry-heavy.";
                case AiDifficulty.Hardened:
                    return "Faster decisions, mixed armies, expanded production.";
                case AiDifficulty.Brutal:
                    return "Fastest decisions, early armour-heavy attacks.";
                default:
                    return "Balanced decision speed and army composition.";
            }
        }

        private void EnsureStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.92f, 0.89f, 0.80f) },
            };

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.86f, 0.55f) },
            };

            _subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                normal = { textColor = new Color(0.95f, 0.92f, 0.82f) },
            };

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                normal = { textColor = new Color(0.95f, 0.92f, 0.82f) },
                hover = { textColor = new Color(1f, 0.94f, 0.72f) },
            };
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

        private static int ReadIntArg(string flag, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return int.TryParse(args[i + 1], out var value) ? value : fallback;
            }

            return fallback;
        }
    }
}
