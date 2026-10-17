using Microsoft.AspNetCore.Mvc;
using OT.Assessment.App.Services;
using OT.Assessment.Common.msg;

namespace OT.Assessment.App.Controllers
{
    [ApiController]
    [Route("api/player")]
    public class PlayerController : ControllerBase
    {
        private readonly IBetPublisher _publisher;
        private readonly ILogger<PlayerController> _logger;

        public PlayerController(
            IBetPublisher publisher,
            ILogger<PlayerController> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        [HttpPost("casinowager")]
        public async Task<IActionResult> PostCasinoBet(
            CasinoBetMesg bet,
            CancellationToken cancellationToken)
        {
            if (bet.BetId == Guid.Empty ||
                bet.AccountId == Guid.Empty ||
                string.IsNullOrWhiteSpace(bet.Username) ||
                string.IsNullOrWhiteSpace(bet.GameName) ||
                string.IsNullOrWhiteSpace(bet.Provider) ||
                bet.Amount <= 0)
            {
                return BadRequest("Bet is missing required fields.");
            }

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
    }
}
