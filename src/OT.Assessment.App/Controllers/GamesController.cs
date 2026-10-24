using Microsoft.AspNetCore.Mvc;
using OT.Assessment.App.Models;
using OT.Assessment.App.Services;

namespace OT.Assessment.App.Controllers
{
    /// <summary>The games a player can choose from.</summary>
    [ApiController]
    [Route("api/games")]
    public class GamesController : ControllerBase
    {
        private readonly IGameReader _reader;
        private readonly ILogger<GamesController> _logger;

        public GamesController(IGameReader reader, ILogger<GamesController> logger)
        {
            _reader = reader;
            _logger = logger;
        }

        /// <summary>Returns all games (name, provider, theme), ordered by name.</summary>
        [HttpGet]
        public async Task<ActionResult<List<GameResponse>>> GetGames(CancellationToken cancellationToken)
        {
            var games = await _reader.GetGamesAsync(cancellationToken);

            _logger.LogInformation("Games list returned {Count} games", games.Count);

            return Ok(games);
        }
    }
}
