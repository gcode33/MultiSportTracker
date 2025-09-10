using MultiSportTracker.Models;

namespace MultiSportTracker.Data
{
    public interface ISportsApiService
    {
        Task<List<Team>> GetTeamsAsync(string league);
        Task<List<Event>> GetEventsAsync(string teamId);
        Task<List<Player>> GetPlayersAsync(string teamId);
    }
}
