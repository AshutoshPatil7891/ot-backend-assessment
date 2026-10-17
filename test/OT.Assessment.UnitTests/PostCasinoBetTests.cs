using Microsoft.AspNetCore.Mvc;
using OT.Assessment.Common.msg;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for POST api/player/casinowager (PlayerController.PostCasinoBet).</summary>
    public class PostCasinoBetTests
    {
        private readonly FakeBetPublisher _publisher = new FakeBetPublisher();
        private readonly FakePlayerReader _reader = new FakePlayerReader();

        // ---------- positive scenarios ----------

        [Fact]
        public async Task ValidBet_ReturnsOk()
        {
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.PostCasinoBet(TestData.ValidBet(), CancellationToken.None);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task ValidBet_IsPublishedExactlyOnce()
        {
            var controller = TestData.CreateController(_publisher, _reader);
            var bet = TestData.ValidBet();

            await controller.PostCasinoBet(bet, CancellationToken.None);

            var published = Assert.Single(_publisher.Published);
            Assert.Same(bet, published);
        }

        [Fact]
        public async Task BetWithoutCountryCodeAndTheme_ReturnsOk()
        {
            var controller = TestData.CreateController(_publisher, _reader);
            var bet = TestData.ValidBet();
            bet.CountryCode = null;
            bet.Theme = null;

            var result = await controller.PostCasinoBet(bet, CancellationToken.None);

            Assert.IsType<OkResult>(result);
            Assert.Single(_publisher.Published);
        }

        [Fact]
        public async Task VerySmallPositiveAmount_ReturnsOk()
        {
            var controller = TestData.CreateController(_publisher, _reader);
            var bet = TestData.ValidBet();
            bet.Amount = 0.0001m;

            var result = await controller.PostCasinoBet(bet, CancellationToken.None);

            Assert.IsType<OkResult>(result);
        }

        // ---------- negative scenarios ----------

        [Theory]
        [InlineData("EmptyAccountId")]
        [InlineData("NullUsername")]
        [InlineData("BlankUsername")]
        [InlineData("NullGameName")]
        [InlineData("BlankGameName")]
        [InlineData("NullProvider")]
        [InlineData("BlankProvider")]
        [InlineData("ZeroAmount")]
        [InlineData("NegativeAmount")]
        public async Task BetWithMissingOrInvalidField_ReturnsBadRequest_AndIsNotPublished(string problem)
        {
            var controller = TestData.CreateController(_publisher, _reader);
            var bet = TestData.ValidBet();
            Break(bet, problem);

            var result = await controller.PostCasinoBet(bet, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(_publisher.Published);
        }

        [Fact]
        public async Task WhenRabbitMqFails_Returns503_InsteadOfFalseSuccess()
        {
            _publisher.ExceptionToThrow = new InvalidOperationException("RabbitMQ is down");
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.PostCasinoBet(TestData.ValidBet(), CancellationToken.None);

            var status = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(503, status.StatusCode);
        }

        [Fact]
        public async Task WhenClientCancels_TheCancellationIsNotTurnedInto503()
        {
            _publisher.ExceptionToThrow = new OperationCanceledException();
            var controller = TestData.CreateController(_publisher, _reader);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => controller.PostCasinoBet(TestData.ValidBet(), CancellationToken.None));
        }

        private static void Break(CasinoBetMesg bet, string problem)
        {
            switch (problem)
            {
                case "EmptyAccountId": bet.AccountId = Guid.Empty; break;
                case "NullUsername": bet.Username = null; break;
                case "BlankUsername": bet.Username = "   "; break;
                case "NullGameName": bet.GameName = null; break;
                case "BlankGameName": bet.GameName = ""; break;
                case "NullProvider": bet.Provider = null; break;
                case "BlankProvider": bet.Provider = " "; break;
                case "ZeroAmount": bet.Amount = 0; break;
                case "NegativeAmount": bet.Amount = -10; break;
                default: throw new ArgumentException("Unknown problem: " + problem);
            }
        }
    }
}
