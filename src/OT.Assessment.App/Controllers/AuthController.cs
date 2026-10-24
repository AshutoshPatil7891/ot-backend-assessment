using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OT.Assessment.App.Models;
using OT.Assessment.App.Services;
using OT.Assessment.App.Settings;

namespace OT.Assessment.App.Controllers
{
    /// <summary>Register and login for players, and login for the admin.</summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private const int MinPasswordLength = 4;

        private readonly IPlayerAccountStore _store;
        private readonly AdminSessions _adminSessions;
        private readonly AdminSettings _admin;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IPlayerAccountStore store,
            AdminSessions adminSessions,
            IOptions<AdminSettings> admin,
            ILogger<AuthController> logger)
        {
            _store = store;
            _adminSessions = adminSessions;
            _admin = admin.Value;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new player with a login name and password. 400 if a field is missing or the
        /// password is too short, 409 if the login name is already taken.
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<LoginResponse>> Register(
            RegisterRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LoginName)
                || string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
            {
                return BadRequest($"Login name is required and the password needs at least {MinPasswordLength} characters.");
            }

            var loginName = request.LoginName.Trim();

            // the admin login name is reserved
            if (string.Equals(loginName, _admin.Username, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("This login name is already taken.");
            }

            var accountId = Guid.NewGuid();
            var created = await _store.RegisterAsync(
                accountId, loginName, PasswordHasher.Hash(request.Password), cancellationToken);

            if (!created)
            {
                return Conflict("This login name is already taken.");
            }

            _logger.LogInformation("Player {LoginName} registered with account {AccountId}", loginName, accountId);

            return Ok(new LoginResponse { Role = "player", AccountId = accountId, Username = loginName });
        }

        /// <summary>
        /// Checks a login. Returns the role ("player" or "admin") and the player's account id.
        /// The admin also gets a token that the admin endpoints ask for. 401 if the login is wrong.
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LoginName) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Login name and password are required.");
            }

            var loginName = request.LoginName.Trim();

            if (string.Equals(loginName, _admin.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (request.Password != _admin.Password)
                {
                    _logger.LogWarning("Failed admin login");
                    return Unauthorized("Wrong login name or password.");
                }

                _logger.LogInformation("Admin logged in");
                return Ok(new LoginResponse
                {
                    Role = "admin",
                    Username = _admin.Username,
                    Token = _adminSessions.CreateToken()
                });
            }

            var player = await _store.FindByLoginNameAsync(loginName, cancellationToken);
            if (player == null || !PasswordHasher.Verify(request.Password, player.PasswordHash))
            {
                _logger.LogWarning("Failed login for {LoginName}", loginName);
                return Unauthorized("Wrong login name or password.");
            }

            _logger.LogInformation("Player {LoginName} logged in", loginName);
            return Ok(new LoginResponse { Role = "player", AccountId = player.AccountId, Username = player.Username });
        }
    }
}
