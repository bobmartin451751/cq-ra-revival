using System;
using System.IO;
using CQRa.Sim.AI;
using CQRa.Sim.Base;
using CQRa.Sim.Campaign;
using CQRa.Sim.Content;
using CQRa.Sim.Core;
using CQRa.Sim.Factions;
using CQRa.Sim.Units;
using CQRa.View;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private bool _observerMode;
        private int _observerSpeed;
        private bool _observerPaused;
        private static readonly float[] ObserverSpeeds = { 1f, 2f, 4f };
        private readonly int[] _customAlliances = new int[MatchRuntime.MaxTeams];
        private bool _active;
        private bool _matchOver;
        private int _winner = 255;
        private bool _captureRun;

        private int _menuMode; // 0 = skirmish, 1 = campaign, 2 = map editor
        private int _missionBriefing = -1;
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
        private static bool _pendingObserver;

        private int _editorSeed = 4242;
        private MapSizeTier _editorTier = MapSizeTier.Medium;
        private bool _editorWater = true;
        private int _editorTeams = 4;
        private MapDefinition _editorMap;

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
            LoadEditorState();
            _selectedMap = MapRuntime.SelectedIndex;
            if (_captureRun)
            {
                _teamCount = Mathf.Clamp(ReadIntArg("-teams", 2), 2, 8);
                _teamMode = HasArg("-teammode") ? TeamMode.Teams : TeamMode.FreeForAll;
            }

            ResetCustomTeams();

            var pendingReplay = ReplayRuntime.TakePendingPlayback();

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
            else if (pendingReplay != null)
            {
                _selectedMap = Mathf.Max(0, MapCatalog.FindIndex(pendingReplay.mapId));
                _playerFaction = FactionCatalog.Parse(pendingReplay.playerFaction);
                _aiFaction = FactionCatalog.Parse(pendingReplay.aiFaction);
                _teamCount = Mathf.Clamp(pendingReplay.teamCount, 2, 8);
                _teamMode = Enum.TryParse(pendingReplay.teamMode, true, out TeamMode replayMode)
                    ? replayMode
                    : TeamMode.FreeForAll;
                _difficulty = (AiDifficulty)pendingReplay.difficulty;
                _observerMode = pendingReplay.observer != 0;
                _campaignActive = false;
                BeginMatch(true);
                if (_selection != null)
                {
                    _selection.enabled = false;
                }

                ReplayRuntime.BeginPlayback(pendingReplay);
                Announcer.Push("Replay", 3f);
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
                _observerMode = _pendingObserver;
                _campaignActive = false;
                BeginMatch(true);
            }
            else if (MatchSaveRuntime.HasPending)
            {
                var save = MatchSaveRuntime.Take();
                _selectedMap = Mathf.Max(0, MapCatalog.FindIndex(save.mapId));
                _playerFaction = FactionCatalog.Parse(save.playerFaction);
                _aiFaction = FactionCatalog.Parse(save.aiFaction);
                _teamCount = Mathf.Clamp(save.teamCount, 2, 8);
                _teamMode = Enum.TryParse(save.teamMode, true, out TeamMode mode)
                    ? mode
                    : TeamMode.FreeForAll;
                _campaignActive = false;
                BeginMatch(true);
                MatchSaveSerializer.Apply(World.DefaultGameObjectInjectionWorld.EntityManager, save);
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
                AudioDirector.SetCombat(false);
            }
        }

        private void Update()
        {
            if (!_active || _matchOver)
            {
                return;
            }

            if (ReplayRuntime.ConsumeFinished())
            {
                Announcer.Push("Replay finished", 3f);
            }

            if (_observerMode)
            {
                HandleObserverInput();
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && !ReplayRuntime.IsPlaying)
            {
                if (keyboard.f5Key.wasPressedThisFrame)
                {
                    SaveMatchToDisk();
                }

                if (keyboard.f9Key.wasPressedThisFrame)
                {
                    LoadMatchFromDisk();
                }

                if (keyboard.f7Key.wasPressedThisFrame)
                {
                    SaveSlots.Select((SaveSlots.CurrentSlot + 1) % SaveSlots.Count);
                    Announcer.Push($"Save slot {SaveSlots.CurrentSlot + 1}", 1.5f);
                }
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

        private void SaveMatchToDisk()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var slot = SaveSlots.CurrentSlot;
            SaveSlots.Save(world.EntityManager, slot);
            Announcer.Push($"Match saved to slot {slot + 1}", 2f);
        }

        private void LoadMatchFromDisk()
        {
            var slot = SaveSlots.CurrentSlot;
            if (!SaveSlots.Exists(slot))
            {
                Announcer.Push($"Slot {slot + 1} is empty", 2f);
                return;
            }

            var save = SaveSlots.Load(slot);
            if (save == null)
            {
                Announcer.Push("Save file is invalid or from a newer version", 2f);
                return;
            }

            var mapIndex = MapCatalog.FindIndex(save.mapId);
            MapRuntime.Select(mapIndex < 0 ? 0 : mapIndex);
            MatchSaveRuntime.Queue(save);
            Restart();
        }

        private void WatchReplay()
        {
            var data = ReplayStorage.LoadFromFile(ReplayRuntime.FilePath);
            if (data == null)
            {
                Announcer.Push("No replay available", 2f);
                return;
            }

            var mapIndex = MapCatalog.FindIndex(data.mapId);
            MapRuntime.Select(mapIndex < 0 ? 0 : mapIndex);
            ReplayRuntime.QueuePlayback(data);
            Restart();
        }

        private void HandleObserverInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                _observerSpeed = Mathf.Max(0, _observerSpeed - 1);
                _observerPaused = false;
                Time.timeScale = ObserverSpeeds[_observerSpeed];
            }

            if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                _observerSpeed = Mathf.Min(ObserverSpeeds.Length - 1, _observerSpeed + 1);
                _observerPaused = false;
                Time.timeScale = ObserverSpeeds[_observerSpeed];
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                _observerPaused = !_observerPaused;
                Time.timeScale = _observerPaused ? 0f : ObserverSpeeds[_observerSpeed];
            }
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

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                MatchHistory.Capture(world.EntityManager, SimClock.Tick);
                ReplayRuntime.SaveLastReplay(world.EntityManager);
            }

            AudioDirector.SetCombat(false);
            Announcer.Push(
                winner == 255
                    ? "Battle ended in a draw"
                    : MatchRuntime.AreAllied(winner, SkirmishCommands.PlayerTeam)
                        ? "Victory"
                        : "Defeat",
                5f);
            if (winner == 255)
            {
                AudioDirector.Play(AudioCue.Defeat);
            }
            else
            {
                AudioDirector.Play(
                    MatchRuntime.AreAllied(winner, SkirmishCommands.PlayerTeam)
                        ? AudioCue.Victory
                        : AudioCue.Defeat);
            }

            Time.timeScale = 0f;

            if (_selection != null)
            {
                _selection.enabled = false;
            }
        }

        private void EndMission(bool won)
        {
            _missionWon = won;
            PersistHeroProgress();
            Announcer.Push(won ? "Mission complete" : "Mission failed", 5f);
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
            _observerSpeed = 0;
            _observerPaused = false;
            AudioDirector.SetCombat(true);
            Announcer.Push("Battle control online");

            MatchRuntime.Configure(_teamCount, _teamMode, _customAlliances);

            if (_selection != null)
            {
                _selection.enabled = !_observerMode;
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

            if (_campaignActive)
            {
                SpawnCampaignHero();
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
                    _difficulty,
                    _observerMode);

                ReplayRuntime.BeginRecording(
                    MapRuntime.Current?.Id ?? "ash-basin",
                    _playerFaction.ToString(),
                    _aiFaction.ToString(),
                    _teamCount,
                    _teamMode.ToString(),
                    (int)_difficulty,
                    _observerMode);
            }

            Debug.Log($"[Skirmish] match started | {_teamCount} teams | {_teamMode} | {_difficulty} AI");
        }

        private void SpawnCampaignHero()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var heroKind = ProductionCatalog.UniqueFor(_playerFaction);
            var start = MapRuntime.Current.SpawnFor(SkirmishCommands.PlayerTeam).Position;
            var spawn = new float3(start.x + 6f, 0f, start.y);

            var hero = UnitFactory.SpawnDefault(
                entityManager, spawn, 0f, heroKind, SkirmishCommands.PlayerTeam);
            var health = entityManager.GetComponentData<Health>(hero);
            health.Max *= 1.35f;
            health.Current = health.Max;
            entityManager.SetComponentData(hero, health);
            var weapon = entityManager.GetComponentData<Weapon>(hero);
            weapon.Damage *= 1.25f;
            entityManager.SetComponentData(hero, weapon);
            entityManager.AddComponentData(hero, new Hero { Flag = 1 });
            entityManager.AddComponentData(hero, new HeroAbility
            {
                Cooldown = HeroAbilityCatalog.Cooldown,
                CooldownRemaining = 0f,
            });

            if (entityManager.HasComponent<Veterancy>(hero))
            {
                var veterancy = entityManager.GetComponentData<Veterancy>(hero);
                veterancy.Kills = HeroProgress.Kills(_playerFaction);
                veterancy.Rank = 0;
                entityManager.SetComponentData(hero, veterancy);
            }
        }

        private void PersistHeroProgress()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Hero>(),
                ComponentType.ReadOnly<UnitInfo>(),
                ComponentType.ReadOnly<Veterancy>());
            using var heroes = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < heroes.Length; i++)
            {
                var info = entityManager.GetComponentData<UnitInfo>(heroes[i]);
                if (info.Team == SkirmishCommands.PlayerTeam)
                {
                    HeroProgress.Save(
                        _playerFaction,
                        entityManager.GetComponentData<Veterancy>(heroes[i]).Kills);
                    return;
                }
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!_active)
            {
                DrawMenu();
                if (_missionBriefing >= 0)
                {
                    DrawMissionBriefing();
                }

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

            if (Announcer.TryGetCurrent(out var announcement))
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 260f, 12f, 520f, 26f),
                    announcement,
                    _subtitle);
            }

            if (_observerMode)
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 300f, Screen.height - 38f, 600f, 24f),
                    _observerPaused
                        ? "SPECTATOR PAUSED  |  P resume  |  [ ] speed"
                        : $"SPECTATOR  x{ObserverSpeeds[_observerSpeed]}  |  [ ] speed  |  P pause",
                    _label);
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
            Announcer.Clear();
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.035f, 0.03f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var width = 540f;
            var height = 790f;
            var x = (Screen.width - width) * 0.5f;
            var y = (Screen.height - height) * 0.5f;
            var panel = new Rect(x, y, width, height);

            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x + 30f, y + 18f, width - 60f, 54f), "C&Q R-A REVIVAL", _title);

            var tabGap = 4f;
            var tabWidth = (width - 60f - tabGap * 2f) / 3f;
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = _menuMode == 0
                ? new Color(0.72f, 0.48f, 0.20f)
                : new Color(0.28f, 0.26f, 0.22f);
            if (GUI.Button(new Rect(x + 30f, y + 78f, tabWidth, 34f), "SKIRMISH", _button))
            {
                _menuMode = 0;
            }

            GUI.backgroundColor = _menuMode == 1
                ? new Color(0.72f, 0.48f, 0.20f)
                : new Color(0.28f, 0.26f, 0.22f);
            if (GUI.Button(
                    new Rect(x + 30f + tabWidth + tabGap, y + 78f, tabWidth, 34f),
                    "CAMPAIGN",
                    _button))
            {
                _menuMode = 1;
            }

            GUI.backgroundColor = _menuMode == 2
                ? new Color(0.72f, 0.48f, 0.20f)
                : new Color(0.28f, 0.26f, 0.22f);
            if (GUI.Button(
                    new Rect(x + 30f + (tabWidth + tabGap) * 2f, y + 78f, tabWidth, 34f),
                    "MAP EDITOR",
                    _button))
            {
                _menuMode = 2;
            }

            GUI.backgroundColor = previousColor;

            GUI.Label(
                new Rect(x + 30f, y + 120f, width - 360f, 24f),
                "The AI plays by exactly the same rules: no bonuses, no stat changes.",
                _label);

            if (GUI.Button(
                    new Rect(x + width - 330f, y + 118f, 100f, 26f),
                    $"SLOT {SaveSlots.CurrentSlot + 1}",
                    _button))
            {
                SaveSlots.Select((SaveSlots.CurrentSlot + 1) % SaveSlots.Count);
            }

            if (GUI.Button(
                    new Rect(x + width - 224f, y + 118f, 82f, 26f),
                    "LOAD",
                    _button))
            {
                LoadMatchFromDisk();
                return;
            }

            if (ReplayRuntime.HasReplay &&
                GUI.Button(new Rect(x + width - 136f, y + 118f, 106f, 26f), "REPLAY", _button))
            {
                WatchReplay();
                return;
            }

            GUI.Label(
                new Rect(x + 30f, y + 144f, width - 60f, 20f),
                SaveSlots.Describe(SaveSlots.CurrentSlot),
                _label);

            var contentY = y + 14f;
            if (_menuMode == 0)
            {
                DrawSkirmishMenu(x, contentY, width);
            }
            else if (_menuMode == 1)
            {
                DrawCampaignMenu(x, contentY, width);
            }
            else
            {
                DrawEditorMenu(x, contentY, width);
            }
        }

        private void LoadEditorState()
        {
            var data = CustomMapStore.Load(CustomMapStore.FilePath);
            if (data != null)
            {
                _editorSeed = data.seed;
                _editorTier = (MapSizeTier)Mathf.Clamp(data.tier, 0, 3);
                _editorWater = data.water != 0;
                _editorTeams = Mathf.Clamp(
                    data.teams, 2, CustomMapFactory.MaxTeamsFor(_editorTier));
            }

            RebuildEditorMap();
        }

        private void RebuildEditorMap()
        {
            _editorTier = (MapSizeTier)Mathf.Clamp((int)_editorTier, 0, 3);
            _editorTeams = Mathf.Clamp(_editorTeams, 2, CustomMapFactory.MaxTeamsFor(_editorTier));
            _editorMap = CustomMapFactory.Build(
                _editorSeed, _editorTier, _editorWater, _editorTeams);
        }

        private void DrawEditorMenu(float x, float y, float width)
        {
            if (_editorMap == null)
            {
                RebuildEditorMap();
            }

            GUI.Label(new Rect(x + 30f, y + 150f, width - 60f, 24f), "MAP EDITOR", _label);

            var preview = MapPreviewCache.GetCustom(_editorMap);
            if (preview != null)
            {
                GUI.DrawTexture(
                    new Rect(x + 30f, y + 180f, 180f, 180f),
                    preview,
                    ScaleMode.ScaleToFit);
            }

            GUI.Label(
                new Rect(x + 230f, y + 180f, width - 260f, 110f),
                $"Seed {_editorSeed}\n" +
                $"{_editorMap.SizeTier}  {_editorMap.WorldSize:0}m  {_editorTeams} players\n" +
                $"{(_editorWater ? "Water: lake" : "Water: none")}\n" +
                (MapRuntime.HasCustom ? "Custom map is live" : "Not deployed yet"),
                _label);

            var controlsX = x + 230f;
            var controlsY = y + 300f;
            if (GUI.Button(new Rect(controlsX, controlsY, 44f, 32f), "-", _button))
            {
                _editorSeed = Mathf.Max(1, _editorSeed - 1);
                RebuildEditorMap();
            }

            GUI.Label(
                new Rect(controlsX + 50f, controlsY, 160f, 32f),
                $"Seed {_editorSeed}",
                _label);

            if (GUI.Button(new Rect(controlsX + 216f, controlsY, 44f, 32f), "+", _button))
            {
                _editorSeed++;
                RebuildEditorMap();
            }

            controlsY += 40f;
            if (GUI.Button(new Rect(controlsX, controlsY, 260f, 32f),
                    $"SIZE: {_editorTier}", _button))
            {
                _editorTier = (MapSizeTier)(((int)_editorTier + 1) % 4);
                RebuildEditorMap();
            }

            controlsY += 40f;
            if (GUI.Button(new Rect(controlsX, controlsY, 260f, 32f),
                    _editorWater ? "WATER: ON" : "WATER: OFF", _button))
            {
                _editorWater = !_editorWater;
                RebuildEditorMap();
            }

            controlsY += 40f;
            if (GUI.Button(new Rect(controlsX, controlsY, 126f, 32f), "- PLAYERS", _button))
            {
                _editorTeams = Mathf.Max(2, _editorTeams - 1);
                RebuildEditorMap();
            }

            GUI.Label(
                new Rect(controlsX + 132f, controlsY, 62f, 32f),
                _editorTeams.ToString(),
                _label);

            if (GUI.Button(new Rect(controlsX + 194f, controlsY, 128f, 32f), "+ PLAYERS", _button))
            {
                _editorTeams = Mathf.Min(
                    CustomMapFactory.MaxTeamsFor(_editorTier), _editorTeams + 1);
                RebuildEditorMap();
            }

            controlsY += 48f;
            if (GUI.Button(new Rect(controlsX, controlsY, 126f, 32f), "RANDOMIZE", _button))
            {
                _editorSeed = UnityEngine.Random.Range(1, 1000000);
                RebuildEditorMap();
            }

            if (GUI.Button(new Rect(controlsX + 134f, controlsY, 126f, 32f), "SAVE", _button))
            {
                SaveEditorMap();
                Announcer.Push("Custom map saved", 2f);
            }

            controlsY += 48f;
            if (GUI.Button(new Rect(x + 30f, y + 520f, width - 60f, 46f),
                    "PLAY CUSTOM MAP", _button))
            {
                StartCustomMap();
            }

            GUI.Label(
                new Rect(x + 30f, y + 576f, width - 60f, 60f),
                "Maps regenerate deterministically from seed, size, water and player count.\n" +
                "The same four values are saved to disk.",
                _label);
        }

        private void SaveEditorMap()
        {
            RebuildEditorMap();
            CustomMapStore.Save(
                CustomMapStore.Capture(
                    _editorSeed, _editorTier, _editorWater, _editorTeams),
                CustomMapStore.FilePath);
        }

        private void StartCustomMap()
        {
            RebuildEditorMap();
            SaveEditorMap();
            MapRuntime.SelectCustom(_editorMap);
            _teamCount = _editorTeams;
            _teamMode = TeamMode.FreeForAll;
            _observerMode = false;
            _campaignActive = false;

            _pendingSkirmish = true;
            _pendingDifficulty = _difficulty;
            _pendingPlayerFaction = _playerFaction;
            _pendingAiFaction = _aiFaction;
            _pendingTeamCount = _teamCount;
            _pendingTeamMode = _teamMode;
            _pendingObserver = false;
            Restart();
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
                    ResetCustomTeams();
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
                    if (_teamMode == TeamMode.Teams)
                    {
                        ResetCustomTeams();
                    }
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

            if (_teamMode == TeamMode.Teams)
            {
                for (var team = 0; team < _teamCount; team++)
                {
                    var alliance = _customAlliances[team] == 0 ? "A" : "B";
                    var previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = _customAlliances[team] == 0
                        ? new Color(0.28f, 0.46f, 0.64f)
                        : new Color(0.64f, 0.34f, 0.26f);

                    if (GUI.Button(
                            new Rect(x + 30f + team * 60f, y + 432f, 54f, 30f),
                            $"{team + 1}:{alliance}",
                            _button))
                    {
                        _customAlliances[team] = _customAlliances[team] == 0 ? 1 : 0;
                    }

                    GUI.backgroundColor = previousColor;
                }
            }
            else
            {
                GUI.Label(
                    new Rect(x + 30f, y + 432f, width - 60f, 34f),
                    $"{FactionCatalog.Name(_playerFaction)} vs {FactionCatalog.Name(_aiFaction)}\n" +
                    FactionCatalog.Description(_playerFaction),
                    _label);
            }

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
                _observerMode = false;

                if (needsReload)
                {
                    _pendingSkirmish = true;
                    _pendingDifficulty = _difficulty;
                    _pendingPlayerFaction = _playerFaction;
                    _pendingAiFaction = _aiFaction;
                    _pendingTeamCount = _teamCount;
                    _pendingTeamMode = _teamMode;
                    _pendingObserver = false;
                    Restart();
                }
                else
                {
                    BeginMatch(true);
                }
            }

            if (GUI.Button(
                    new Rect(x + 30f, y + 700f, width - 60f, 34f),
                    "OBSERVE AI VS AI",
                    _button))
            {
                var needsReload = MapRuntime.SelectedIndex != _selectedMap;
                MapRuntime.Select(_selectedMap);
                _campaignActive = false;
                _observerMode = true;

                if (needsReload)
                {
                    _pendingSkirmish = true;
                    _pendingDifficulty = _difficulty;
                    _pendingPlayerFaction = _playerFaction;
                    _pendingAiFaction = _aiFaction;
                    _pendingTeamCount = _teamCount;
                    _pendingTeamMode = _teamMode;
                    _pendingObserver = true;
                    Restart();
                }
                else
                {
                    BeginMatch(true);
                }
            }

            GUI.Label(
                new Rect(x + 30f, y + 750f, width - 60f, 24f),
                "WASD/edges pan Â· wheel zoom Â· Q/E rotate",
                _label);
        }

        private void ResetCustomTeams()
        {
            for (var team = 0; team < _customAlliances.Length; team++)
            {
                _customAlliances[team] = _teamMode == TeamMode.Teams &&
                                         team >= _teamCount / 2
                    ? 1
                    : 0;
            }
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
                new Rect(x + 104f, y + 366f, width - 134f, 70f),
                $"{FactionCatalog.Name(playerFaction)} vs {FactionCatalog.Name(aiFaction)}\n" +
                $"Map: {missionMap?.Name ?? "Ash Basin"}  |  {mission.teamCount} players\n" +
                $"Hero: {ProductionCatalog.UnitName(playerFaction, ProductionCatalog.UniqueFor(playerFaction))}" +
                $"  ({HeroProgress.Kills(playerFaction)} kills)\n" +
                mission.briefing,
                _label);

            GUI.Label(
                new Rect(x + 30f, y + 440f, width - 60f, 48f),
                $"CODEX: {mission.codex}\n" +
                "Branches: " + DescribeNextMissions(mission),
                _label);

            if (GUI.Button(
                    new Rect(x + 30f, y + 500f, width - 60f, 40f),
                    "START MISSION",
                    _button) &&
                MissionCatalog.IsUnlocked(_selectedMission))
            {
                _missionBriefing = _selectedMission;
            }
        }

        private static string DescribeNextMissions(MissionDto mission)
        {
            if (mission.next == null || mission.next.Length == 0)
            {
                return "campaign finale";
            }

            var names = new string[mission.next.Length];
            for (var i = 0; i < mission.next.Length; i++)
            {
                var index = MissionCatalog.FindIndex(mission.next[i]);
                names[i] = index >= 0
                    ? MissionCatalog.Missions[index].name
                    : mission.next[i];
            }

            return string.Join(" or ", names);
        }

        private void DrawMissionBriefing()
        {
            var missions = MissionCatalog.Missions;
            if (missions == null || _missionBriefing < 0 || _missionBriefing >= missions.Length)
            {
                _missionBriefing = -1;
                return;
            }

            var mission = missions[_missionBriefing];
            var mapIndex = MapCatalog.FindIndex(mission.mapId);
            var map = MapCatalog.Get(mapIndex);
            var playerFaction = FactionCatalog.Parse(mission.playerFaction);

            var previous = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.88f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            var width = Mathf.Min(800f, Screen.width - 60f);
            var height = Mathf.Min(660f, Screen.height - 60f);
            var x = (Screen.width - width) * 0.5f;
            var y = (Screen.height - height) * 0.5f;
            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.98f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(
                new Rect(x + 26f, y + 16f, width - 52f, 44f),
                mission.name,
                _title);

            var preview = MapPreviewCache.Get(mapIndex);
            if (preview != null)
            {
                GUI.DrawTexture(
                    new Rect(x + width - 220f, y + 70f, 180f, 180f),
                    preview,
                    ScaleMode.ScaleToFit);
            }

            var textStyle = new GUIStyle(_label) { wordWrap = true, fontSize = 15 };
            GUI.Label(
                new Rect(x + 26f, y + 70f, width - 280f, height - 160f),
                CampaignBriefing.Text(mission, map?.Name),
                textStyle);

            GUI.Label(
                new Rect(x + width - 220f, y + 260f, 190f, 90f),
                $"Hero: {ProductionCatalog.UnitName(playerFaction, ProductionCatalog.UniqueFor(playerFaction))}\n" +
                $"Career kills: {HeroProgress.Kills(playerFaction)}",
                textStyle);

            if (GUI.Button(
                    new Rect(x + 26f, y + height - 66f, 220f, 44f),
                    "DEPLOY",
                    _button))
            {
                var index = _missionBriefing;
                _missionBriefing = -1;
                BeginMission(index);
            }

            if (GUI.Button(
                    new Rect(x + 260f, y + height - 66f, 180f, 44f),
                    "BACK",
                    _button))
            {
                _missionBriefing = -1;
            }
        }

        private void DrawCampaignResult()
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.02f, 0.78f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.3f, Screen.width, 360f), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(
                new Rect(0f, Screen.height * 0.3f + 18f, Screen.width, 60f),
                _missionWon ? "MISSION COMPLETE" : "MISSION FAILED",
                _title);

            GUI.Label(
                new Rect(0f, Screen.height * 0.3f + 76f, Screen.width, 22f),
                MatchStatsText(),
                _label);

            var buttonY = Screen.height * 0.3f + 110f;
            var nextOptions = _missionWon
                ? MissionCatalog.NextMissionIndices(_selectedMission)
                : System.Array.Empty<int>();

            if (GUI.Button(
                    new Rect(Screen.width * 0.5f - 330f, buttonY, 200f, 44f),
                    "RETRY",
                    _button))
            {
                _pendingMission = _selectedMission;
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

            if (nextOptions.Length > 0)
            {
                const float branchWidth = 250f;
                const float branchGap = 10f;
                var total = nextOptions.Length * branchWidth +
                            (nextOptions.Length - 1) * branchGap;
                var startX = Screen.width * 0.5f - total * 0.5f;
                var branchY = buttonY + 64f;

                for (var i = 0; i < nextOptions.Length; i++)
                {
                    var index = nextOptions[i];
                    var name = MissionCatalog.Missions[index].name;
                    if (GUI.Button(
                            new Rect(
                                startX + i * (branchWidth + branchGap),
                                branchY,
                                branchWidth,
                                44f),
                            $"NEXT: {name}",
                            _button))
                    {
                        _pendingMission = index;
                        Restart();
                    }
                }
            }
        }

        private void DrawResult()
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.02f, 0.72f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.34f, Screen.width, 460f), Texture2D.whiteTexture);
            GUI.color = previous;

            string text = SkirmishCommands.ResultText(_winner);

            GUI.Label(
                new Rect(0f, Screen.height * 0.34f + 18f, Screen.width, 60f),
                text,
                _title);

            GUI.Label(
                new Rect(0f, Screen.height * 0.34f + 76f, Screen.width, 22f),
                MatchStatsText(),
                _label);

            if (GUI.Button(
                    new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.34f + 100f, 220f, 46f),
                    "RESTART",
                    _button))
            {
                Restart();
            }

            DrawHistoryGraph(
                Screen.width * 0.5f - 360f,
                Screen.height * 0.34f + 160f,
                720f,
                270f);
        }

        private void DrawHistoryGraph(float x, float y, float width, float height)
        {
            var samples = MatchHistory.Samples;
            if (samples.Count < 2)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 22f), "MATCH HISTORY (5 s)", _label);

            var half = (width - 30f) * 0.5f;
            DrawSeries(
                new Rect(x + 10f, y + 34f, half, 100f),
                "ARMY",
                samples,
                s => s.Units0,
                s => s.Units1);
            DrawSeries(
                new Rect(x + 20f + half, y + 34f, half, 100f),
                "CREDITS",
                samples,
                s => s.Credits0,
                s => s.Credits1);
            DrawSeries(
                new Rect(x + 10f, y + 154f, width - 20f, 90f),
                "KILLS",
                samples,
                s => s.Kills0,
                s => s.Kills1);

            GUI.Label(
                new Rect(x + 10f, y + height - 24f, width - 20f, 22f),
                "Player (team 0) warm  |  Opponent (team 1) cool",
                _label);
        }

        private static void DrawSeries(
            Rect rect,
            string label,
            System.Collections.Generic.IReadOnlyList<MatchHistory.Sample> samples,
            System.Func<MatchHistory.Sample, int> first,
            System.Func<MatchHistory.Sample, int> second)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), label);

            var plot = new Rect(rect.x, rect.y + 20f, rect.width, rect.height - 20f);
            var count = samples.Count;
            var start = math.max(0, count - 160);
            var span = count - start;
            if (span < 2)
            {
                return;
            }

            var max = 1;
            for (var i = start; i < count; i++)
            {
                max = math.max(max, math.max(first(samples[i]), second(samples[i])));
            }

            var step = plot.width / (span - 1);
            var barWidth = math.max(2f, step + 0.5f);

            for (var i = start; i < count; i++)
            {
                var index = i - start;
                var px = plot.x + index * step;
                DrawBar(plot, px, barWidth, first(samples[i]), max,
                    Color.Lerp(new Color(0.95f, 0.62f, 0.20f), Color.white, 0.15f));
                DrawBar(plot, px, barWidth, second(samples[i]), max,
                    new Color(0.22f, 0.52f, 0.86f));
            }
        }

        private static void DrawBar(
            Rect plot,
            float x,
            float barWidth,
            int value,
            int max,
            Color color)
        {
            var fraction = math.saturate(value / (float)math.max(1, max));
            var height = plot.height * fraction * 0.92f;
            if (height <= 0f)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 0.72f);
            GUI.DrawTexture(
                new Rect(x, plot.y + plot.height - height, barWidth, height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string MatchStatsText()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return string.Empty;
            }

            if (!MatchStatsCommands.TryRead(
                    world.EntityManager, SkirmishCommands.PlayerTeam, out var stats))
            {
                return string.Empty;
            }

            return $"Built {stats.UnitsBuilt}  |  Kills {stats.Kills}  |  " +
                   $"Losses {stats.Losses}  |  Buildings destroyed {stats.BuildingsDestroyed}";
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
