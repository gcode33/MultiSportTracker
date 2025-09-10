using Microsoft.AspNetCore.Mvc;
using MultiSportTracker.Data;

namespace MultiSportTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SportsController : ControllerBase
    {
        private readonly ISportsApiService _api;

        public SportsController(ISportsApiService api) => _api = api;

        [HttpGet("teams/{league}")]
        public async Task<IActionResult> GetTeams(string league)
        {
            var t = await _api.GetTeamsAsync(league);
            return Ok(t);
        }

        [HttpGet("events/{teamId}")]
        public async Task<IActionResult> GetEvents(string teamId)
        {
            var e = await _api.GetEventsAsync(teamId);
            return Ok(e);
        }

        [HttpGet("players/{teamId}")]
        public async Task<IActionResult> GetPlayers(string teamId)
        {
            var p = await _api.GetPlayersAsync(teamId);
            return Ok(p);
        }
    }
}
