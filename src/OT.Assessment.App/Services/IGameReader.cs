using OT.Assessment.App.Models;

namespace OT.Assessment.App.Services
{
    public interface IGameReader
    {
        Task<List<GameResponse>> GetGamesAsync(CancellationToken cancellationToken = default);
    }
}
