using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.App.Models;
using System.Data;

namespace OT.Assessment.App.Services
{
    public class SqlGameReader : IGameReader
    {
        private readonly string _connectionString;

        public SqlGameReader(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DatabaseConnection");
        }

        public async Task<List<GameResponse>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var games = await connection.QueryAsync<GameResponse>(
                new CommandDefinition(
                    "usp_GetGames",
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

            return games.ToList();
        }
    }
}
