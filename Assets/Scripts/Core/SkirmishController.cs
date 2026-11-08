using System;
using CQRa.Sim.AI;
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

        private AiDifficulty _difficulty = AiDifficulty.Veteran;
        private bool _active;
        private bool _matchOver;
        private int _winner = 255;
        private bool _captureRun;

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

        private void Awake()
        {
            _captureRun = HasArg("-screenshot");

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
            if (match.GameOver == 0)
            {
                return;
            }

            _matchOver = true;
            _winner = match.Winner;
            Time.timeScale = 0f;

            if (_selection != null)
            {
                _selection.enabled = false;
            }
        }

        private void BeginMatch(bool withAi)
        {
            _active = true;
            _matchOver = false;
            Time.timeScale = 1f;

            if (_selection != null)
            {
                _selection.enabled = true;
            }

            if (_cameraRig != null)
            {
                _cameraRig.enabled = true;
            }

            if (!withAi)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                SkirmishCommands.StartMatch(world.EntityManager, _difficulty);
            }

            Debug.Log($"[Skirmish] match started vs {_difficulty} AI");
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

            if (_matchOver)
            {
                DrawResult();
            }
        }

        private void DrawMenu()
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.035f, 0.03f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var width = 520f;
            var height = 420f;
            var x = (Screen.width - width) * 0.5f;
            var y = (Screen.height - height) * 0.5f;
            var panel = new Rect(x, y, width, height);

            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x + 30f, y + 24f, width - 60f, 54f), "C&Q R-A REVIVAL", _title);
            GUI.Label(new Rect(x + 30f, y + 78f, width - 60f, 30f), "SKIRMISH", _subtitle);
            GUI.Label(
                new Rect(x + 30f, y + 112f, width - 60f, 46f),
                "The AI plays by exactly the same rules: no bonuses, no stat changes.",
                _label);

            GUI.Label(new Rect(x + 30f, y + 158f, width - 60f, 24f), "DIFFICULTY", _label);

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
                        new Rect(x + 30f + i * 116f, y + 186f, 106f, 38f),
                        difficulty.ToString(),
                        _button))
                {
                    _difficulty = difficulty;
                }

                GUI.backgroundColor = previousColor;
            }

            GUI.Label(
                new Rect(x + 30f, y + 236f, width - 60f, 46f),
                Describe(_difficulty),
                _label);

            var start = GUI.Button(
                new Rect(x + 30f, y + 300f, width - 60f, 52f),
                "START MATCH",
                _button);

            GUI.Label(
                new Rect(x + 30f, y + 360f, width - 60f, 24f),
                "WASD/edges pan · wheel zoom · Q/E rotate",
                _label);

            if (start)
            {
                BeginMatch(true);
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
    }
}
