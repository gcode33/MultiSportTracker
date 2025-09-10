namespace MultiSportTracker.Models
{
    public class PlayerResponse
    {
        public List<Player> player { get; set; } = new();
    }

    public class Player
    {
        public string idPlayer { get; set; } = "";
        public string strPlayer { get; set; } = "";
        public string strPosition { get; set; } = "";
        public string strTeam { get; set; } = "";
        public string strCutout { get; set; } = "";
    }
}
