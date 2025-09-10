using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MultiSportTracker.Models;
using MultiSportTracker.Services;

namespace MultiSportTracker.Data
{
    public class SportsApiService : ISportsApiService
    {
        private readonly HttpClient _http;
        private readonly CacheService _cache;
        private readonly ILogger<SportsApiService> _logger;

        public SportsApiService(HttpClient http, CacheService cache, ILogger<SportsApiService> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<Team>> GetTeamsAsync(string league)
        {
            var key = $"teams:{league}";
            if (_cache.TryGet<List<Team>>(key, out var cached)) return cached;

            try
            {
                _logger.LogInformation("Fetching teams for league: {League}", league);
                var resp = await _http.GetFromJsonAsync<TeamResponse>($"search_all_teams.php?l={Uri.EscapeDataString(league)}");
                var list = resp?.teams ?? new List<Team>();
                
                _logger.LogInformation("Successfully fetched {Count} teams for league: {League}", list.Count, league);
                _cache.Set(key, list, minutes: 30);
                return list;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when fetching teams for league: {League}", league);
                return GetFallbackTeams(league);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when fetching teams for league: {League}", league);
                return new List<Team>();
            }
        }

        public async Task<List<Event>> GetEventsAsync(string teamId)
        {
            var key = $"events:{teamId}";
            if (_cache.TryGet<List<Event>>(key, out var cached)) return cached;

            var resp = await _http.GetFromJsonAsync<EventResponse>($"eventsnext.php?id={Uri.EscapeDataString(teamId)}");
            var list = resp?.events ?? new List<Event>();
            _cache.Set(key, list, minutes: 5);
            return list;
        }

        public async Task<List<Player>> GetPlayersAsync(string teamId)
        {
            var key = $"players:{teamId}";
            if (_cache.TryGet<List<Player>>(key, out var cached)) return cached;

            var resp = await _http.GetFromJsonAsync<PlayerResponse>($"lookup_all_players.php?id={Uri.EscapeDataString(teamId)}");
            var list = resp?.player ?? new List<Player>();
            _cache.Set(key, list, minutes: 60);
            return list;
        }
    }
}
