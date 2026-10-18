using Microsoft.AspNetCore.Mvc;
using OT.Assessment.App.Services;
using OT.Assessment.Common.msg;
using OT.Assessment.Common.Resp;

namespace OT.Assessment.App.Controllers
{
    [ApiController]
    [Route("api/player")]
    public class PlayerController : ControllerBase
    {
        private readonly IBetPublisher _publisher;
        private readonly ILogger<PlayerController> _logger;
        private readonly IPlayerReader _reader;


        //constructor
        public PlayerController(
      IBetPublisher publisher,
      IPlayerReader reader,
      ILogger<PlayerController> logger)
        {
            _publisher = publisher;
            _reader = reader;
            _logger = logger;
        }
        /// <summary>
        /// Sends one bet data to the RabbitMQ queue and waits until RabbitMQ confirms it has received it.
        /// Connects on first use and reuses the same connection afterwards.
        /// </summary>
        [HttpPost("casinowager")]
        public async Task<IActionResult> PostCasinoBet(
            CasinoBetMesg bet,
            CancellationToken cancellationToken)
        {
            var invalidFields = InvalidFields(bet);
            if (invalidFields.Count > 0)
            {
                _logger.LogWarning("Bet {BetId} for player {AccountId} rejected, invalid fields: {Fields}",
                    bet.BetId, bet.AccountId, string.Join(", ", invalidFields));
                return BadRequest("Bet is missing required fields.");
            }

            try
            {
                await _publisher.PublishAsync(bet, cancellationToken);
                _logger.LogInformation("Bet {BetId} for player {AccountId} accepted and queued", bet.BetId, bet.AccountId);
                return Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish bet {BetId}", bet.BetId);
                return StatusCode(503);
            }
        }

        // names of the required fields that are missing or invalid (empty list = the bet is valid)
        private static List<string> InvalidFields(CasinoBetMesg bet)
        {
            var invalid = new List<string>();
            if (bet.BetId == Guid.Empty) invalid.Add("BetId");
            if (bet.AccountId == Guid.Empty) invalid.Add("AccountId");
            if (bet.CreatedDateTime == default) invalid.Add("CreatedDateTime");
            if (string.IsNullOrWhiteSpace(bet.Username)) invalid.Add("Username");
            if (string.IsNullOrWhiteSpace(bet.GameName)) invalid.Add("GameName");
            if (string.IsNullOrWhiteSpace(bet.Provider)) invalid.Add("Provider");
            if (bet.Amount <= 0) invalid.Add("Amount");
            return invalid;
        }


        /// <summary>
        /// Returns one page of a player's latest casino bets, newest first.
        /// </summary>
        /// <param name="playerId">The player's account id (a GUID).</param>
        /// <param name="page">The page number, starting at 1 (default 1).</param>
        /// <param name="pageSize">How many bets per page, from 1 to 100 (default 10).</param>
        /// <param name="cancellationToken">Cancelled if the client disconnects.</param>
        [HttpGet("{playerId:guid}/casino")]
        public async Task<ActionResult<PagedResp<PlayerBetResp>>> GetPlayerBets(
            Guid playerId,
            CancellationToken cancellationToken,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid paging for player {PlayerId}: page {Page}, pageSize {PageSize}",
                    playerId, page, pageSize);
                return BadRequest("Invalid page or pageSize.");
            }

            var result = await _reader.GetPlayerBetsAsync(
                playerId, page, pageSize, cancellationToken);

            _logger.LogInformation("Player {PlayerId} bets: page {Page}, pageSize {PageSize}, returned {Count} of {Total}",
                playerId, page, pageSize, result.Data.Count, result.Total);

            return Ok(result);
        }

        /// <summary>
        /// Returns the top players by total amount spent across all games, highest first.
        /// </summary>
        /// <param name="count">How many players to return, from 1 to 100 (default 10).</param>
        /// <param name="cancellationToken">Cancelled if the client disconnects.</param>
        [HttpGet("topSpenders")]
        public async Task<ActionResult<List<TopSpenderResp>>> GetTopSpenders(
            CancellationToken cancellationToken,
            [FromQuery] int count = 10)
        {
            if (count < 1 || count > 100)
            {
                _logger.LogWarning("Invalid top spenders count {Count}", count);
                return BadRequest("Count must be between 1 and 100.");
            }

            var result = await _reader.GetTopSpendersAsync(count, cancellationToken);

            _logger.LogInformation("Top spenders: count {Count}, returned {Returned}", count, result.Count);

            return Ok(result);
        }


    }

}
