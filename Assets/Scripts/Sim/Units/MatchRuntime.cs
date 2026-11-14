using Unity.Mathematics;

namespace CQRa.Sim.Units
{
    public enum TeamMode : byte
    {
        FreeForAll = 0,
        Teams = 1,
    }

    /// <summary>
    /// Per-match team layout. Team 0 is always the local player. Team count is
    /// 2..8; team mode puts the first half against the second half.
    /// </summary>
    public static class MatchRuntime
    {
        public const int MaxTeams = 8;

        private static int _teamCount = 2;
        private static TeamMode _mode = TeamMode.FreeForAll;

        public static int TeamCount => _teamCount;
        public static TeamMode Mode => _mode;

        public static void Configure(int teamCount, TeamMode mode)
        {
            _teamCount = math.clamp(teamCount, 2, MaxTeams);
            _mode = mode;
        }

        public static void Reset()
        {
            _teamCount = 2;
            _mode = TeamMode.FreeForAll;
        }

        public static int AllianceFor(int team)
        {
            if (_mode == TeamMode.FreeForAll)
            {
                return team;
            }

            return team < _teamCount / 2 ? 0 : 1;
        }

        public static bool AreAllied(int a, int b)
        {
            return a == b || AllianceFor(a) == AllianceFor(b);
        }
    }
}
