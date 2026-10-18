using OT.Assessment.App.Monitoring;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Tests that the POST endpoint feeds the numbers shown in Grafana: how many bets were
    /// accepted, rejected or failed, and how long publishing took. The counters are shared by
    /// all tests, so each test compares the value before and after its own call.
    /// </summary>
    [Collection(PostEndpointCollection.Name)]
    public class ApiMetricsTests
    {
        private readonly FakeBetPublisher _publisher = new FakeBetPublisher();
        private readonly FakePlayerReader _reader = new FakePlayerReader();

        [Fact]
        public async Task AcceptedBet_CountsOneAcceptedBet_AndNothingElse()
        {
            var accepted = ApiMetrics.BetsAccepted.Value;
            var rejected = ApiMetrics.BetsRejected.Value;
            var failed = ApiMetrics.BetsPublishFailed.Value;

            await TestData.CreateController(_publisher, _reader).PostCasinoBet(TestData.ValidBet(), CancellationToken.None);

            Assert.Equal(accepted + 1, ApiMetrics.BetsAccepted.Value);
            Assert.Equal(rejected, ApiMetrics.BetsRejected.Value);
            Assert.Equal(failed, ApiMetrics.BetsPublishFailed.Value);
        }

        [Fact]
        public async Task RejectedBet_CountsOneRejectedBet_AndNothingElse()
        {
            var bet = TestData.ValidBet();
            bet.Amount = 0;
            var accepted = ApiMetrics.BetsAccepted.Value;
            var rejected = ApiMetrics.BetsRejected.Value;
            var failed = ApiMetrics.BetsPublishFailed.Value;

            await TestData.CreateController(_publisher, _reader).PostCasinoBet(bet, CancellationToken.None);

            Assert.Equal(accepted, ApiMetrics.BetsAccepted.Value);
            Assert.Equal(rejected + 1, ApiMetrics.BetsRejected.Value);
            Assert.Equal(failed, ApiMetrics.BetsPublishFailed.Value);
        }

        [Fact]
        public async Task PublishFailure_CountsOneFailedBet_AndNothingElse()
        {
            _publisher.ExceptionToThrow = new InvalidOperationException("RabbitMQ is down");
            var accepted = ApiMetrics.BetsAccepted.Value;
            var rejected = ApiMetrics.BetsRejected.Value;
            var failed = ApiMetrics.BetsPublishFailed.Value;

            await TestData.CreateController(_publisher, _reader).PostCasinoBet(TestData.ValidBet(), CancellationToken.None);

            Assert.Equal(accepted, ApiMetrics.BetsAccepted.Value);
            Assert.Equal(rejected, ApiMetrics.BetsRejected.Value);
            Assert.Equal(failed + 1, ApiMetrics.BetsPublishFailed.Value);
        }

        [Fact]
        public async Task EveryPublishAttempt_IsTimed_AcceptedOrFailed()
        {
            var before = ApiMetrics.PublishDuration.Count;

            await TestData.CreateController(_publisher, _reader).PostCasinoBet(TestData.ValidBet(), CancellationToken.None);
            _publisher.ExceptionToThrow = new InvalidOperationException("RabbitMQ is down");
            await TestData.CreateController(_publisher, _reader).PostCasinoBet(TestData.ValidBet(), CancellationToken.None);

            Assert.Equal(before + 2, ApiMetrics.PublishDuration.Count);
        }

        [Fact]
        public async Task RejectedBet_IsNotTimed_BecauseNothingWasPublished()
        {
            var bet = TestData.ValidBet();
            bet.Amount = -1;
            var before = ApiMetrics.PublishDuration.Count;

            await TestData.CreateController(_publisher, _reader).PostCasinoBet(bet, CancellationToken.None);

            Assert.Equal(before, ApiMetrics.PublishDuration.Count);
        }

        [Fact]
        public async Task ClientCancellation_IsNotCountedAsAFailure()
        {
            _publisher.ExceptionToThrow = new OperationCanceledException();
            var failed = ApiMetrics.BetsPublishFailed.Value;

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => TestData.CreateController(_publisher, _reader).PostCasinoBet(TestData.ValidBet(), CancellationToken.None));

            Assert.Equal(failed, ApiMetrics.BetsPublishFailed.Value);
        }
    }
}
