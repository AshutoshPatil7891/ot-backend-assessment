
using OT.Assessment.Common.Responses;

namespace OT.Assessment.App.Services
{
    public interface IPlayerReader
    {
        Task<PagedResponse<PlayerBetResponse>> GetPlayerBetsAsync(
            Guid playerId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<List<TopSpenderResponse>> GetTopSpendersAsync(
            int count,
            CancellationToken cancellationToken = default);
    }
}

