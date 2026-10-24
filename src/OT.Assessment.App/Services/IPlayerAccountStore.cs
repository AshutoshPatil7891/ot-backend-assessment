using OT.Assessment.App.Models;
using OT.Assessment.Common.Responses;

namespace OT.Assessment.App.Services
{
    public interface IPlayerAccountStore
    {
        /// <summary>Creates a player with a login. Returns false if the login name is already taken.</summary>
        Task<bool> RegisterAsync(Guid accountId, string loginName, string passwordHash,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Players who have a login, whose login name contains the search text (empty = all),
        /// with the total they have spent so far.
        /// </summary>
        Task<List<TopSpenderResponse>> ListPlayersAsync(string search, int count,
            CancellationToken cancellationToken = default);

        /// <summary>Finds the player with this login name, or null.</summary>
        Task<PlayerLogin> FindByLoginNameAsync(string loginName,
            CancellationToken cancellationToken = default);
    }
}
