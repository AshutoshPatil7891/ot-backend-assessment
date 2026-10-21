using OT.Assessment.App.Services;
using OT.Assessment.Common.Responses;

namespace OT.Assessment.UnitTests.Fakes
{
    /// <summary>
    /// A stand-in for the SQL reader. It returns what the test gives it and remembers
    /// how it was called, so the controller can be tested without a database.
    /// </summary>
    public class FakePlayerReader : IPlayerReader
    {
        public PagedResponse<PlayerBetResponse> BetsToReturn { get; set; } = new PagedResponse<PlayerBetResponse>();

        public List<TopSpenderResponse> SpendersToReturn { get; set; } = new List<TopSpenderResponse>();

        public int BetsCallCount { get; private set; }
        public int SpendersCallCount { get; private set; }

        public Guid LastPlayerId { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public int LastCount { get; private set; }

        public Task<PagedResponse<PlayerBetResponse>> GetPlayerBetsAsync(
            Guid playerId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            BetsCallCount++;
            LastPlayerId = playerId;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(BetsToReturn);
        }

        public Task<List<TopSpenderResponse>> GetTopSpendersAsync(
            int count, CancellationToken cancellationToken = default)
        {
            SpendersCallCount++;
            LastCount = count;
            return Task.FromResult(SpendersToReturn);
        }
    }
}
