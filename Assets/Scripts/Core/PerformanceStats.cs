using UnityEngine;

namespace CQRa.Core
{
    /// <summary>Rolling frame-time window used by the F3 overlay. Pure and testable.</summary>
    public sealed class PerformanceStats
    {
        private readonly float[] _frameMs;

        public PerformanceStats(int capacity = 120)
        {
            _frameMs = new float[Mathf.Max(2, capacity)];
        }

        public int Count { get; private set; }
        public int Capacity => _frameMs.Length;
        public int Next { get; private set; }

        public void Add(float frameMilliseconds)
        {
            if (frameMilliseconds < 0f)
            {
                frameMilliseconds = 0f;
            }

            _frameMs[Next] = frameMilliseconds;
            Next = (Next + 1) % _frameMs.Length;
            if (Count < _frameMs.Length)
            {
                Count++;
            }
        }

        public float AverageMs()
        {
            if (Count == 0)
            {
                return 0f;
            }

            var sum = 0f;
            for (var i = 0; i < Count; i++)
            {
                sum += _frameMs[i];
            }

            return sum / Count;
        }

        public float WorstMs()
        {
            var worst = 0f;
            for (var i = 0; i < Count; i++)
            {
                worst = Mathf.Max(worst, _frameMs[i]);
            }

            return worst;
        }

        public float AverageFps()
        {
            var average = AverageMs();
            return average <= 0.0001f ? 0f : 1000f / average;
        }

        public void Clear()
        {
            Count = 0;
            Next = 0;
        }
    }
}
