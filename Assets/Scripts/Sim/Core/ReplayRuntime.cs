using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Unity.Entities;
using UnityEngine;

namespace CQRa.Sim.Core
{
    [Serializable]
    public sealed class ReplayFrame
    {
        public int tick;
        public MatchSaveData state;
    }

    /// <summary>Versioned replay header plus deterministic state keyframes.</summary>
    [Serializable]
    public sealed class ReplayData
    {
        public int version = 1;
        public string mapId;
        public string playerFaction;
        public string aiFaction;
        public int teamCount;
        public string teamMode;
        public int difficulty;
        public int observer;
        public string recordedUtc;
        public ReplayFrame[] frames;
    }

    /// <summary>
    /// Records the match as periodic state keyframes. Because every frame is a full
    /// snapshot, playback does not depend on machine speed or ECS entity IDs.
    /// </summary>
    public static class ReplayRuntime
    {
        public const int CurrentVersion = 1;
        public const int FrameIntervalTicks = 15;

        private static readonly List<ReplayFrame> Captured = new List<ReplayFrame>();
        private static ReplayData _data;
        private static ReplayData _playback;
        private static int _playbackIndex;
        private static int _lastCapturedTick = -1;
        private static bool _finished;

        public static bool IsRecording { get; private set; }
        public static bool IsPlaying { get; private set; }
        public static ReplayData Playback => _playback;
        public static int FrameCount => Captured.Count;

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, "cqra-replay.dat");

        public static bool HasReplay => File.Exists(FilePath);

        public static void BeginRecording(
            string mapId,
            string playerFaction,
            string aiFaction,
            int teamCount,
            string teamMode,
            int difficulty,
            bool observer)
        {
            Captured.Clear();
            _lastCapturedTick = -1;
            _finished = false;
            _data = new ReplayData
            {
                version = CurrentVersion,
                mapId = mapId,
                playerFaction = playerFaction,
                aiFaction = aiFaction,
                teamCount = teamCount,
                teamMode = teamMode,
                difficulty = difficulty,
                observer = observer ? 1 : 0,
                recordedUtc = DateTime.UtcNow.ToString("o"),
            };
            IsRecording = true;
            IsPlaying = false;
            _playback = null;
            _playbackIndex = 0;
        }

        public static void CaptureFrame(EntityManager entityManager, int tick)
        {
            if (!IsRecording || tick <= _lastCapturedTick)
            {
                return;
            }

            _lastCapturedTick = tick;
            Captured.Add(new ReplayFrame
            {
                tick = tick,
                state = MatchSaveSerializer.Capture(entityManager),
            });
        }

        public static ReplayData StopRecording(EntityManager entityManager)
        {
            if (!IsRecording)
            {
                return _data;
            }

            CaptureFrame(
                entityManager,
                Captured.Count == 0 ? 0 : Captured[Captured.Count - 1].tick + FrameIntervalTicks);
            IsRecording = false;
            _data.frames = Captured.ToArray();
            return _data;
        }

        public static bool SaveLastReplay(EntityManager entityManager)
        {
            var data = StopRecording(entityManager);
            if (data == null || data.frames == null || data.frames.Length == 0)
            {
                return false;
            }

            ReplayStorage.SaveToFile(data, FilePath);
            return true;
        }

        public static void CancelRecording()
        {
            IsRecording = false;
            Captured.Clear();
            _data = null;
        }

        private static ReplayData _pendingPlayback;

        public static void QueuePlayback(ReplayData data)
        {
            _pendingPlayback = data;
        }

        public static ReplayData TakePendingPlayback()
        {
            var data = _pendingPlayback;
            _pendingPlayback = null;
            return data;
        }

        public static bool BeginPlayback(ReplayData data)
        {
            if (data == null || data.frames == null || data.frames.Length == 0)
            {
                return false;
            }

            IsRecording = false;
            _playback = data;
            _playbackIndex = 0;
            _finished = false;
            IsPlaying = true;
            return true;
        }

        public static void StopPlayback()
        {
            IsPlaying = false;
            _playback = null;
            _playbackIndex = 0;
            _finished = false;
        }

        /// <summary>Applies every frame whose tick has been reached. Returns true when finished.</summary>
        public static bool TickPlayback(EntityManager entityManager, int tick)
        {
            if (!IsPlaying || _playback?.frames == null)
            {
                return false;
            }

            var frames = _playback.frames;
            while (_playbackIndex < frames.Length && frames[_playbackIndex].tick <= tick)
            {
                MatchSaveSerializer.Apply(entityManager, frames[_playbackIndex].state);
                _playbackIndex++;
            }

            if (_playbackIndex >= frames.Length)
            {
                IsPlaying = false;
                _finished = true;
                return true;
            }

            return false;
        }

        public static bool ConsumeFinished()
        {
            if (!_finished)
            {
                return false;
            }

            _finished = false;
            return true;
        }

        public static void Reset()
        {
            IsRecording = false;
            IsPlaying = false;
            _playback = null;
            _playbackIndex = 0;
            _lastCapturedTick = -1;
            _finished = false;
            Captured.Clear();
            _data = null;
        }
    }

    public static class ReplayStorage
    {
        public static void SaveToFile(ReplayData data, string path)
        {
            var json = JsonUtility.ToJson(data);
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionMode.Compress);
            using var writer = new StreamWriter(gzip);
            writer.Write(json);
        }

        public static ReplayData LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var data = JsonUtility.FromJson<ReplayData>(reader.ReadToEnd());
            if (data == null || data.version != ReplayRuntime.CurrentVersion)
            {
                return null;
            }

            return data.frames != null && data.frames.Length > 0 ? data : null;
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(SimClockSystem))]
    public partial class ReplayCaptureSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!ReplayRuntime.IsRecording || SimClock.Tick % ReplayRuntime.FrameIntervalTicks != 0)
            {
                return;
            }

            ReplayRuntime.CaptureFrame(EntityManager, SimClock.Tick);
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ReplayCaptureSystem))]
    public partial class ReplayPlaybackSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (ReplayRuntime.IsPlaying)
            {
                ReplayRuntime.TickPlayback(EntityManager, SimClock.Tick);
            }
        }
    }
}
