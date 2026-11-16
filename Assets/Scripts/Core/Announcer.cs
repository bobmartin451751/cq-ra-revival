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
            AudioDirector.Play(AudioCue.Click);
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

        public static void BaseUnderAttack()
        {
            if (Time.unscaledTime < _nextBaseWarning)
            {
                return;
            }

            _nextBaseWarning = Time.unscaledTime + 8f;
            Push("Base under attack", 2.5f);
        }
    }
}
