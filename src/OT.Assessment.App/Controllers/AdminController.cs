using Microsoft.AspNetCore.Mvc;
using OT.Assessment.App.Models;
using OT.Assessment.App.Services;
using OT.Assessment.Common.Responses;

namespace OT.Assessment.App.Controllers
{
    /// <summary>Admin only: runs the test projects of this solution.</summary>
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly AdminSessions _adminSessions;
        private readonly ITestRunner _runner;
        private readonly IPlayerAccountStore _players;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AdminSessions adminSessions,
            ITestRunner runner,
            IPlayerAccountStore players,
            ILogger<AdminController> logger)
        {
            _adminSessions = adminSessions;
            _runner = runner;
            _players = players;
            _logger = logger;
        }

        /// <summary>
        /// Lists the registered players (players with a login), optionally filtered by part of the
        /// login name, with the total each has spent. Use a player's accountId with
        /// GET api/player/{accountId}/casino to see their bets.
        /// Needs the admin token in the X-Admin-Token header (401 without it, 400 if count is not 1-100).
        /// </summary>
        [HttpGet("players")]
        public async Task<ActionResult<List<TopSpenderResponse>>> GetPlayers(
            [FromHeader(Name = "X-Admin-Token")] string token,
            [FromQuery] string search,
            [FromQuery] int count = 50,
            CancellationToken cancellationToken = default)
        {
            if (!_adminSessions.IsValid(token))
            {
                return Unauthorized();
            }

            if (count < 1 || count > 100)
            {
                return BadRequest("Count must be between 1 and 100.");
            }

            var players = await _players.ListPlayersAsync(search ?? "", count, cancellationToken);

            _logger.LogInformation("Admin listed registered players: search '{Search}', returned {Count}", search, players.Count);

            return Ok(players);
        }

        /// <summary>
        /// Runs a test project and returns its output. kind = "unit" runs the unit tests,
        /// kind = "load" runs the Tester (load test against the POST endpoint).
        /// Needs the admin token (from login) in the X-Admin-Token header.
        /// 401 without it, 400 for an unknown kind, 409 if a run is already in progress.
        /// </summary>
        [HttpPost("tests/{kind}")]
        public async Task<ActionResult<TestRunResult>> RunTests(
            string kind,
            [FromHeader(Name = "X-Admin-Token")] string token,
            CancellationToken cancellationToken)
        {
            if (!_adminSessions.IsValid(token))
            {
                return Unauthorized();
            }

            if (kind != "unit" && kind != "load")
            {
                return BadRequest("Kind must be 'unit' or 'load'.");
            }

            if (_runner.IsRunning)
            {
                return Conflict("A test run is already in progress.");
            }

            _logger.LogInformation("Admin started the {Kind} tests", kind);
            var result = await _runner.RunAsync(kind, cancellationToken);
            _logger.LogInformation("The {Kind} tests finished: passed {Passed}, exit code {ExitCode}, {Seconds} s",
                kind, result.Passed, result.ExitCode, result.Seconds);

            return Ok(result);
        }
    }
}
