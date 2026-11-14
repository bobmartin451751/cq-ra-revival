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
        private static readonly int[] Alliances = { 0, 1, 2, 3, 4, 5, 6, 7 };

        public static int TeamCount => _teamCount;
        public static TeamMode Mode => _mode;

        public static void Configure(int teamCount, TeamMode mode)
        {
            _teamCount = math.clamp(teamCount, 2, MaxTeams);
            _mode = mode;
            for (var team = 0; team < MaxTeams; team++)
            {
                Alliances[team] = mode == TeamMode.Teams
                    ? (team < _teamCount / 2 ? 0 : 1)
                    : team;
            }
        }

        public static void Configure(int teamCount, TeamMode mode, int[] alliances)
        {
            Configure(teamCount, mode);
            if (alliances == null)
            {
                return;
            }

            for (var team = 0; team < MaxTeams && team < alliances.Length; team++)
            {
                Alliances[team] = alliances[team];
            }
        }

        public static void SetAlliance(int team, int alliance)
        {
            if (team >= 0 && team < MaxTeams)
            {
                Alliances[team] = alliance;
            }
        }

        public static void Reset()
        {
            _teamCount = 2;
            _mode = TeamMode.FreeForAll;
            for (var team = 0; team < MaxTeams; team++)
            {
                Alliances[team] = team;
            }
        }

        public static int AllianceFor(int team)
        {
            if (_mode == TeamMode.FreeForAll)
            {
                return team;
            }

            if (team < 0 || team >= MaxTeams)
            {
                return team;
            }

            return Alliances[team];
        }

        public static bool AreAllied(int a, int b)
        {
            return a == b || AllianceFor(a) == AllianceFor(b);
        }
    }
}
