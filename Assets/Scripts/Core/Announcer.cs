using UnityEngine;

namespace CQRa.Core
{
    /// <summary>Short radio-style subtitles for match events.</summary>
    public static class Announcer
    {
        public static string Message { get; private set; } = string.Empty;
        public static float Until { get; private set; }

        public static void Push(string message, float seconds = 3f)
        {
            Message = message;
            Until = Time.unscaledTime + seconds;
            AudioDirector.Play(AudioCue.Announcer);
        }

        public static bool TryGetCurrent(out string message)
        {
            if (Time.unscaledTime <= Until && !string.IsNullOrEmpty(Message))
            {
                message = Message;
                return true;
            }

            message = string.Empty;
            return false;
        }

        public static void Clear()
        {
            Message = string.Empty;
            Until = 0f;
        }

        private static float _nextBaseWarning;

        public static Vector3 LastAttackPosition { get; private set; }
        public static float AttackPositionUntil { get; private set; }

        public static void BaseUnderAttack(Vector3 position)
        {
            LastAttackPosition = position;
            AttackPositionUntil = Time.unscaledTime + 8f;

            if (Time.unscaledTime < _nextBaseWarning)
            {
                return;
            }

            _nextBaseWarning = Time.unscaledTime + 8f;
            Push("Base under attack", 2.5f);
        }

        public static bool TryGetAttackPosition(out Vector3 position)
        {
            if (Time.unscaledTime <= AttackPositionUntil)
            {
                position = LastAttackPosition;
                return true;
            }

            position = default;
            return false;
        }
    }
}
