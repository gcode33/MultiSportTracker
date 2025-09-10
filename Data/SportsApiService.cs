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
                
                // Get league mappings for the sport
                var leagueMappings = GetLeagueMappings(league);
                var allTeams = new List<Team>();
                
                // Fetch teams from multiple leagues for this sport
                foreach (var mapping in leagueMappings)
                {
                    try
                    {
                        TeamResponse? resp = null;
                        
                        // Try league ID lookup first (more reliable)
                        if (!string.IsNullOrEmpty(mapping.LeagueId))
                        {
                            resp = await _http.GetFromJsonAsync<TeamResponse>($"lookup_all_teams.php?id={mapping.LeagueId}");
                        }
                        
                        // Fallback to league name search
                        if (resp?.teams == null && !string.IsNullOrEmpty(mapping.LeagueName))
                        {
                            resp = await _http.GetFromJsonAsync<TeamResponse>($"search_all_teams.php?l={Uri.EscapeDataString(mapping.LeagueName)}");
                        }
                        
                        if (resp?.teams != null)
                        {
                            allTeams.AddRange(resp.teams);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch teams for league {LeagueName} (ID: {LeagueId})", mapping.LeagueName, mapping.LeagueId);
                    }
                }
                
                _logger.LogInformation("Successfully fetched {Count} teams for sport: {League}", allTeams.Count, league);
                _cache.Set(key, allTeams, minutes: 30);
                return allTeams;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when fetching teams for league: {League}", league);
                return GetFallbackTeams(league);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when fetching teams for league: {League}", league);
                return GetFallbackTeams(league);
            }
        }

        public async Task<List<Event>> GetEventsAsync(string teamId)
        {
            var key = $"events:{teamId}";
            if (_cache.TryGet<List<Event>>(key, out var cached)) return cached;

            try
            {
                _logger.LogInformation("Fetching events for team: {TeamId}", teamId);
                var resp = await _http.GetFromJsonAsync<EventResponse>($"eventsnext.php?id={Uri.EscapeDataString(teamId)}");
                var list = resp?.events ?? new List<Event>();
                
                _logger.LogInformation("Successfully fetched {Count} events for team: {TeamId}", list.Count, teamId);
                _cache.Set(key, list, minutes: 5);
                return list;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when fetching events for team: {TeamId}", teamId);
                return new List<Event>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when fetching events for team: {TeamId}", teamId);
                return new List<Event>();
            }
        }

        public async Task<List<Player>> GetPlayersAsync(string teamId)
        {
            var key = $"players:{teamId}";
            if (_cache.TryGet<List<Player>>(key, out var cached)) return cached;

            try
            {
                _logger.LogInformation("Fetching players for team: {TeamId}", teamId);
                var resp = await _http.GetFromJsonAsync<PlayerResponse>($"lookup_all_players.php?id={Uri.EscapeDataString(teamId)}");
                var list = resp?.player ?? new List<Player>();
                
                _logger.LogInformation("Successfully fetched {Count} players for team: {TeamId}", list.Count, teamId);
                _cache.Set(key, list, minutes: 60);
                return list;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when fetching players for team: {TeamId}", teamId);
                return new List<Player>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when fetching players for team: {TeamId}", teamId);
                return new List<Player>();
            }
        }

        private List<LeagueMapping> GetLeagueMappings(string sport)
        {
            return sport.ToLowerInvariant() switch
            {
                "soccer" => new List<LeagueMapping>
                {
                    new("", "English Premier League"),
                    new("", "Spanish La Liga"),
                    new("", "German Bundesliga"),
                    new("", "Italian Serie A"),
                    new("", "French Ligue 1"),
                    new("", "American Major League Soccer"),
                },
                "basketball" => new List<LeagueMapping>
                {
                    new("", "NBA"),
                    new("", "NCAA"),
                    new("", "WNBA"),
                },
                "baseball" => new List<LeagueMapping>
                {
                    new("", "MLB"),
                    new("", "NCAA Baseball"),
                },
                "football" or "american football" => new List<LeagueMapping>
                {
                    new("", "NFL"),
                    new("", "NCAA Football"),
                },
                _ => new List<LeagueMapping>()
            };
        }

        private List<Team> GetFallbackTeams(string league)
        {
            _logger.LogInformation("Using fallback data for league: {League}", league);
            
            return league.ToLowerInvariant() switch
            {
                "soccer" or "english premier league" => new List<Team>
                {
                    new() { idTeam = "133604", strTeam = "Arsenal", strLeague = "English Premier League", strSport = "Soccer", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/vysruo1448813175.png" },
                    new() { idTeam = "133613", strTeam = "Chelsea", strLeague = "English Premier League", strSport = "Soccer", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/yvwvtu1448813215.png" },
                    new() { idTeam = "133602", strTeam = "Manchester United", strLeague = "English Premier League", strSport = "Soccer", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/xzqdr11517509072.png" },
                    new() { idTeam = "133616", strTeam = "Liverpool", strLeague = "English Premier League", strSport = "Soccer", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/tr61id1519401148.png" }
                },
                "basketball" => new List<Team>
                {
                    new() { idTeam = "134859", strTeam = "Los Angeles Lakers", strLeague = "NBA", strSport = "Basketball", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/yqrxrs1420568796.png" },
                    new() { idTeam = "134860", strTeam = "Boston Celtics", strLeague = "NBA", strSport = "Basketball", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/xqtxpy1418850263.png" },
                    new() { idTeam = "134861", strTeam = "Golden State Warriors", strLeague = "NBA", strSport = "Basketball", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/qsuypq1420568103.png" }
                },
                "baseball" => new List<Team>
                {
                    new() { idTeam = "135249", strTeam = "New York Yankees", strLeague = "MLB", strSport = "Baseball", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/tpvstp1438783811.png" },
                    new() { idTeam = "135252", strTeam = "Los Angeles Dodgers", strLeague = "MLB", strSport = "Baseball", strTeamBadge = "https://www.thesportsdb.com/images/media/team/badge/wywrtu1438823394.png" }
                },
                _ => new List<Team>()
            };
        }
    }

    public record LeagueMapping(string LeagueId, string LeagueName);
}
