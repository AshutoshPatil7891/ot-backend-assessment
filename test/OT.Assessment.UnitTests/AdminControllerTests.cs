using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OT.Assessment.App.Controllers;
using OT.Assessment.App.Models;
using OT.Assessment.App.Services;
using OT.Assessment.Common.Responses;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for POST api/admin/tests/{kind}.</summary>
    public class AdminControllerTests
    {
        private readonly AdminSessions _sessions = new AdminSessions();
        private readonly FakeTestRunner _runner = new FakeTestRunner();

        private readonly FakePlayerAccountStore _store = new FakePlayerAccountStore();

        private AdminController CreateController()
        {
            return new AdminController(_sessions, _runner, _store, NullLogger<AdminController>.Instance);
        }

        // ---------- registered players list ----------

        [Fact]
        public async Task GetPlayers_ValidToken_ReturnsTheStoresPlayers()
        {
            var token = _sessions.CreateToken();
            _store.PlayersToList = new List<TopSpenderResponse>
            {
                new TopSpenderResponse { AccountId = Guid.NewGuid(), Username = "ravi", TotalAmountSpend = 12.5m }
            };

            var result = await CreateController().GetPlayers(token, "rav", 20, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_store.PlayersToList, ok.Value);
            Assert.Equal("rav", _store.LastSearch);
            Assert.Equal(20, _store.LastCount);
        }

        [Fact]
        public async Task GetPlayers_NoSearch_PassesAnEmptySearch()
        {
            var token = _sessions.CreateToken();

            await CreateController().GetPlayers(token, null, 50, CancellationToken.None);

            Assert.Equal("", _store.LastSearch);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("wrong-token")]
        public async Task GetPlayers_MissingOrWrongToken_Returns401(string token)
        {
            var result = await CreateController().GetPlayers(token, "", 50, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Equal(0, _store.ListCallCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public async Task GetPlayers_CountOutOfRange_Returns400(int count)
        {
            var token = _sessions.CreateToken();

            var result = await CreateController().GetPlayers(token, "", count, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(0, _store.ListCallCount);
        }

        [Theory]
        [InlineData("unit")]
        [InlineData("load")]
        public async Task RunTests_ValidTokenAndKind_RunsAndReturnsTheResult(string kind)
        {
            var token = _sessions.CreateToken();

            var result = await CreateController().RunTests(kind, token, CancellationToken.None);

            var body = Assert.IsType<TestRunResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(kind, body.Kind);
            Assert.Equal(1, _runner.CallCount);
            Assert.Equal(kind, _runner.LastKind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-real-token")]
        public async Task RunTests_MissingOrWrongToken_Returns401AndRunsNothing(string token)
        {
            var result = await CreateController().RunTests("unit", token, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Equal(0, _runner.CallCount);
        }

        [Fact]
        public async Task RunTests_UnknownKind_Returns400AndRunsNothing()
        {
            var token = _sessions.CreateToken();

            var result = await CreateController().RunTests("format-disk", token, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(0, _runner.CallCount);
        }

        [Fact]
        public async Task RunTests_WhileAnotherRunIsInProgress_Returns409()
        {
            var token = _sessions.CreateToken();
            _runner.IsRunning = true;

            var result = await CreateController().RunTests("unit", token, CancellationToken.None);

            Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(0, _runner.CallCount);
        }
    }
}
