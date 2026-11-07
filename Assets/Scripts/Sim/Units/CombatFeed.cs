using System.Collections.Generic;
using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    /// <summary>
    /// Presentation-only bridge from the deterministic simulation to the view.
    ///
    /// The combat system appends short-lived tracer segments; <c>CombatTracerRenderer</c>
    /// drains the queue and draws them. Nothing here feeds back into gameplay, so the
    /// simulation stays deterministic and headless-safe.
    /// </summary>
    public static class CombatFeed
    {
        public struct Tracer
        {
            public float3 From;
            public float3 To;
        }

        private const int Capacity = 256;
        private static readonly Queue<Tracer> Pending = new Queue<Tracer>(Capacity);

        public static void AddTracer(float3 from, float3 to)
        {
            if (Pending.Count >= Capacity)
            {
                Pending.Dequeue();
            }

            Pending.Enqueue(new Tracer { From = from, To = to });
        }

        public static bool TryTake(out Tracer tracer)
        {
            if (Pending.Count == 0)
            {
                tracer = default;
                return false;
            }

            tracer = Pending.Dequeue();
            return true;
        }

        public static void Clear()
        {
            Pending.Clear();
        }
    }
}
