using Microsoft.Extensions.Logging;
using OT.Assessment.Common.Resp;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Tests that every endpoint writes the log lines needed to find a problem later:
    /// information for requests that worked, warnings for rejected input, errors for failures.
    /// </summary>
    [Collection(PostEndpointCollection.Name)]
    public class ControllerLoggingTests
    {
        private readonly FakeBetPublisher _publisher = new FakeBetPublisher();
        private readonly FakePlayerReader _reader = new FakePlayerReader();
        private readonly FakeLogger<OT.Assessment.App.Controllers.PlayerController> _logger =
            new FakeLogger<OT.Assessment.App.Controllers.PlayerController>();

        private OT.Assessment.App.Controllers.PlayerController CreateController()
        {
            return TestData.CreateController(_publisher, _reader, _logger);
        }

        // ---------- POST api/player/casinowager ----------

        [Fact]
        public async Task AcceptedBet_LogsInformationWithTheBetIdAndAccountId()
        {
            var bet = TestData.ValidBet();

            await CreateController().PostCasinoBet(bet, CancellationToken.None);

            var message = Assert.Single(_logger.Messages(LogLevel.Information));
            Assert.Contains(bet.BetId.ToString(), message);
            Assert.Contains(bet.AccountId.ToString(), message);
        }

        [Fact]
        public async Task RejectedBet_LogsAWarningWithTheBetId_AndNoInformation()
        {
            var bet = TestData.ValidBet();
            bet.Amount = 0;

            await CreateController().PostCasinoBet(bet, CancellationToken.None);

            var message = Assert.Single(_logger.Messages(LogLevel.Warning));
            Assert.Contains(bet.BetId.ToString(), message);
            Assert.Empty(_logger.Messages(LogLevel.Information));
        }

        [Fact]
        public async Task RejectedBet_WarningNamesTheFieldsThatAreWrong()
        {
            var bet = TestData.ValidBet();
            bet.Username = " ";
            bet.Amount = -5;

            await CreateController().PostCasinoBet(bet, CancellationToken.None);

            var message = Assert.Single(_logger.Messages(LogLevel.Warning));
            Assert.Contains("Username", message);
            Assert.Contains("Amount", message);
            Assert.DoesNotContain("GameName", message);
        }

        [Fact]
        public async Task PublishFailure_LogsAnErrorWithTheBetId_AndNoInformation()
        {
            _publisher.ExceptionToThrow = new InvalidOperationException("RabbitMQ is down");
            var bet = TestData.ValidBet();

            await CreateController().PostCasinoBet(bet, CancellationToken.None);

            var message = Assert.Single(_logger.Messages(LogLevel.Error));
            Assert.Contains(bet.BetId.ToString(), message);
            Assert.Empty(_logger.Messages(LogLevel.Information));
        }

        // ---------- GET api/player/{playerId}/casino ----------

        [Fact]
        public async Task PlayerBets_LogsInformationWithThePlayerPagingAndResultSize()
        {
            var playerId = Guid.NewGuid();
            _reader.BetsToReturn = new PagedResp<PlayerBetResp>
            {
                Data = new List<PlayerBetResp> { new PlayerBetResp(), new PlayerBetResp() },
                Total = 12
            };

            await CreateController().GetPlayerBets(playerId, CancellationToken.None, 2, 5);

            var message = Assert.Single(_logger.Messages(LogLevel.Information));
            Assert.Contains(playerId.ToString(), message);
            Assert.Contains("2", message);
            Assert.Contains("12", message);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 0)]
        [InlineData(1, 101)]
        public async Task PlayerBetsWithBadPaging_LogsAWarningWithTheValues(int page, int pageSize)
        {
            var playerId = Guid.NewGuid();

            await CreateController().GetPlayerBets(playerId, CancellationToken.None, page, pageSize);

            var message = Assert.Single(_logger.Messages(LogLevel.Warning));
            Assert.Contains(playerId.ToString(), message);
            Assert.Contains(page.ToString(), message);
            Assert.Contains(pageSize.ToString(), message);
            Assert.Empty(_logger.Messages(LogLevel.Information));
        }

        // ---------- GET api/player/topSpenders ----------

        [Fact]
        public async Task TopSpenders_LogsInformationWithTheCountAndHowManyWereReturned()
        {
            _reader.SpendersToReturn = new List<TopSpenderResp>
            {
                new TopSpenderResp { Username = "Ravi" },
                new TopSpenderResp { Username = "Jay" },
                new TopSpenderResp { Username = "Mira" }
            };

            await CreateController().GetTopSpenders(CancellationToken.None, 10);

            var message = Assert.Single(_logger.Messages(LogLevel.Information));
            Assert.Contains("10", message);
            Assert.Contains("3", message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public async Task TopSpendersWithBadCount_LogsAWarningWithTheValue(int count)
        {
            await CreateController().GetTopSpenders(CancellationToken.None, count);

            var message = Assert.Single(_logger.Messages(LogLevel.Warning));
            Assert.Contains(count.ToString(), message);
            Assert.Empty(_logger.Messages(LogLevel.Information));
        }
    }
}
