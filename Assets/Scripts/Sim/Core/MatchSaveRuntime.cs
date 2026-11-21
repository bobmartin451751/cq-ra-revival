namespace CQRa.Sim.Core
{
    /// <summary>Snapshot queued across a scene reload for the load pipeline.</summary>
    public static class MatchSaveRuntime
    {
        public static MatchSaveData Pending { get; private set; }

        public static bool HasPending => Pending != null;

        public static void Queue(MatchSaveData data)
        {
            Pending = data;
        }

        public static MatchSaveData Take()
        {
            var data = Pending;
            Pending = null;
            return data;
        }

        public static void Clear()
        {
            Pending = null;
        }
    }
}
