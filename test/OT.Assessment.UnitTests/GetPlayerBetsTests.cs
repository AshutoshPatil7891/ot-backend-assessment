using Microsoft.AspNetCore.Mvc;
using OT.Assessment.Common.Resp;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for GET api/player/{playerId}/casino (PlayerController.GetPlayerBets).</summary>
    public class GetPlayerBetsTests
    {
        private static readonly Guid PlayerId = Guid.Parse("5ac75fec-23e9-27d1-b660-179eee70003d");

        private readonly FakeBetPublisher _publisher = new FakeBetPublisher();
        private readonly FakePlayerReader _reader = new FakePlayerReader();

        // ---------- positive scenarios ----------

        [Fact]
        public async Task ValidRequest_ReturnsOkWithTheReadersPage()
        {
            _reader.BetsToReturn = new PagedResp<PlayerBetResp>
            {
                Data = new List<PlayerBetResp>
                {
                    new PlayerBetResp { BetId = Guid.NewGuid(), Game = "Game X", Provider = "Prov 1", Amount = 10m },
                    new PlayerBetResp { BetId = Guid.NewGuid(), Game = "Game Y", Provider = "Prov 2", Amount = 20m }
                },
                Page = 1, PageSize = 10, Total = 2, TotalPages = 1
            };
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetPlayerBets(PlayerId, CancellationToken.None, 1, 10);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(_reader.BetsToReturn, ok.Value);
        }

        [Fact]
        public async Task ValidRequest_PassesPlayerPageAndPageSizeToTheReader()
        {
            var controller = TestData.CreateController(_publisher, _reader);

            await controller.GetPlayerBets(PlayerId, CancellationToken.None, 3, 25);

            Assert.Equal(1, _reader.BetsCallCount);
            Assert.Equal(PlayerId, _reader.LastPlayerId);
            Assert.Equal(3, _reader.LastPage);
            Assert.Equal(25, _reader.LastPageSize);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 100)]
        [InlineData(5, 10)]
        public async Task PageAndPageSizeInsideTheLimits_ReturnOk(int page, int pageSize)
        {
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetPlayerBets(PlayerId, CancellationToken.None, page, pageSize);

            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task PlayerWithNoBets_ReturnsOkWithAnEmptyPage()
        {
            _reader.BetsToReturn = new PagedResp<PlayerBetResp> { Page = 1, PageSize = 10, Total = 0, TotalPages = 0 };
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetPlayerBets(PlayerId, CancellationToken.None, 1, 10);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var page = Assert.IsType<PagedResp<PlayerBetResp>>(ok.Value);
            Assert.Empty(page.Data);
            Assert.Equal(0, page.Total);
        }

        // ---------- negative scenarios ----------

        [Theory]
        [InlineData(0, 10)]
        [InlineData(-1, 10)]
        [InlineData(1, 0)]
        [InlineData(1, -5)]
        [InlineData(1, 101)]
        [InlineData(1, 500)]
        public async Task PageOrPageSizeOutsideTheLimits_ReturnBadRequest_AndTheDatabaseIsNotCalled(int page, int pageSize)
        {
            var controller = TestData.CreateController(_publisher, _reader);

            var result = await controller.GetPlayerBets(PlayerId, CancellationToken.None, page, pageSize);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(0, _reader.BetsCallCount);
        }
    }
}
