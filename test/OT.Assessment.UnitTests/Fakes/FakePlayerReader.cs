using OT.Assessment.App.Services;
using OT.Assessment.Common.Resp;

namespace OT.Assessment.UnitTests.Fakes
{
    /// <summary>
    /// A stand-in for the SQL reader. It returns what the test gives it and remembers
    /// how it was called, so the controller can be tested without a database.
    /// </summary>
    public class FakePlayerReader : IPlayerReader
    {
        public PagedResp<PlayerBetResp> BetsToReturn { get; set; } = new PagedResp<PlayerBetResp>();

        public List<TopSpenderResp> SpendersToReturn { get; set; } = new List<TopSpenderResp>();

        public int BetsCallCount { get; private set; }
        public int SpendersCallCount { get; private set; }

        public Guid LastPlayerId { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public int LastCount { get; private set; }

        public Task<PagedResp<PlayerBetResp>> GetPlayerBetsAsync(
            Guid playerId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            BetsCallCount++;
            LastPlayerId = playerId;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(BetsToReturn);
        }

        public Task<List<TopSpenderResp>> GetTopSpendersAsync(
            int count, CancellationToken cancellationToken = default)
        {
            SpendersCallCount++;
            LastCount = count;
            return Task.FromResult(SpendersToReturn);
        }
    }
}
