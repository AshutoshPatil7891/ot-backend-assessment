
using OT.Assessment.Common.Resp;

namespace OT.Assessment.App.Services
{
    public interface IPlayerReader
    {
        Task<PagedResp<PlayerBetResp>> GetPlayerBetsAsync(
            Guid playerId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<List<TopSpenderResp>> GetTopSpendersAsync(
            int count,
            CancellationToken cancellationToken = default);
    }
}

