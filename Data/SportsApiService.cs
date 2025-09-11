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
                
                // First, try to get the team name from our teams cache or API
                var teamName = await GetTeamNameAsync(teamId);
                _logger.LogInformation("Found team name: {TeamName} for ID: {TeamId}", teamName ?? "null", teamId);
                
                var players = new List<Player>();
                
                // Try ID-based lookup first (most direct)
                try
                {
                    var resp = await _http.GetFromJsonAsync<PlayerResponse>($"lookup_all_players.php?id={Uri.EscapeDataString(teamId)}");
                    if (resp?.player != null && resp.player.Any())
                    {
                        players = resp.player;
                        _logger.LogInformation("Got {Count} players from ID lookup for team: {TeamId}", players.Count, teamId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to lookup players by team ID: {TeamId}", teamId);
                }
                
                // Try team name search if ID lookup didn't work
                if (!players.Any() && !string.IsNullOrEmpty(teamName))
                {
                    try
                    {
                        var resp = await _http.GetFromJsonAsync<PlayerResponse>($"searchplayers.php?t={Uri.EscapeDataString(teamName)}");
                        if (resp?.player != null && resp.player.Any())
                        {
                            players = resp.player;
                            _logger.LogInformation("Got {Count} players from name search for team: {TeamName}", players.Count, teamName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to search players by team name: {TeamName}", teamName);
                    }
                }
                
                // Now check what we got
                if (players.Any())
                {
                    var isArsenalTeam = teamName?.Contains("Arsenal", StringComparison.OrdinalIgnoreCase) == true;
                    var hasArsenalPlayers = players.All(p => p.strTeam?.Contains("Arsenal", StringComparison.OrdinalIgnoreCase) == true);
                    
                    _logger.LogInformation("Team analysis - IsArsenalTeam: {IsArsenal}, HasArsenalPlayers: {HasArsenal}, PlayerTeam: {PlayerTeam}", 
                        isArsenalTeam, hasArsenalPlayers, players.FirstOrDefault()?.strTeam ?? "null");
                    
                    // If this is not Arsenal but we got Arsenal players, use fallback data
                    if (!isArsenalTeam && hasArsenalPlayers)
                    {
                        _logger.LogInformation("API limitation detected: Non-Arsenal team got Arsenal players. Using fallback data.");
                        players = GetFallbackPlayers(teamId, teamName ?? "Unknown Team");
                    }
                    else
                    {
                        _logger.LogInformation("Using real API player data for team: {TeamName}", teamName);
                    }
                }
                else
                {
                    // No players found at all, use fallback
                    _logger.LogInformation("No players found from API, using fallback data for team: {TeamName}", teamName);
                    players = GetFallbackPlayers(teamId, teamName ?? "Unknown Team");
                }
                
                _logger.LogInformation("Returning {Count} players for team: {TeamId} ({TeamName})", players.Count, teamId, teamName);
                _cache.Set(key, players, minutes: 60);
                return players;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when fetching players for team: {TeamId}", teamId);
                return GetFallbackPlayers(teamId, "Unknown Team");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when fetching players for team: {TeamId}", teamId);
                return GetFallbackPlayers(teamId, "Unknown Team");
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

        private async Task<string?> GetTeamNameAsync(string teamId)
        {
            // First check if we have the team in our cache from previous team lookups
            var cacheKeys = new[] { "teams:soccer", "teams:basketball", "teams:baseball", "teams:football" };
            
            foreach (var cacheKey in cacheKeys)
            {
                if (_cache.TryGet<List<Team>>(cacheKey, out var teams))
                {
                    var team = teams.FirstOrDefault(t => t.idTeam == teamId);
                    if (team != null)
                    {
                        return team.strTeam;
                    }
                }
            }
            
            // If not in cache, try to look up the team by ID
            try
            {
                var resp = await _http.GetFromJsonAsync<TeamResponse>($"lookupteam.php?id={Uri.EscapeDataString(teamId)}");
                return resp?.teams?.FirstOrDefault()?.strTeam;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to lookup team name for ID: {TeamId}", teamId);
                return null;
            }
        }

        private List<Player> GetFallbackPlayers(string teamId, string teamName)
        {
            _logger.LogInformation("Using fallback player data for team: {TeamName} (ID: {TeamId})", teamName, teamId);
            
            // Generate some sample players based on the team and sport
            var sport = GetSportFromTeamName(teamName);
            var positions = GetPositionsForSport(sport);
            
            var players = new List<Player>();
            var sampleNames = GetSampleNamesForSport(sport);
            
            for (int i = 0; i < Math.Min(positions.Length, sampleNames.Length); i++)
            {
                players.Add(new Player
                {
                    idPlayer = $"demo_{teamId}_{i}",
                    strPlayer = sampleNames[i],
                    strTeam = teamName,
                    strPosition = positions[i],
                    strCutout = "" // No images for demo data
                });
            }
            
            return players;
        }
        
        private string GetSportFromTeamName(string teamName)
        {
            if (string.IsNullOrEmpty(teamName)) return "Soccer";
            
            var name = teamName.ToLowerInvariant();
            if (name.Contains("lakers") || name.Contains("celtics") || name.Contains("warriors") || name.Contains("hawks")) return "Basketball";
            if (name.Contains("yankees") || name.Contains("dodgers") || name.Contains("red sox")) return "Baseball";
            if (name.Contains("patriots") || name.Contains("cowboys") || name.Contains("packers")) return "American Football";
            return "Soccer";
        }
        
        private string[] GetPositionsForSport(string sport)
        {
            return sport switch
            {
                "Basketball" => new[] { "Point Guard", "Shooting Guard", "Small Forward", "Power Forward", "Center" },
                "Baseball" => new[] { "Pitcher", "Catcher", "First Base", "Second Base", "Shortstop", "Third Base", "Left Field", "Center Field", "Right Field" },
                "American Football" => new[] { "Quarterback", "Running Back", "Wide Receiver", "Tight End", "Offensive Line", "Defensive Line", "Linebacker", "Cornerback", "Safety" },
                _ => new[] { "Goalkeeper", "Defender", "Midfielder", "Forward", "Winger" }
            };
        }
        
        private string[] GetSampleNamesForSport(string sport)
        {
            return sport switch
            {
                "Basketball" => new[] { "Demo Player 1", "Demo Player 2", "Demo Player 3", "Demo Player 4", "Demo Player 5" },
                "Baseball" => new[] { "Demo Pitcher", "Demo Catcher", "Demo Infielder 1", "Demo Infielder 2", "Demo Infielder 3", "Demo Infielder 4", "Demo Outfielder 1", "Demo Outfielder 2", "Demo Outfielder 3" },
                "American Football" => new[] { "Demo QB", "Demo RB", "Demo WR1", "Demo WR2", "Demo TE", "Demo OL", "Demo DL", "Demo LB", "Demo DB" },
                _ => new[] { "Demo Keeper", "Demo Defender 1", "Demo Midfielder 1", "Demo Forward 1", "Demo Winger" }
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
