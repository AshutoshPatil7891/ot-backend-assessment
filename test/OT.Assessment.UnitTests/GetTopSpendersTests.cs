using Microsoft.AspNetCore.Mvc;
using OT.Assessment.Common.Resp;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for GET api/player/topSpenders (PlayerController.GetTopSpenders).</summary>
    public class GetTopSpendersTests
    {
        private readonly FakeBetPublisher _publisher = new FakeBetPublisher();
        private readonly FakePlayerReader _reader = new FakePlayerReader();

        // ---------- positive scenarios ----------

        [Fact]
        public async Task ValidCount_ReturnsOkWithTheReadersList()
        {
            _reader.SpendersToReturn = new List<TopSpenderResp>
            {
                new TopSpenderResp { AccountId = Guid.NewGuid(), Username = "Ravi", TotalAmountSpend = 1250m },
                new TopSpenderResp { AccountId = Guid.NewGuid(), Username = "Jay", TotalAmountSpend = 900m }
            };
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetTopSpenders(CancellationToken.None, 2);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_reader.SpendersToReturn, ok.Value);
        }

        [Fact]
        public async Task ValidCount_IsPassedToTheReader()
        {
            var controller = TestData.CreateController(_publisher, _reader);

            await controller.GetTopSpenders(CancellationToken.None, 7);

            Assert.Equal(1, _reader.SpendersCallCount);
            Assert.Equal(7, _reader.LastCount);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task CountInsideTheLimits_ReturnsOk(int count)
        {
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetTopSpenders(CancellationToken.None, count);

            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task NoSpendersYet_ReturnsOkWithAnEmptyList()
        {
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetTopSpenders(CancellationToken.None, 10);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<TopSpenderResp>>(ok.Value);
            Assert.Empty(list);
        }

        // ---------- negative scenarios ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(1000)]
        public async Task CountOutsideTheLimits_ReturnsBadRequest_AndTheDatabaseIsNotCalled(int count)
        {
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetTopSpenders(CancellationToken.None, count);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(0, _reader.SpendersCallCount);
        }
    }
}
