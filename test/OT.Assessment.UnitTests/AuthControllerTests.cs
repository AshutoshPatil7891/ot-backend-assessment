using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OT.Assessment.App.Controllers;
using OT.Assessment.App.Models;
using OT.Assessment.App.Services;
using OT.Assessment.App.Settings;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for POST api/auth/register and POST api/auth/login.</summary>
    public class AuthControllerTests
    {
        private readonly FakePlayerAccountStore _store = new FakePlayerAccountStore();
        private readonly AdminSessions _sessions = new AdminSessions();

        private AuthController CreateController()
        {
            var settings = Options.Create(new AdminSettings { Username = "admin", Password = "admin-pass" });
            return new AuthController(_store, _sessions, settings, NullLogger<AuthController>.Instance);
        }

        // ---------- register ----------

        [Fact]
        public async Task Register_NewPlayer_ReturnsOkAndStoresAHashNotThePassword()
        {
            var result = await CreateController().Register(new RegisterRequest { LoginName = "ravi", Password = "pass1234" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<LoginResponse>(ok.Value);
            Assert.Equal("player", body.Role);
            Assert.NotEqual(Guid.Empty, body.AccountId);
            Assert.Equal("ravi", body.Username);
            Assert.NotEqual("pass1234", _store.Players["ravi"].PasswordHash);
        }

        [Theory]
        [InlineData(null, "pass1234")]
        [InlineData("", "pass1234")]
        [InlineData("   ", "pass1234")]
        [InlineData("ravi", null)]
        [InlineData("ravi", "")]
        [InlineData("ravi", "123")]
        public async Task Register_MissingOrShortFields_Returns400(string loginName, string password)
        {
            var result = await CreateController().Register(new RegisterRequest { LoginName = loginName, Password = password });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(_store.Players);
        }

        [Fact]
        public async Task Register_NullBody_Returns400()
        {
            var result = await CreateController().Register(null);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Register_LoginNameAlreadyTaken_Returns409()
        {
            var controller = CreateController();
            await controller.Register(new RegisterRequest { LoginName = "ravi", Password = "pass1234" });

            var result = await controller.Register(new RegisterRequest { LoginName = "ravi", Password = "other5678" });

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task Register_AdminLoginName_Returns409()
        {
            var result = await CreateController().Register(new RegisterRequest { LoginName = "ADMIN", Password = "pass1234" });

            Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Empty(_store.Players);
        }

        // ---------- login ----------

        [Fact]
        public async Task Login_CorrectPlayerCredentials_ReturnsPlayerRoleAndAccountId()
        {
            var controller = CreateController();
            var registered = (LoginResponse)((OkObjectResult)(await controller.Register(
                new RegisterRequest { LoginName = "ravi", Password = "pass1234" })).Result).Value;

            var result = await controller.Login(new LoginRequest { LoginName = "ravi", Password = "pass1234" });

            var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal("player", body.Role);
            Assert.Equal(registered.AccountId, body.AccountId);
            Assert.Null(body.Token);
        }

        [Fact]
        public async Task Login_WrongPassword_Returns401()
        {
            var controller = CreateController();
            await controller.Register(new RegisterRequest { LoginName = "ravi", Password = "pass1234" });

            var result = await controller.Login(new LoginRequest { LoginName = "ravi", Password = "wrong-pass" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Login_UnknownPlayer_Returns401()
        {
            var result = await CreateController().Login(new LoginRequest { LoginName = "nobody", Password = "pass1234" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Login_PlayerWithoutPasswordSet_Returns401()
        {
            _store.Players["old"] = new PlayerLogin { AccountId = Guid.NewGuid(), Username = "old", PasswordHash = null };

            var result = await CreateController().Login(new LoginRequest { LoginName = "old", Password = "pass1234" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Login_NullBodyOrEmptyFields_Returns400()
        {
            var controller = CreateController();

            Assert.IsType<BadRequestObjectResult>((await controller.Login(null)).Result);
            Assert.IsType<BadRequestObjectResult>((await controller.Login(new LoginRequest { LoginName = "", Password = "x" })).Result);
            Assert.IsType<BadRequestObjectResult>((await controller.Login(new LoginRequest { LoginName = "x", Password = "" })).Result);
        }

        [Fact]
        public async Task Login_AdminCredentials_ReturnsAdminRoleWithAValidToken()
        {
            var result = await CreateController().Login(new LoginRequest { LoginName = "admin", Password = "admin-pass" });

            var body = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal("admin", body.Role);
            Assert.True(_sessions.IsValid(body.Token));
        }

        [Fact]
        public async Task Login_AdminWithWrongPassword_Returns401()
        {
            var result = await CreateController().Login(new LoginRequest { LoginName = "admin", Password = "nope" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }
    }
}
