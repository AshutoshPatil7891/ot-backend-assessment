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
            if (bet.AccountId == Guid.Empty ||
                string.IsNullOrWhiteSpace(bet.Username) ||
                string.IsNullOrWhiteSpace(bet.GameName) ||
                string.IsNullOrWhiteSpace(bet.Provider) ||
                bet.Amount <= 0)
            {
                return BadRequest("Bet is missing required fields.");
            }


            bet.CreatedDateTime = DateTimeOffset.UtcNow;

            try
            {
                await _publisher.PublishAsync(bet, cancellationToken);
                return Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish bet.");
                return StatusCode(503);
            }
        }


        [HttpGet("{playerId:guid}/casino")]
        public async Task<ActionResult<PagedResp<PlayerBetResp>>> GetPlayerBets(
            Guid playerId,
            CancellationToken cancellationToken,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Invalid page or pageSize.");
            }

            var result = await _reader.GetPlayerBetsAsync(
                playerId, page, pageSize, cancellationToken);

            return Ok(result);
        }

        [HttpGet("topSpenders")]
        public async Task<ActionResult<List<TopSpenderResp>>> GetTopSpenders(
            CancellationToken cancellationToken,
            [FromQuery] int count = 10)
        {
            if (count < 1 || count > 100)
            {
                return BadRequest("Count must be between 1 and 100.");
            }

            var result = await _reader.GetTopSpendersAsync(count, cancellationToken);

            return Ok(result);
        }


    }

}
