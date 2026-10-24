using OT.Assessment.App.Models;
using OT.Assessment.App.Services;

namespace OT.Assessment.UnitTests.Fakes
{
    public class FakeGameReader : IGameReader
    {
        public List<GameResponse> GamesToReturn { get; set; } = new List<GameResponse>();

        public Task<List<GameResponse>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GamesToReturn);
        }
    }
}
