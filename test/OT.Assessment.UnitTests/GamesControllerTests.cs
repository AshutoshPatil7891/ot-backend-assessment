using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OT.Assessment.App.Controllers;
using OT.Assessment.App.Models;
using OT.Assessment.UnitTests.Fakes;

namespace OT.Assessment.UnitTests
{
    /// <summary>Tests for GET api/games.</summary>
    public class GamesControllerTests
    {
        [Fact]
        public async Task GetGames_ReturnsOkWithTheReadersList()
        {
            var reader = new FakeGameReader
            {
                GamesToReturn = new List<GameResponse>
                {
                    new GameResponse { GameName = "Starburst", Provider = "NetEnt", Theme = "space" },
                    new GameResponse { GameName = "Book of Dead", Provider = "Play N Go", Theme = "egypt" }
                }
            };
            var controller = new GamesController(reader, NullLogger<GamesController>.Instance);

            var result = await controller.GetGames(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(reader.GamesToReturn, ok.Value);
        }

        [Fact]
        public async Task GetGames_NoGames_ReturnsOkWithEmptyList()
        {
            var controller = new GamesController(new FakeGameReader(), NullLogger<GamesController>.Instance);

            var result = await controller.GetGames(CancellationToken.None);

            var list = Assert.IsType<List<GameResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Empty(list);
        }
    }
}
