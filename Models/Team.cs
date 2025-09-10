namespace MultiSportTracker.Models
{
    public class TeamResponse
    {
        public List<Team> teams { get; set; } = new();
    }

    public class Team
    {
        public string idTeam { get; set; } = "";
        public string strTeam { get; set; } = "";
        public string strSport { get; set; } = "";
        public string strLeague { get; set; } = "";
        public string strTeamBadge { get; set; } = "";
    }
}
