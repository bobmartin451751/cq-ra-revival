using System.Text;

namespace CQRa.Sim.Campaign
{
    /// <summary>Builds the full-screen pre-mission briefing text.</summary>
    public static class CampaignBriefing
    {
        public static string Text(MissionDto mission, string mapName)
        {
            if (mission == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(512);
            builder.Append("OBJECTIVE\n")
                .Append(string.IsNullOrEmpty(mission.objectiveText)
                    ? mission.objective ?? "Destroy the enemy."
                    : mission.objectiveText)
                .Append("\n\nBRIEFING\n")
                .Append(mission.briefing ?? string.Empty)
                .Append("\n\nCODEX\n")
                .Append(string.IsNullOrEmpty(mission.codex) ? mission.briefing : mission.codex)
                .Append("\n\nMAP\n")
                .Append(string.IsNullOrEmpty(mapName) ? mission.mapId : mapName)
                .Append("  |  ")
                .Append(mission.teamCount)
                .Append(" players  |  ")
                .Append(mission.difficulty)
                .Append("\n\nNEXT\n")
                .Append(MissionCatalog.NextMissionNames(mission));
            return builder.ToString();
        }
    }
}
