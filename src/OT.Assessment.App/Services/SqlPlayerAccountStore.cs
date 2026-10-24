using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.App.Models;
using OT.Assessment.Common.Responses;
using System.Data;

namespace OT.Assessment.App.Services
{
    public class SqlPlayerAccountStore : IPlayerAccountStore
    {
        // SQL Server error numbers for "duplicate key"
        private const int DuplicateKeyIndex = 2601;
        private const int DuplicateKeyConstraint = 2627;

        private readonly string _connectionString;

        public SqlPlayerAccountStore(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DatabaseConnection");
        }

        public async Task<bool> RegisterAsync(Guid accountId, string loginName, string passwordHash,
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            try
            {
                // the procedure returns 1 when the player was created and 0 when the login name exists
                var created = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        "usp_RegisterPlayer",
                        new { AccountId = accountId, LoginName = loginName, PasswordHash = passwordHash },
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: cancellationToken));
                return created == 1;
            }
            catch (SqlException ex) when (ex.Number == DuplicateKeyIndex || ex.Number == DuplicateKeyConstraint)
            {
                // two people registered the same name at the same moment
                return false;
            }
        }

        public async Task<List<TopSpenderResponse>> ListPlayersAsync(string search, int count,
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var players = await connection.QueryAsync<TopSpenderResponse>(
                new CommandDefinition(
                    "usp_GetRegisteredPlayers",
                    new { Search = search, Count = count },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

            return players.ToList();
        }

        public async Task<PlayerLogin> FindByLoginNameAsync(string loginName,
            CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            return await connection.QueryFirstOrDefaultAsync<PlayerLogin>(
                new CommandDefinition(
                    "usp_GetPlayerLogin",
                    new { LoginName = loginName },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
        }
    }
}
