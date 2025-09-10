namespace MultiSportTracker.Models
{
    public class EventResponse
    {
        public List<Event> events { get; set; } = new();
    }

    public class Event
    {
        public string idEvent { get; set; } = "";
        public string strEvent { get; set; } = "";
        public string dateEvent { get; set; } = "";
        public string strTime { get; set; } = "";
        public string strLeague { get; set; } = "";
    }
}
