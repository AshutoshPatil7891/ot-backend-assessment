using OT.Assessment.App.Models;
using OT.Assessment.App.Services;
using OT.Assessment.Common.Responses;

namespace OT.Assessment.UnitTests.Fakes
{
    /// <summary>An in-memory stand-in for the SQL account store.</summary>
    public class FakePlayerAccountStore : IPlayerAccountStore
    {
        public Dictionary<string, PlayerLogin> Players { get; } =
            new Dictionary<string, PlayerLogin>(StringComparer.OrdinalIgnoreCase);

        public Task<bool> RegisterAsync(Guid accountId, string loginName, string passwordHash,
            CancellationToken cancellationToken = default)
        {
            if (Players.ContainsKey(loginName)) return Task.FromResult(false);
            Players[loginName] = new PlayerLogin { AccountId = accountId, Username = loginName, PasswordHash = passwordHash };
            return Task.FromResult(true);
        }

        public List<TopSpenderResponse> PlayersToList { get; set; } = new List<TopSpenderResponse>();
        public string LastSearch { get; private set; }
        public int LastCount { get; private set; }
        public int ListCallCount { get; private set; }

        public Task<List<TopSpenderResponse>> ListPlayersAsync(string search, int count,
            CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            LastSearch = search;
            LastCount = count;
            return Task.FromResult(PlayersToList);
        }

        public Task<PlayerLogin> FindByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
        {
            Players.TryGetValue(loginName, out var player);
            return Task.FromResult(player);
        }
    }
}
